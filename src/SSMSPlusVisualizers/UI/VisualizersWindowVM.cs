namespace SSMSPlusVisualizers.UI
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Windows.Input;

    using Microsoft.Extensions.Logging;

    using SSMSPlusCore.Ui;

    using SSMSPlusVisualizers.Entities;
    using SSMSPlusVisualizers.Services;

    public class VisualizersWindowVM : ViewModelBase
    {
        private readonly VisualizerStore _store;
        private readonly ILogger<VisualizersWindowVM> _logger;

        public ObservableCollection<VisualizerVM> Visualizers { get; } = new ObservableCollection<VisualizerVM>();

        private VisualizerVM _selected;
        public VisualizerVM Selected
        {
            get => _selected;
            set
            {
                SetField(ref _selected, value);
                RaisePropertyChanged(nameof(HasSelection));
            }
        }

        public bool HasSelection => _selected != null;

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

        public ICommand AddCmd { get; }
        public ICommand DeleteCmd { get; }
        public ICommand MoveUpCmd { get; }
        public ICommand MoveDownCmd { get; }
        public ICommand SaveCmd { get; }

        /// <summary>Set by the view: asks for an application file. Returns null when cancelled.</summary>
        public Func<string, string> BrowseForApplication { get; set; }
        public ICommand BrowseCmd { get; }

        public VisualizersWindowVM(VisualizerStore store, ILogger<VisualizersWindowVM> logger)
        {
            _store = store;
            _logger = logger;
            AddCmd = new Command(Add, null, HandleError);
            DeleteCmd = new Command(Delete, null, HandleError);
            MoveUpCmd = new Command(() => Move(-1), null, HandleError);
            MoveDownCmd = new Command(() => Move(1), null, HandleError);
            SaveCmd = new Command(() => Save(), null, HandleError);
            BrowseCmd = new Command(Browse, null, HandleError);
        }

        public void Load()
        {
            Visualizers.Clear();
            foreach (var visualizer in _store.Visualizers)
                Visualizers.Add(VisualizerVM.From(visualizer));
            Selected = Visualizers.FirstOrDefault();
        }

        private void Add()
        {
            if (Visualizers.Count >= VisualizerStore.MaxVisualizers)
            {
                ShowMessage($"At most {VisualizerStore.MaxVisualizers} visualizers.", true);
                return;
            }

            var visualizer = new VisualizerVM { Name = "New visualizer", Extension = ".txt", Arguments = VisualizerDefinition.DefaultArguments };
            Visualizers.Add(visualizer);
            Selected = visualizer;
        }

        private void Delete()
        {
            if (Selected == null)
                return;

            int index = Visualizers.IndexOf(Selected);
            Visualizers.Remove(Selected);
            Selected = Visualizers.Count == 0 ? null : Visualizers[Math.Min(index, Visualizers.Count - 1)];
        }

        private void Move(int offset)
        {
            if (Selected == null)
                return;

            int index = Visualizers.IndexOf(Selected);
            int target = index + offset;
            if (target >= 0 && target < Visualizers.Count)
                Visualizers.Move(index, target);
        }

        private void Browse()
        {
            if (Selected == null || BrowseForApplication == null)
                return;

            var path = BrowseForApplication(Selected.Application);
            if (path != null)
                Selected.Application = path;
        }

        /// <summary>Returns false when a value is invalid (the message is shown).</summary>
        public bool Save()
        {
            var definitions = Visualizers.Select(v => v.ToDefinition()).ToList();
            var error = Validate(definitions);
            if (error != null)
            {
                ShowMessage(error, true);
                return false;
            }

            _store.Save(definitions);
            ShowMessage("Saved.", false);
            return true;
        }

        public static string Validate(IReadOnlyList<VisualizerDefinition> visualizers)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var visualizer in visualizers)
            {
                if (visualizer.Name.Length == 0)
                    return "Every visualizer needs a name.";

                if (!names.Add(visualizer.Name))
                    return $"The name '{visualizer.Name}' is used twice.";

                if (visualizer.Extension.Length < 2 || visualizer.Extension.Substring(1).IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || visualizer.Extension.IndexOf('.', 1) >= 0)
                    return $"'{visualizer.Name}': the extension must be like .png or .txt.";

                if (visualizer.Application != null && visualizer.Application.IndexOfAny(new[] { '\\', '/' }) >= 0 && !File.Exists(Environment.ExpandEnvironmentVariables(visualizer.Application)))
                    return $"'{visualizer.Name}': the application {visualizer.Application} was not found.";
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
            _logger.LogError(ex, "Visualizers window error");
            ShowMessage(ex.Message, true);
        }
    }
}
