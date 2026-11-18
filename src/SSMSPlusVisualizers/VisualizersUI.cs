namespace SSMSPlusVisualizers
{
    using System;
    using System.ComponentModel.Design;
    using System.Diagnostics;

    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;

    using SSMSPlusCore.Integration;

    using SSMSPlusVisualizers.Services;
    using SSMSPlusVisualizers.UI;

    /// <summary>Results grid context menu > Show As > (visualizers) / Settings..., and SSMS Plus > Settings > Visualizers...</summary>
    public class VisualizersUI
    {
        public const int SettingsCommandId = 3103;
        public const int FirstSlotCommandId = 3301;
        public const int ShowAsSettingsCommandId = 3320;

        private readonly PackageProvider _packageProvider;
        private readonly VisualizerStore _store;
        private readonly VisualizerRunner _runner;
        private readonly ILogger<VisualizersUI> _logger;

        // the menu asks every entry: read the cell once per menu opening
        private GridCell _cell;
        private DateTime _cellReadAt;

        public VisualizersUI(PackageProvider packageProvider, VisualizerStore store, VisualizerRunner runner, ILogger<VisualizersUI> logger)
        {
            _packageProvider = packageProvider;
            _store = store;
            _runner = runner;
            _logger = logger;
        }

        public void Register()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var commands = _packageProvider.CommandService;
            commands.AddCommand(new MenuCommand((s, e) => ShowSettings(), new CommandID(MenuHelper.CommandSet, SettingsCommandId)));

            var showAsSettings = new OleMenuCommand((s, e) => ShowSettings(), new CommandID(MenuHelper.CommandSet, ShowAsSettingsCommandId));
            showAsSettings.BeforeQueryStatus += (s, e) => ((OleMenuCommand)s).Visible = CurrentCell() != null;
            commands.AddCommand(showAsSettings);

            for (int slot = 0; slot < VisualizerStore.MaxVisualizers; slot++)
            {
                int index = slot;
                var command = new OleMenuCommand((s, e) => RunSlot(index), new CommandID(MenuHelper.CommandSet, FirstSlotCommandId + slot));
                command.BeforeQueryStatus += (s, e) => UpdateSlot((OleMenuCommand)s, index);
                commands.AddCommand(command);
            }

            _runner.DeleteOldFiles();
        }

        private GridCell CurrentCell()
        {
            if (DateTime.Now - _cellReadAt > TimeSpan.FromMilliseconds(500))
            {
                _cellReadAt = DateTime.Now;
                try
                {
                    _cell = GridCellReader.ReadActive();
                }
                catch (Exception ex)
                {
                    _cell = null;
                    _logger.LogWarning(ex, "Could not read the results grid cell");
                }
            }

            return _cell;
        }

        private void UpdateSlot(OleMenuCommand command, int slot)
        {
            var visualizers = _store.Visualizers;
            command.Visible = slot < visualizers.Count && CurrentCell() != null;
            command.Enabled = command.Visible;
            if (command.Visible)
                command.Text = visualizers[slot].Name;
        }

        private void RunSlot(int slot)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var visualizers = _store.Visualizers;
            if (slot >= visualizers.Count)
                return;

            var visualizer = visualizers[slot];
            try
            {
                _cellReadAt = DateTime.MinValue;
                var cell = CurrentCell();
                if (cell == null)
                    return;

                _runner.Run(visualizer, cell);
                if (cell.MayBeTruncated)
                {
                    SetStatus($"Show As: SSMS stored only the first {cell.StoredLimit:N0} {(cell.Value is byte[] ? "bytes" : "characters")} of the value. "
                        + "Raise Tools > Options > Query Results > SQL Server > Results to Grid > Non XML data (up to 10485760) and run the query again.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Show As {Name} failed", visualizer.Name);
                VsShellUtilities.ShowMessageBox(ServiceProvider.GlobalProvider, $"Show As {visualizer.Name}: {ex.Message}", "SSMS Plus",
                    OLEMSGICON.OLEMSGICON_WARNING, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }

        private static void SetStatus(string text)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var statusBar = (IVsStatusbar)Package.GetGlobalService(typeof(SVsStatusbar));
            statusBar?.SetText(text);
        }

        private void ShowSettings()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            new VisualizersWindow().ShowModal();
        }
    }
}
