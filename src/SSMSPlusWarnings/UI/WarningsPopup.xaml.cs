namespace SSMSPlusWarnings.UI
{
    using System;
    using System.Collections.Specialized;
    using System.Windows;
    using System.Windows.Interop;
    using System.Windows.Threading;

    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;

    using SSMSPlusWarnings.Services;

    /// <summary>
    /// The warnings at the top right corner of SSMS. Owned by the SSMS main window: it stays above it,
    /// follows it and is hidden with it, but not above the other applications.
    /// </summary>
    public partial class WarningsPopup : Window
    {
        private IntPtr _ownerHandle;
        private Window _ownerWindow;
        private DispatcherTimer _followTimer;

        public WarningsPopup(WarningCenter center)
        {
            InitializeComponent();
            DataContext = center;

            // a new message is added at the end: keep it visible when the list scrolls
            center.Warnings.CollectionChanged += Warnings_CollectionChanged;
            Closed += (s, e) => center.Warnings.CollectionChanged -= Warnings_CollectionChanged;
        }

        private void Warnings_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => Scroller.ScrollToEnd()));
        }

        public void ShowNextToMainWindow()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _ownerWindow = Application.Current?.MainWindow;
            _ownerHandle = _ownerWindow != null ? new WindowInteropHelper(_ownerWindow).Handle : IntPtr.Zero;
            if (_ownerHandle == IntPtr.Zero)
            {
                _ownerWindow = null;
                var shell = (IVsUIShell)Package.GetGlobalService(typeof(SVsUIShell));
                shell?.GetDialogOwnerHwnd(out _ownerHandle);
            }

            new WindowInteropHelper(this).Owner = _ownerHandle;

            // off screen until placed, to not flash at the default position
            Left = -32000;
            Top = -32000;
            SourceInitialized += (s, e) => PopupPlacement.MakeNonActivating(new WindowInteropHelper(this).Handle);
            SizeChanged += (s, e) => Place();
            Show();

            if (_ownerWindow != null)
            {
                _ownerWindow.LocationChanged += Owner_Changed;
                _ownerWindow.SizeChanged += Owner_Changed;
                _ownerWindow.StateChanged += Owner_Changed;
            }
            else
            {
                _followTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (s, e) => Place(), Dispatcher);
                _followTimer.Start();
            }

            Place();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_ownerWindow != null)
            {
                _ownerWindow.LocationChanged -= Owner_Changed;
                _ownerWindow.SizeChanged -= Owner_Changed;
                _ownerWindow.StateChanged -= Owner_Changed;
            }

            _followTimer?.Stop();
            base.OnClosed(e);
        }

        private void Owner_Changed(object sender, EventArgs e)
        {
            // the window state change is applied after the event
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(Place));
        }

        private void Place()
        {
            var available = PopupPlacement.Place(new WindowInteropHelper(this).Handle, _ownerHandle);
            // many messages: scroll instead of going below the SSMS window
            if (available.HasValue)
            {
                double maxHeight = Math.Max(120, available.Value);
                if (Math.Abs(Scroller.MaxHeight - maxHeight) > 1)
                    Scroller.MaxHeight = maxHeight;
            }
        }
    }
}
