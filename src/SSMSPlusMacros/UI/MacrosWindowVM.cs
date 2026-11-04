namespace SSMSPlusMacros.UI
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Windows.Input;

    using Microsoft.Extensions.Logging;

    using SSMSPlusCore.Ui;

    using SSMSPlusMacros.Entities;
    using SSMSPlusMacros.Services;

    public class MacrosWindowVM : ViewModelBase
    {
        private readonly MacroStore _store;
        private readonly MacroKeyBinder _keyBinder;
        private readonly ILogger<MacrosWindowVM> _logger;
        private HashSet<string> _knownCommands;

        public ObservableCollection<MacroVM> Macros { get; } = new ObservableCollection<MacroVM>();

        private MacroVM _selectedMacro;
        public MacroVM SelectedMacro
        {
            get => _selectedMacro;
            set
            {
                SetField(ref _selectedMacro, value);
                RaisePropertyChanged(nameof(HasSelection));
            }
        }

        public bool HasSelection => _selectedMacro != null;

        private IReadOnlyList<string> _availableCommands = new string[0];
        /// <summary>All SSMS command names, for the command picker.</summary>
        public IReadOnlyList<string> AvailableCommands
        {
            get => _availableCommands;
            private set => SetField(ref _availableCommands, value);
        }

        private string _commandSearch;
        public string CommandSearch
        {
            get => _commandSearch;
            set
            {
                SetField(ref _commandSearch, value);
                UpdateMatchingCommands();
            }
        }

        private IReadOnlyList<string> _matchingCommands = new string[0];
        /// <summary>SSMS commands matching <see cref="CommandSearch"/>: exact name first, then names starting with it.</summary>
        public IReadOnlyList<string> MatchingCommands
        {
            get => _matchingCommands;
            private set => SetField(ref _matchingCommands, value);
        }

        private string _selectedMatchingCommand;
        public string SelectedMatchingCommand
        {
            get => _selectedMatchingCommand;
            set => SetField(ref _selectedMatchingCommand, value);
        }

        private string _message;
        public string Message
        {
            get => _message;
            private set => SetField(ref _message, value);
        }

        private bool _isError;
        public bool IsError
        {
            get => _isError;
            private set => SetField(ref _isError, value);
        }

        public ICommand AddMacroCmd { get; }
        public ICommand DeleteMacroCmd { get; }
        public ICommand AddCommandCmd { get; }
        public ICommand RemoveCommandCmd { get; }
        public ICommand MoveUpCmd { get; }
        public ICommand MoveDownCmd { get; }
        public ICommand ClearShortcutCmd { get; }
        public ICommand SaveCmd { get; }

        public MacrosWindowVM(MacroStore store, MacroKeyBinder keyBinder, ILogger<MacrosWindowVM> logger)
        {
            _store = store;
            _keyBinder = keyBinder;
            _logger = logger;

            AddMacroCmd = new Command(AddMacro, null, HandleError);
            DeleteMacroCmd = new Command(DeleteMacro, null, HandleError);
            AddCommandCmd = new Command(AddCommand, null, HandleError);
            RemoveCommandCmd = new Command(RemoveCommand, null, HandleError);
            MoveUpCmd = new Command(() => MoveCommand(-1), null, HandleError);
            MoveDownCmd = new Command(() => MoveCommand(1), null, HandleError);
            ClearShortcutCmd = new Command(() => { if (SelectedMacro != null) SelectedMacro.Shortcut = null; }, null, HandleError);
            SaveCmd = new Command(Save, null, HandleError);
        }

        /// <summary>Reads the saved macros and the SSMS command list. Main thread.</summary>
        public void Load()
        {
            Macros.Clear();
            foreach (var macro in _store.Settings.Macros)
                Macros.Add(MacroVM.From(macro));
            SelectedMacro = Macros.FirstOrDefault();

            try
            {
                AvailableCommands = _keyBinder.GetCommandNames();
                _knownCommands = new HashSet<string>(AvailableCommands, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                // the macros can still be edited, command names are just not checked
                _logger.LogError(ex, "Could not read the SSMS commands");
                ShowMessage("Could not read the SSMS command list, command names are not checked.", true);
            }

            UpdateMatchingCommands();
        }

        private void UpdateMatchingCommands()
        {
            var search = (CommandSearch ?? string.Empty).Trim();
            IEnumerable<string> matches = AvailableCommands;
            if (search.Length > 0)
            {
                matches = matches
                    .Where(c => c.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(c => string.Equals(c, search, StringComparison.OrdinalIgnoreCase) ? 0 : c.StartsWith(search, StringComparison.OrdinalIgnoreCase) ? 1 : 2)
                    .ThenBy(c => c, StringComparer.OrdinalIgnoreCase);
            }

            MatchingCommands = matches.Take(200).ToList();
            // Enter adds the best match
            SelectedMatchingCommand = search.Length > 0 ? MatchingCommands.FirstOrDefault() : null;
        }

        private void AddMacro()
        {
            if (Macros.Count >= MacroStore.MaxMacros)
            {
                ShowMessage($"At most {MacroStore.MaxMacros} macros.", true);
                return;
            }

            var macro = new MacroVM { Name = "New macro " + (Macros.Count + 1) };
            Macros.Add(macro);
            SelectedMacro = macro;
        }

        private void DeleteMacro()
        {
            if (SelectedMacro == null)
                return;

            int index = Macros.IndexOf(SelectedMacro);
            Macros.Remove(SelectedMacro);
            SelectedMacro = Macros.Count == 0 ? null : Macros[Math.Min(index, Macros.Count - 1)];
        }

        private void AddCommand()
        {
            // the selected match, or the typed text when the command list could not be read
            var command = (SelectedMatchingCommand ?? CommandSearch ?? string.Empty).Trim();
            if (SelectedMacro == null || command.Length == 0)
                return;

            SelectedMacro.Commands.Add(command);
            SelectedMacro.SelectedCommandIndex = SelectedMacro.Commands.Count - 1;
        }

        private void RemoveCommand()
        {
            var macro = SelectedMacro;
            if (macro == null || macro.SelectedCommandIndex < 0)
                return;

            int index = macro.SelectedCommandIndex;
            macro.Commands.RemoveAt(index);
            macro.SelectedCommandIndex = Math.Min(index, macro.Commands.Count - 1);
        }

        private void MoveCommand(int offset)
        {
            var macro = SelectedMacro;
            if (macro == null || macro.SelectedCommandIndex < 0)
                return;

            int index = macro.SelectedCommandIndex;
            int target = index + offset;
            if (target < 0 || target >= macro.Commands.Count)
                return;

            macro.Commands.Move(index, target);
            macro.SelectedCommandIndex = target;
        }

        private void Save()
        {
            var macros = Macros.Select(m => m.ToDefinition()).ToList();
            var error = Validate(macros);
            if (error != null)
            {
                ShowMessage(error, true);
                return;
            }

            var settings = new MacroSettings
            {
                Macros = macros,
                DisplacedBindings = _store.Settings.DisplacedBindings.ToList(),
            };

            IReadOnlyList<string> changes;
            using (new WaitCursor())
            {
                changes = _keyBinder.Apply(settings);
                _store.Save(settings);
            }

            ShowMessage("Saved." + (changes.Count == 0 ? string.Empty : Environment.NewLine + string.Join(Environment.NewLine, changes)), false);
        }

        private string Validate(IReadOnlyList<MacroDefinition> macros)
        {
            var shortcuts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var macro in macros)
            {
                if (macro.Name.Length == 0)
                    return "Every macro needs a name.";

                if (macro.Commands.Count == 0)
                    return $"Macro '{macro.Name}' has no command.";

                foreach (var command in macro.Commands)
                {
                    var commandName = command.Split(' ')[0];
                    if (_knownCommands != null && !_knownCommands.Contains(commandName))
                        return $"Macro '{macro.Name}': unknown command '{commandName}'.";
                }

                if (macro.Shortcut != null)
                {
                    if (Shortcut.NeedsCtrlOrAlt(macro.Shortcut))
                        return $"Macro '{macro.Name}': {macro.Shortcut} would stop that key from typing; add Ctrl or Alt.";

                    var key = macro.Scope + "|" + string.Join("+", macro.Shortcut.Split('+').Select(p => p.Trim()).OrderBy(p => p));
                    if (!shortcuts.Add(key))
                        return $"Macro '{macro.Name}': {macro.Shortcut} is already used by another macro.";
                }
            }

            return null;
        }

        private void ShowMessage(string message, bool isError)
        {
            IsError = isError;
            Message = message;
        }

        private void HandleError(Exception ex)
        {
            _logger.LogError(ex, "Macros window error");
            ShowMessage(ex.Message, true);
        }

        private sealed class WaitCursor : IDisposable
        {
            private readonly Cursor _previous = Mouse.OverrideCursor;

            public WaitCursor()
            {
                Mouse.OverrideCursor = Cursors.Wait;
            }

            public void Dispose()
            {
                Mouse.OverrideCursor = _previous;
            }
        }
    }
}
