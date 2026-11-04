namespace SSMSPlusWarnings.Guards
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    using Microsoft.SqlServer.TransactSql.ScriptDom;

    /// <summary>An UPDATE / DELETE changing every row of a table.</summary>
    public class UnsafeStatement
    {
        /// <summary>1-based, in the executed text.</summary>
        public int Line { get; set; }

        /// <summary>UPDATE or DELETE.</summary>
        public string Kind { get; set; }

        /// <summary>The target table as written in the query.</summary>
        public string Target { get; set; }

        /// <summary>True when there is a FROM clause (a JOIN could have restricted the rows).</summary>
        public bool HasFrom { get; set; }

        public string Describe(int lineOffset)
        {
            return $"Line {Line + lineOffset}: {Kind} {Target} - no WHERE{(HasFrom ? " or JOIN condition" : string.Empty)}";
        }
    }

    /// <summary>
    /// Finds the UPDATE and DELETE statements without WHERE or join condition, with the T-SQL parser of SSMS.
    /// Not reported: temp tables (#t, ##t), table variables (@t), TOP (n), WHERE CURRENT OF and
    /// statements inside CREATE / ALTER PROCEDURE, FUNCTION or TRIGGER (not executed by the script).
    /// </summary>
    public static class UnsafeStatementFinder
    {
        // a batch separator line: GO, GO 5, GO -- comment
        private static readonly Regex BatchSeparator = new Regex(@"^\s*GO(\s+\d+)?\s*(--.*)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static IReadOnlyList<UnsafeStatement> Find(string sql)
        {
            var found = new List<UnsafeStatement>();
            if (string.IsNullOrWhiteSpace(sql))
                return found;

            var script = Parse(sql, out var errors);
            if (errors.Count == 0)
            {
                script.Accept(new Visitor(found, 0));
                return found;
            }

            // a syntax error (or SQLCMD lines) in one batch: check the other batches one by one,
            // the failing batch would not run anyway
            foreach (var batch in SplitBatches(sql))
            {
                var batchScript = Parse(batch.Text, out var batchErrors);
                if (batchErrors.Count == 0)
                    batchScript.Accept(new Visitor(found, batch.FirstLine - 1));
            }

            return found;
        }

        private static TSqlFragment Parse(string sql, out IList<ParseError> errors)
        {
            var parser = new TSql170Parser(initialQuotedIdentifiers: true);
            using (var reader = new StringReader(sql))
            {
                return parser.Parse(reader, out errors);
            }
        }

        private static IEnumerable<(string Text, int FirstLine)> SplitBatches(string sql)
        {
            var lines = sql.Replace("\r\n", "\n").Split('\n');
            var batch = new StringBuilder();
            int firstLine = 1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (BatchSeparator.IsMatch(lines[i]) || lines[i].TrimStart().StartsWith(":"))
                {
                    if (batch.Length > 0)
                        yield return (batch.ToString(), firstLine);

                    batch.Clear();
                    firstLine = i + 2;
                    continue;
                }

                batch.Append(lines[i]).Append('\n');
            }

            if (batch.Length > 0)
                yield return (batch.ToString(), firstLine);
        }

        private sealed class Visitor : TSqlFragmentVisitor
        {
            private readonly List<UnsafeStatement> _found;
            private readonly int _lineOffset;

            public Visitor(List<UnsafeStatement> found, int lineOffset)
            {
                _found = found;
                _lineOffset = lineOffset;
            }

            public override void ExplicitVisit(UpdateStatement node)
            {
                Check("UPDATE", node, node.UpdateSpecification);
                base.ExplicitVisit(node);
            }

            public override void ExplicitVisit(DeleteStatement node)
            {
                Check("DELETE", node, node.DeleteSpecification);
                base.ExplicitVisit(node);
            }

            // module bodies are created, not executed
            public override void ExplicitVisit(CreateProcedureStatement node) { }
            public override void ExplicitVisit(AlterProcedureStatement node) { }
            public override void ExplicitVisit(CreateOrAlterProcedureStatement node) { }
            public override void ExplicitVisit(CreateFunctionStatement node) { }
            public override void ExplicitVisit(AlterFunctionStatement node) { }
            public override void ExplicitVisit(CreateOrAlterFunctionStatement node) { }
            public override void ExplicitVisit(CreateTriggerStatement node) { }
            public override void ExplicitVisit(AlterTriggerStatement node) { }
            public override void ExplicitVisit(CreateOrAlterTriggerStatement node) { }

            private void Check(string kind, TSqlStatement statement, UpdateDeleteSpecificationBase spec)
            {
                if (spec == null || spec.WhereClause != null || spec.TopRowFilter != null)
                    return;

                var tables = spec.FromClause?.TableReferences ?? (IList<TableReference>)new TableReference[0];
                if (tables.Any(HasJoinCondition))
                    return;

                var target = Resolve(spec.Target, tables);
                if (IsTempOrVariable(target))
                    return;

                _found.Add(new UnsafeStatement
                {
                    Line = statement.StartLine + _lineOffset,
                    Kind = kind,
                    Target = TextOf(spec.Target),
                    HasFrom = spec.FromClause != null,
                });
            }

            /// <summary>INNER JOIN, RIGHT JOIN and CROSS APPLY restrict the rows; LEFT / FULL / CROSS JOIN do not.</summary>
            private static bool HasJoinCondition(TableReference table)
            {
                switch (table)
                {
                    case QualifiedJoin join:
                        return join.QualifiedJoinType == QualifiedJoinType.Inner
                            || join.QualifiedJoinType == QualifiedJoinType.RightOuter
                            || HasJoinCondition(join.FirstTableReference)
                            || HasJoinCondition(join.SecondTableReference);
                    case UnqualifiedJoin join:
                        return join.UnqualifiedJoinType == UnqualifiedJoinType.CrossApply
                            || HasJoinCondition(join.FirstTableReference)
                            || HasJoinCondition(join.SecondTableReference);
                    case JoinParenthesisTableReference parenthesis:
                        return HasJoinCondition(parenthesis.Join);
                    default:
                        return false;
                }
            }

            /// <summary>"UPDATE a ... FROM #t a": the target is the table aliased in the FROM clause.</summary>
            private static TableReference Resolve(TableReference target, IList<TableReference> fromTables)
            {
                if (!(target is NamedTableReference named) || named.SchemaObject.SchemaIdentifier != null)
                    return target;

                string name = named.SchemaObject.BaseIdentifier.Value;
                foreach (var table in fromTables.SelectMany(Flatten))
                {
                    if (table is TableReferenceWithAlias aliased && string.Equals(aliased.Alias?.Value, name, StringComparison.OrdinalIgnoreCase))
                        return table;
                }

                return target;
            }

            private static IEnumerable<TableReference> Flatten(TableReference table)
            {
                switch (table)
                {
                    case JoinTableReference join:
                        return Flatten(join.FirstTableReference).Concat(Flatten(join.SecondTableReference));
                    case JoinParenthesisTableReference parenthesis:
                        return Flatten(parenthesis.Join);
                    default:
                        return new[] { table };
                }
            }

            private static bool IsTempOrVariable(TableReference table)
            {
                switch (table)
                {
                    case VariableTableReference _:
                        return true;
                    case NamedTableReference named:
                        return named.SchemaObject.BaseIdentifier.Value.StartsWith("#");
                    default:
                        return false;
                }
            }

            private static string TextOf(TSqlFragment fragment)
            {
                if (fragment is NamedTableReference named)
                    fragment = named.SchemaObject;

                var text = new StringBuilder();
                for (int i = fragment.FirstTokenIndex; i <= fragment.LastTokenIndex; i++)
                    text.Append(fragment.ScriptTokenStream[i].Text);

                return Regex.Replace(text.ToString(), @"\s+", " ").Trim();
            }
        }
    }
}
