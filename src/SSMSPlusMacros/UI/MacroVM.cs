namespace SSMSPlusMacros.UI
{
    using System.Collections.ObjectModel;
    using System.Linq;

    using SSMSPlusCore.Ui;

    using SSMSPlusMacros.Entities;

    public class MacroVM : ViewModelBase
    {
        private string _name;
        public string Name
        {
            get => _name;
            set => SetField(ref _name, value);
        }

        private string _shortcut;
        public string Shortcut
        {
            get => _shortcut;
            set => SetField(ref _shortcut, value);
        }

        private bool _isGlobal;
        /// <summary>Shortcut active everywhere, not only in query windows.</summary>
        public bool IsGlobal
        {
            get => _isGlobal;
            set
            {
                SetField(ref _isGlobal, value);
                RaisePropertyChanged(nameof(IsQueryEditor));
            }
        }

        public bool IsQueryEditor
        {
            get => !_isGlobal;
            set => IsGlobal = !value;
        }

        public ObservableCollection<string> Commands { get; } = new ObservableCollection<string>();

        private int _selectedCommandIndex = -1;
        public int SelectedCommandIndex
        {
            get => _selectedCommandIndex;
            set => SetField(ref _selectedCommandIndex, value);
        }

        public static MacroVM From(MacroDefinition definition)
        {
            var vm = new MacroVM
            {
                Name = definition.Name,
                Shortcut = definition.Shortcut,
                IsGlobal = definition.Scope == MacroDefinition.GlobalScope,
            };
            foreach (var command in definition.Commands ?? Enumerable.Empty<string>())
                vm.Commands.Add(command);
            return vm;
        }

        public MacroDefinition ToDefinition()
        {
            return new MacroDefinition
            {
                Name = (Name ?? string.Empty).Trim(),
                Shortcut = string.IsNullOrWhiteSpace(Shortcut) ? null : Shortcut.Trim(),
                Scope = IsGlobal ? MacroDefinition.GlobalScope : MacroDefinition.QueryEditorScope,
                Commands = Commands.Select(c => c.Trim()).Where(c => c.Length > 0).ToList(),
            };
        }
    }
}
