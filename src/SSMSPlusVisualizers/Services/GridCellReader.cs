namespace SSMSPlusVisualizers.Services
{
    using System;
    using System.Collections;
    using System.Collections.Specialized;
    using System.Reflection;

    using Microsoft.VisualStudio;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;

    /// <summary>The current cell of a results grid, with its value as stored by SSMS.</summary>
    public class GridCell
    {
        /// <summary>byte[], string, or another value type of the column.</summary>
        public object Value { get; set; }

        public string ColumnName { get; set; }

        /// <summary>The value may be cut: SSMS stores at most the "Non XML data" number of bytes / characters.</summary>
        public bool MayBeTruncated { get; set; }

        /// <summary>The SSMS limit, for the truncation message.</summary>
        public int StoredLimit { get; set; }
    }

    /// <summary>
    /// Reads the current cell of the focused results grid of the active query window, from the SSMS result storage
    /// (not the grid text, which shows at most half of the stored bytes). The SSMS types are internal: reflection.
    /// </summary>
    public static class GridCellReader
    {
        private const string EditorTypeName = "Microsoft.SqlServer.Management.UI.VSIntegration.Editors.SqlScriptEditorControl";
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>The current cell when exactly one data cell with a value is selected, else null (null cell, several cells, row number column).</summary>
        public static GridCell ReadActive()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var editor = GetActiveEditor();
            if (editor == null)
                return null;

            var resultsControl = GetMember(editor, "m_sqlResultsControl");
            var gridPage = resultsControl != null ? GetMember(resultsControl, "m_gridResultsPage") : null;
            var grid = gridPage != null ? GetMember(gridPage, "FocusedGrid") : null;
            if (grid == null)
                return null;

            return ReadCurrentCell(grid);
        }

        /// <summary>For a SSMS GridControl.</summary>
        public static GridCell ReadCurrentCell(object grid)
        {
            if (!IsSingleCellSelected(grid))
                return null;

            var args = new object[] { 0L, 0 };
            grid.GetType().GetMethod("GetCurrentCell", new[] { typeof(long).MakeByRefType(), typeof(int).MakeByRefType() }).Invoke(grid, args);
            long row = (long)args[0];
            int column = (int)args[1];

            var resultSet = grid.GetType().GetProperty("GridStorage")?.GetValue(grid);
            return resultSet != null ? Read(resultSet, row, column) : null;
        }

        /// <summary>For a SSMS QEResultSet. In grid mode the column 0 is the row number.</summary>
        public static GridCell Read(object resultSet, long row, int column)
        {
            var type = resultSet.GetType();
            bool gridMode = type.GetProperty("GridMode")?.GetValue(resultSet) as bool? ?? false;
            int dataColumn = gridMode ? column - 1 : column;
            if (row < 0 || dataColumn < 0)
                return null;

            if ((bool)type.GetMethod("IsCellDataNull").Invoke(resultSet, new object[] { row, column }))
                return null;

            var value = Normalize(type.GetMethod("GetCellData").Invoke(resultSet, new object[] { row, column }));
            if (value == null)
                return null;

            var names = type.GetProperty("ColumnNames")?.GetValue(resultSet) as StringCollection;
            var storage = GetMember(resultSet, "m_storage");
            int limit = storage?.GetType().GetProperty("MaxCharsToStore")?.GetValue(storage) as int? ?? 0;
            int length = value is byte[] bytes ? bytes.Length : value is string text ? text.Length : 0;

            return new GridCell
            {
                Value = value,
                ColumnName = names != null && dataColumn < names.Count ? names[dataColumn] : null,
                StoredLimit = limit,
                MayBeTruncated = limit > 0 && length >= limit,
            };
        }

        /// <summary>byte[] for the binary types, string for the text types, the value itself for the others; null for NULL.</summary>
        public static object Normalize(object value)
        {
            switch (value)
            {
                case null:
                case DBNull _:
                    return null;
                case System.Data.SqlTypes.INullable nullable when nullable.IsNull:
                    return null;
                case System.Data.SqlTypes.SqlBytes sqlBytes:
                    return sqlBytes.Value;
                case System.Data.SqlTypes.SqlBinary sqlBinary:
                    return sqlBinary.Value;
                case System.Data.SqlTypes.SqlString sqlString:
                    return sqlString.Value;
                case System.Data.SqlTypes.SqlChars sqlChars:
                    return new string(sqlChars.Value);
                case System.Data.SqlTypes.SqlXml sqlXml:
                    return sqlXml.Value;
                case System.Data.SqlTypes.SqlGuid sqlGuid:
                    return sqlGuid.Value.ToString();
                default:
                    return value;
            }
        }

        private static bool IsSingleCellSelected(object grid)
        {
            if (!(grid.GetType().GetProperty("SelectedCells")?.GetValue(grid) is IEnumerable blocks))
                return true;

            int count = 0;
            foreach (var block in blocks)
            {
                if (++count > 1)
                    return false;

                var width = block.GetType().GetProperty("Width")?.GetValue(block);
                var height = block.GetType().GetProperty("Height")?.GetValue(block);
                if (width != null && Convert.ToInt64(width) != 1 || height != null && Convert.ToInt64(height) != 1)
                    return false;
            }

            return true;
        }

        private static object GetActiveEditor()
        {
            var monitorSelection = (IVsMonitorSelection)Package.GetGlobalService(typeof(SVsShellMonitorSelection));
            if (monitorSelection == null
                || ErrorHandler.Failed(monitorSelection.GetCurrentElementValue((uint)VSConstants.VSSELELEMID.SEID_DocumentFrame, out object value))
                || !(value is IVsWindowFrame frame)
                || ErrorHandler.Failed(frame.GetProperty((int)__VSFPROPID.VSFPROPID_DocView, out object view)))
                return null;

            for (var type = view?.GetType(); type != null; type = type.BaseType)
            {
                if (type.FullName == EditorTypeName)
                    return view;
            }

            return null;
        }

        /// <summary>A field or property, public or not, declared on the type or a base type.</summary>
        private static object GetMember(object target, string name)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, AnyInstance | BindingFlags.DeclaredOnly);
                if (field != null)
                    return field.GetValue(target);

                var property = type.GetProperty(name, AnyInstance | BindingFlags.DeclaredOnly);
                if (property != null)
                    return property.GetValue(target);
            }

            return null;
        }
    }
}
