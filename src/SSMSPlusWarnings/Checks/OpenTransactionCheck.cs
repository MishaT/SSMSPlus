namespace SSMSPlusWarnings.Checks
{
    using System;

    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.Shell;

    using SSMSPlusWarnings.Entities;
    using SSMSPlusWarnings.Services;

    /// <summary>Warns while a query window has a transaction left open (@@TRANCOUNT > 0) after an execution.</summary>
    public class OpenTransactionCheck : IQueryWindowCheck
    {
        private readonly WarningCenter _warningCenter;
        private readonly ILogger<OpenTransactionCheck> _logger;

        public OpenTransactionCheck(WarningCenter warningCenter, ILogger<OpenTransactionCheck> logger)
        {
            _warningCenter = warningCenter;
            _logger = logger;
        }

        public void Check(QueryWindow window, QueryWindowEvent windowEvent)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string key = "open-transaction:" + window.Id;

            if (windowEvent == QueryWindowEvent.Closed || !window.IsConnected)
            {
                _warningCenter.Hide(key);
                return;
            }

            bool isOpen;
            try
            {
                isOpen = window.HasOpenTransactions();
            }
            catch (Exception ex)
            {
                // keep the current state: a failed check must not hide a warning
                _logger.LogWarning(ex, "Could not check the open transactions of {Window}", window.Name);
                return;
            }

            if (!isOpen)
            {
                _warningCenter.Hide(key);
                return;
            }

            // a connection change keeps the time of the last execution
            if (windowEvent == QueryWindowEvent.ConnectionChanged && _warningCenter.IsShown(key))
                return;

            _warningCenter.Show(new Warning
            {
                Key = key,
                Title = "ATTENTION: OPEN TRANSACTION(s) in tab: " + window.Name,
                Message = "Last execution ended: " + DateTime.Now.ToString("HH:mm:ss"),
                Severity = WarningSeverity.Warning,
                Activate = window.Activate,
            });
        }
    }
}
