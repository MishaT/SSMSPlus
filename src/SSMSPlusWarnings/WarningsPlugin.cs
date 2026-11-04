namespace SSMSPlusWarnings
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.Shell;

    using SSMSPlusWarnings.Checks;
    using SSMSPlusWarnings.Guards;
    using SSMSPlusWarnings.Services;

    /// <summary>Runs the query window checks after each execution, connection change and when a window is closed.</summary>
    public class WarningsPlugin
    {
        private readonly QueryWindowMonitor _monitor;
        private readonly ExecutionGuard _guard;
        private readonly SettingsUI _settingsUI;
        private readonly IReadOnlyList<IQueryWindowCheck> _checks;
        private readonly ILogger<WarningsPlugin> _logger;
        private bool _isRegistered;

        public WarningsPlugin(QueryWindowMonitor monitor, ExecutionGuard guard, SettingsUI settingsUI, IEnumerable<IQueryWindowCheck> checks, ILogger<WarningsPlugin> logger)
        {
            _settingsUI = settingsUI;
            _monitor = monitor;
            _guard = guard;
            _checks = checks.ToList();
            _logger = logger;
        }

        public void Register()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_isRegistered)
                throw new Exception("WarningsPlugin is already registred");

            _isRegistered = true;
            _settingsUI.Register();
            _monitor.WindowEvent += OnWindowEvent;

            try
            {
                _monitor.Start();
            }
            catch (Exception ex)
            {
                // SSMS internals changed: the rest of the extension keeps working
                _logger.LogError(ex, "Could not follow the query windows, the warnings are disabled");
            }

            try
            {
                _guard.Start();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not hook the query execution, no confirmation for UPDATE / DELETE without WHERE");
            }
        }

        private void OnWindowEvent(QueryWindow window, QueryWindowEvent windowEvent)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // the next execution already started (a macro for example): its end will run the checks
            if (windowEvent != QueryWindowEvent.Closed && window.IsExecuting)
                return;

            foreach (var check in _checks)
            {
                try
                {
                    check.Check(window, windowEvent);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "The check {Check} failed", check.GetType().Name);
                }
            }
        }
    }
}
