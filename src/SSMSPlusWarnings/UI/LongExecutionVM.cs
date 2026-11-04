namespace SSMSPlusWarnings.UI
{
    using System;

    using Microsoft.Extensions.Logging;

    using SSMSPlusCore.Settings;
    using SSMSPlusCore.Ui;

    public class LongExecutionVM : ViewModelBase
    {
        private readonly UserPreferencesStore _preferencesStore;
        private readonly ILogger<LongExecutionVM> _logger;

        public LongExecutionVM(UserPreferencesStore preferencesStore, ILogger<LongExecutionVM> logger)
        {
            _preferencesStore = preferencesStore;
            _logger = logger;
            var preferences = preferencesStore.Preferences;
            _isEnabled = preferences.ShowLongExecutions;
            _thresholdSeconds = preferences.LongExecutionSeconds.ToString();
            _hideAfterSeconds = preferences.LongExecutionHideAfterSeconds.ToString();
            _keepAfterSeconds = preferences.LongExecutionKeepAfterSeconds.ToString();
        }

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetField(ref _isEnabled, value);
        }

        private string _thresholdSeconds;
        public string ThresholdSeconds
        {
            get => _thresholdSeconds;
            set => SetField(ref _thresholdSeconds, value);
        }

        private string _hideAfterSeconds;
        public string HideAfterSeconds
        {
            get => _hideAfterSeconds;
            set => SetField(ref _hideAfterSeconds, value);
        }

        private string _keepAfterSeconds;
        public string KeepAfterSeconds
        {
            get => _keepAfterSeconds;
            set => SetField(ref _keepAfterSeconds, value);
        }

        private string _error;
        public string Error
        {
            get => _error;
            private set => SetField(ref _error, value);
        }

        /// <summary>Returns false when a value is invalid or the preferences could not be saved (the error is shown).</summary>
        public bool Save()
        {
            if (!TryParse(ThresholdSeconds, 1, "The threshold", out int threshold)
                || !TryParse(HideAfterSeconds, 0, "The time to hide the message", out int hideAfter)
                || !TryParse(KeepAfterSeconds, 0, "The execution length to keep the message", out int keepAfter))
                return false;

            try
            {
                var preferences = _preferencesStore.Preferences.Clone();
                preferences.ShowLongExecutions = IsEnabled;
                preferences.LongExecutionSeconds = threshold;
                preferences.LongExecutionHideAfterSeconds = hideAfter;
                preferences.LongExecutionKeepAfterSeconds = keepAfter;
                _preferencesStore.Save(preferences);
                Error = null;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not save the long execution settings");
                Error = "Could not save the settings: " + ex.Message;
                return false;
            }
        }

        private bool TryParse(string text, int min, string name, out int value)
        {
            if (int.TryParse((text ?? string.Empty).Trim(), out value) && value >= min)
                return true;

            Error = $"{name} must be a number of seconds, {min} or more.";
            return false;
        }
    }
}
