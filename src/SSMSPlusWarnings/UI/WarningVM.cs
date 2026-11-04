namespace SSMSPlusWarnings.UI
{
    using System.Windows.Input;

    using SSMSPlusCore.Ui;

    using SSMSPlusWarnings.Entities;
    using SSMSPlusWarnings.Services;

    public class WarningVM : ViewModelBase
    {
        private Warning _warning;

        public WarningVM(Warning warning, WarningCenter center)
        {
            _warning = warning;
            ActivateCmd = new Command(() => center.Activate(this));
            CopyCmd = new Command(() => center.Copy(this));
            CloseCmd = new Command(() => center.Hide(Warning.Key));
        }

        public Warning Warning => _warning;

        public string Title => _warning.Title;

        public string Message => _warning.Message;

        public bool HasMessage => !string.IsNullOrEmpty(_warning.Message);

        public WarningSeverity Severity => _warning.Severity;

        public bool CanActivate => _warning.Activate != null;

        public ICommand ActivateCmd { get; }

        public ICommand CopyCmd { get; }

        /// <summary>Hides the warning until it is raised again (for the open transactions: at the next execution).</summary>
        public ICommand CloseCmd { get; }

        public void Update(Warning warning)
        {
            _warning = warning;
            RaisePropertyChanged(nameof(Title));
            RaisePropertyChanged(nameof(Message));
            RaisePropertyChanged(nameof(HasMessage));
            RaisePropertyChanged(nameof(Severity));
            RaisePropertyChanged(nameof(CanActivate));
        }
    }
}
