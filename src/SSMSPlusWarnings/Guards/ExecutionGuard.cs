namespace SSMSPlusWarnings.Guards
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    using EnvDTE;

    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;

    using SSMSPlusCore.Integration;
    using SSMSPlusCore.Settings;

    /// <summary>
    /// Asks before executing a query with an UPDATE / DELETE without WHERE or JOIN condition (Preferences, on by default).
    /// </summary>
    public class ExecutionGuard
    {
        private const int MaxListedStatements = 10;
        private const int IDYES = 6;

        private readonly PackageProvider _packageProvider;
        private readonly UserPreferencesStore _preferencesStore;
        private readonly ILogger<ExecutionGuard> _logger;
        private CommandEvents _queryExecuteEvents;

        public ExecutionGuard(PackageProvider packageProvider, UserPreferencesStore preferencesStore, ILogger<ExecutionGuard> logger)
        {
            _packageProvider = packageProvider;
            _preferencesStore = preferencesStore;
            _logger = logger;
        }

        /// <summary>Replaced in tests. Returns true to execute.</summary>
        public Func<string, bool> Confirm { get; set; }

        public void Start()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Confirm = Confirm ?? ShowConfirmation;

            var command = _packageProvider.Dte2.Commands.Item("Query.Execute");
            _queryExecuteEvents = _packageProvider.Dte2.Events.get_CommandEvents(command.Guid, command.ID);

            // SSMS 22 redirects EnvDTE 8.0 to Microsoft.VisualStudio.Interop 18.0: create the delegate from the runtime type (see QueryTracker)
            var beforeExecute = typeof(_dispCommandEvents_Event).GetEvent(nameof(_dispCommandEvents_Event.BeforeExecute));
            beforeExecute.AddEventHandler(_queryExecuteEvents, Delegate.CreateDelegate(beforeExecute.EventHandlerType, this, nameof(QueryExecute_BeforeExecute)));
        }

        private void QueryExecute_BeforeExecute(string guid, int id, object customIn, object customOut, ref bool cancelDefault)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (cancelDefault || !_preferencesStore.Preferences.ConfirmUnsafeStatements)
                return;

            try
            {
                if (!GetExecutedText(out string sql, out int lineOffset))
                    return;

                cancelDefault = !ShouldExecute(sql, lineOffset);
            }
            catch (Exception ex)
            {
                // never block the execution because of the guard itself
                _logger.LogError(ex, "Could not check the query for UPDATE / DELETE without WHERE");
            }
        }

        /// <summary>True to execute: nothing unsafe found, or the user said yes.</summary>
        public bool ShouldExecute(string sql, int lineOffset)
        {
            var statements = UnsafeStatementFinder.Find(sql);
            if (statements.Count == 0)
                return true;

            bool execute = Confirm(BuildMessage(statements, lineOffset));
            _logger.LogInformation("{Count} UPDATE / DELETE without WHERE: {Answer}", statements.Count, execute ? "executed" : "cancelled");
            return execute;
        }

        public static string BuildMessage(IReadOnlyList<UnsafeStatement> statements, int lineOffset)
        {
            var message = new StringBuilder();
            message.AppendLine(statements.Count == 1
                ? "This statement will change ALL the rows of the table:"
                : "These statements will change ALL the rows of their table:");
            message.AppendLine();
            foreach (var statement in statements.Take(MaxListedStatements))
                message.AppendLine(statement.Describe(lineOffset));

            if (statements.Count > MaxListedStatements)
                message.AppendLine($"... and {statements.Count - MaxListedStatements} more");

            message.AppendLine();
            message.Append("Execute anyway?");
            return message.ToString();
        }

        /// <summary>The selection when there is one (SSMS executes only the selection), else the whole query.</summary>
        private bool GetExecutedText(out string sql, out int lineOffset)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            sql = null;
            lineOffset = 0;

            var document = _packageProvider.Dte2.ActiveDocument;
            if (!(document?.Object("TextDocument") is TextDocument textDocument))
                return false;

            var selection = textDocument.Selection;
            if (!string.IsNullOrEmpty(selection.Text))
            {
                // line 1 of the selection is the line where it starts in the document
                sql = selection.Text;
                lineOffset = selection.TopPoint.Line - 1;
            }
            else
            {
                sql = textDocument.StartPoint.CreateEditPoint().GetText(textDocument.EndPoint);
            }

            return !string.IsNullOrWhiteSpace(sql);
        }

        private bool ShowConfirmation(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            int answer = VsShellUtilities.ShowMessageBox(
                ServiceProvider.GlobalProvider,
                message,
                "SSMS Plus - UPDATE / DELETE without WHERE",
                OLEMSGICON.OLEMSGICON_WARNING,
                OLEMSGBUTTON.OLEMSGBUTTON_YESNO,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_SECOND);
            return answer == IDYES;
        }
    }
}
