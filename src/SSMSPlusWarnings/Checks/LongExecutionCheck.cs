namespace SSMSPlusWarnings.Checks
{
    using System;

    using Microsoft.VisualStudio.Shell;

    using SSMSPlusCore.Settings;

    using SSMSPlusWarnings.Entities;
    using SSMSPlusWarnings.Services;

    /// <summary>Shows the information of an execution which took longer than the threshold (Settings > Long Executions).</summary>
    public class LongExecutionCheck : IQueryWindowCheck
    {
        private readonly WarningCenter _warningCenter;
        private readonly UserPreferencesStore _preferencesStore;
        private int _lastNumber;

        public LongExecutionCheck(WarningCenter warningCenter, UserPreferencesStore preferencesStore)
        {
            _warningCenter = warningCenter;
            _preferencesStore = preferencesStore;
        }

        public void Check(QueryWindow window, QueryWindowEvent windowEvent)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var preferences = _preferencesStore.Preferences;
            var execution = window.LastExecution;
            if (windowEvent != QueryWindowEvent.ExecutionCompleted || !preferences.ShowLongExecutions || execution == null)
                return;

            if (execution.Duration.TotalSeconds < Math.Max(0, preferences.LongExecutionSeconds))
                return;

            _warningCenter.Show(Create(window.Name, execution, preferences, window.Activate, ++_lastNumber));
        }

        /// <summary>One message per execution: several can be shown at once.</summary>
        public static Warning Create(string document, ExecutionInfo execution, UserPreferences preferences, Action activate, int number)
        {
            bool keep = preferences.LongExecutionKeepAfterSeconds > 0 && execution.Duration.TotalSeconds >= preferences.LongExecutionKeepAfterSeconds;
            return new Warning
            {
                Key = "long-execution:" + number,
                Title = "Long execution finished",
                Message = "Document: " + document + Environment.NewLine
                    + "Execution result: " + execution.Result + Environment.NewLine
                    + "Connection: " + execution.Connection + Environment.NewLine
                    + "Started: " + execution.Started.ToString("HH:mm:ss") + " Ended: " + execution.Ended.ToString("HH:mm:ss") + Environment.NewLine
                    + "Duration: " + ExecutionInfo.FormatDuration(execution.Duration),
                Severity = SeverityOf(execution.Result),
                Activate = activate,
                HideAfter = keep || preferences.LongExecutionHideAfterSeconds <= 0 ? (TimeSpan?)null : TimeSpan.FromSeconds(preferences.LongExecutionHideAfterSeconds),
            };
        }

        private static WarningSeverity SeverityOf(string result)
        {
            switch (result)
            {
                case "Success":
                    return WarningSeverity.Info;
                case "Failure":
                case "Timeout":
                    return WarningSeverity.Critical;
                default:
                    return WarningSeverity.Warning;
            }
        }
    }
}
