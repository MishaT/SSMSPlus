namespace SSMSPlusMacros
{
    using System;
    using System.ComponentModel.Design;

    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.Shell;

    using SSMSPlusCore.Integration;

    using SSMSPlusMacros.Services;
    using SSMSPlusMacros.UI;

    public class MacrosUI
    {
        public const int MacrosCommandId = 3001;

        private bool isRegistred = false;

        private readonly PackageProvider _packageProvider;
        private readonly MacroStore _store;
        private readonly MacroRunner _runner;
        private readonly MacroKeyBinder _keyBinder;
        private readonly ILogger<MacrosUI> _logger;

        public MacrosUI(PackageProvider packageProvider, MacroStore store, MacroRunner runner, MacroKeyBinder keyBinder, ILogger<MacrosUI> logger)
        {
            _packageProvider = packageProvider;
            _store = store;
            _runner = runner;
            _keyBinder = keyBinder;
            _logger = logger;
        }

        public void Register()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (isRegistred)
            {
                throw new Exception("MacrosUI is already registred");
            }

            isRegistred = true;

            _packageProvider.CommandService.AddCommand(new MenuCommand(this.ExecuteFromMenu, new CommandID(MenuHelper.CommandSet, MacrosCommandId)));

            for (int slot = 0; slot < MacroStore.MaxMacros; slot++)
            {
                int slotIndex = slot;
                var command = new OleMenuCommand((s, e) => RunSlot(slotIndex), new CommandID(MenuHelper.CommandSet, MacroKeyBinder.FirstSlotCommandId + slot));
                command.BeforeQueryStatus += (s, e) => UpdateSlot((OleMenuCommand)s, slotIndex);
                _packageProvider.CommandService.AddCommand(command);
            }

            _store.Load();
            try
            {
                _keyBinder.ApplySlotBindings(_store.Settings.Macros);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not assign the macro shortcuts");
            }
        }

        private void UpdateSlot(OleMenuCommand command, int slot)
        {
            var macros = _store.Settings.Macros;
            command.Visible = slot < macros.Count;
            command.Enabled = command.Visible;
            if (command.Visible)
                command.Text = macros[slot].Name;
        }

        private void RunSlot(int slot)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var macros = _store.Settings.Macros;
            if (slot < macros.Count)
                _runner.Run(macros[slot]);
        }

        private void ExecuteFromMenu(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            // DialogWindow: modal on the SSMS main window, keys go to the dialog instead of the SSMS shortcuts
            new MacrosWindow().ShowModal();
        }
    }
}
