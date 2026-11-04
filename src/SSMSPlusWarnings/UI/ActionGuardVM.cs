namespace SSMSPlusWarnings.UI
{
    using System;

    using Microsoft.Extensions.Logging;

    using SSMSPlusCore.Settings;
    using SSMSPlusCore.Ui;

    public class ActionGuardVM : ViewModelBase
    {
        private readonly UserPreferencesStore _preferencesStore;
        private readonly ILogger<ActionGuardVM> _logger;

        public ActionGuardVM(UserPreferencesStore preferencesStore, ILogger<ActionGuardVM> logger)
        {
            _preferencesStore = preferencesStore;
            _logger = logger;
            _confirmUnsafeStatements = preferencesStore.Preferences.ConfirmUnsafeStatements;
        }

        private bool _confirmUnsafeStatements;
        /// <summary>Ask before executing an UPDATE / DELETE without WHERE or JOIN condition.</summary>
        public bool ConfirmUnsafeStatements
        {
            get => _confirmUnsafeStatements;
            set => SetField(ref _confirmUnsafeStatements, value);
        }

        private string _error;
        public string Error
        {
            get => _error;
            private set => SetField(ref _error, value);
        }

        /// <summary>Returns false when the preferences could not be saved (the error is shown).</summary>
        public bool Save()
        {
            try
            {
                var preferences = _preferencesStore.Preferences.Clone();
                preferences.ConfirmUnsafeStatements = ConfirmUnsafeStatements;
                _preferencesStore.Save(preferences);
                Error = null;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not save the Action Guard settings");
                Error = "Could not save the settings: " + ex.Message;
                return false;
            }
        }
    }
}
