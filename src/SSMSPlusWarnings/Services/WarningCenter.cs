namespace SSMSPlusWarnings.Services
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Windows;
    using System.Windows.Threading;

    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.Shell;

    using SSMSPlusWarnings.Entities;
    using SSMSPlusWarnings.UI;

    /// <summary>
    /// The warnings shown in the popup at the top right corner of SSMS. The popup is shown while there is a warning.
    /// UI thread only.
    /// </summary>
    public class WarningCenter
    {
        private readonly ILogger<WarningCenter> _logger;
        private WarningsPopup _popup;
        private readonly Dictionary<string, DispatcherTimer> _hideTimers = new Dictionary<string, DispatcherTimer>();

        public ObservableCollection<WarningVM> Warnings { get; } = new ObservableCollection<WarningVM>();

        public WarningCenter(ILogger<WarningCenter> logger)
        {
            _logger = logger;
        }

        public bool IsShown(string key)
        {
            return Find(key) != null;
        }

        /// <summary>Shows the warning, or updates the visible one with the same key.</summary>
        public void Show(Warning warning)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var existing = Find(warning.Key);
            if (existing != null)
                existing.Update(warning);
            else
                Warnings.Add(new WarningVM(warning, this));

            StartHideTimer(warning);
            UpdatePopup();
        }

        public void Hide(string key)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            StopHideTimer(key);
            var existing = Find(key);
            if (existing == null)
                return;

            Warnings.Remove(existing);
            UpdatePopup();
        }

        private void StartHideTimer(Warning warning)
        {
            StopHideTimer(warning.Key);
            if (warning.HideAfter == null)
                return;

            var timer = new DispatcherTimer { Interval = warning.HideAfter.Value <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(1) : warning.HideAfter.Value };
            timer.Tick += (s, e) =>
            {
                // not while it is being read
                if (_popup?.IsMouseOver == true)
                {
                    timer.Interval = TimeSpan.FromSeconds(3);
                    return;
                }

                Hide(warning.Key);
            };
            _hideTimers[warning.Key] = timer;
            timer.Start();
        }

        private void StopHideTimer(string key)
        {
            if (_hideTimers.TryGetValue(key, out var timer))
            {
                timer.Stop();
                _hideTimers.Remove(key);
            }
        }

        internal void Activate(WarningVM warning)
        {
            try
            {
                warning.Warning.Activate?.Invoke();
            }
            catch (Exception ex)
            {
                // for example the window was closed meanwhile
                _logger.LogWarning(ex, "Could not activate the warning {Key}", warning.Warning.Key);
                Hide(warning.Warning.Key);
            }
        }

        internal void Copy(WarningVM warning)
        {
            try
            {
                Clipboard.SetText(warning.Warning.ToClipboardText());
            }
            catch (Exception ex)
            {
                // the clipboard can be locked by another application
                _logger.LogWarning(ex, "Could not copy the warning");
            }
        }

        private WarningVM Find(string key)
        {
            return Warnings.FirstOrDefault(w => w.Warning.Key == key);
        }

        private void UpdatePopup()
        {
            try
            {
                if (Warnings.Count > 0)
                {
                    if (_popup == null)
                    {
                        _popup = new WarningsPopup(this);
                        _popup.Closed += (s, e) => _popup = null;
                        _popup.ShowNextToMainWindow();
                    }
                }
                else if (_popup != null)
                {
                    _popup.Close();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not show the warnings popup");
            }
        }
    }
}
