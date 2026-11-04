using SSMSPlusCore.App;
using SSMSPlusCore.Settings;
using SSMSPlusCore.Ui;
using System;
using System.Diagnostics;

namespace SSMSPlusPreferences.UI
{
    public class PreferencesWindowVM : ViewModelBase
    {
        public const string ForkUrl = "https://github.com/MishaT/SSMSPlus";
        public const string OriginalUrl = "https://github.com/akarzazi/SSMSPlus";

        private IVersionProvider _versionProvider;
        private IWorkingDirProvider _workingDirProvider;
        public DistributionSettings DistributionSettings { get; private set; }

        public Command OpenWorkingDirCmd { get; private set; }
        public Command OpenContributeCmd { get; private set; }
        public Command OpenForkCmd { get; private set; }
        public Command OpenOriginalCmd { get; private set; }

        public PreferencesWindowVM(IVersionProvider versionProvider, IWorkingDirProvider workingDirProvider, DistributionSettings distributionSettings)
        {
            _versionProvider = versionProvider;
            _workingDirProvider = workingDirProvider;
            DistributionSettings = distributionSettings;
            OpenWorkingDirCmd = new Command(OnOpenWorkingDir, null, HandleError);
            OpenContributeCmd = new Command(() => Process.Start(DistributionSettings.ContributeUrl), null, HandleError);
            OpenForkCmd = new Command(() => Process.Start(ForkUrl), null, HandleError);
            OpenOriginalCmd = new Command(() => Process.Start(OriginalUrl), null, HandleError);
            InitDefaults();
        }

        private void OnOpenWorkingDir()
        {
            Process.Start(_workingDirProvider.GetWorkingDir());
        }

        private void HandleError(Exception ex)
        {
            // nothing for now
        }

        private void InitDefaults()
        {
            Version = string.Join(".", _versionProvider.GetBuildAndRevision());
            WorkingDirPath = _workingDirProvider.GetWorkingDir();
        }

        public string Version { get; private set; }
        public string WorkingDirPath { get; private set; }
    }
}
