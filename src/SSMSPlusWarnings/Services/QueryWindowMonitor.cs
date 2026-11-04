namespace SSMSPlusWarnings.Services
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using System.Windows.Threading;

    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;

    public enum QueryWindowEvent
    {
        /// <summary>An execution of the window ended (completed, failed or cancelled).</summary>
        ExecutionCompleted,

        /// <summary>The window was connected to another server or disconnected.</summary>
        ConnectionChanged,

        Closed,
    }

    /// <summary>
    /// Follows the SSMS query windows: they are picked up when activated (and at start for the open ones).
    /// The events are raised on the UI thread, once SSMS is done with its own processing.
    /// </summary>
    public class QueryWindowMonitor : IVsSelectionEvents
    {
        private static readonly MethodInfo OnEventMethod = typeof(Subscription).GetMethod(nameof(Subscription.OnEvent));

        private readonly ILogger<QueryWindowMonitor> _logger;
        private readonly Dictionary<object, QueryWindow> _windows = new Dictionary<object, QueryWindow>();
        private Dispatcher _dispatcher;
        private int _lastId;
        private uint _selectionEventsCookie;
        private bool _hookFailureLogged;

        public event Action<QueryWindow, QueryWindowEvent> WindowEvent;

        public QueryWindowMonitor(ILogger<QueryWindowMonitor> logger)
        {
            _logger = logger;
        }

        public IReadOnlyCollection<QueryWindow> Windows => _windows.Values;

        public void Start()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _dispatcher = Dispatcher.CurrentDispatcher;

            var monitorSelection = (IVsMonitorSelection)Package.GetGlobalService(typeof(SVsShellMonitorSelection));
            ErrorHandler.ThrowOnFailure(monitorSelection.AdviseSelectionEvents(this, out _selectionEventsCookie));

            // windows already open (restored at start); not yet loaded tabs are picked up when activated
            var shell = (IVsUIShell)Package.GetGlobalService(typeof(SVsUIShell));
            if (ErrorHandler.Succeeded(shell.GetDocumentWindowEnum(out IEnumWindowFrames frames)) && frames != null)
            {
                var frame = new IVsWindowFrame[1];
                while (frames.Next(1, frame, out uint fetched) == VSConstants.S_OK && fetched == 1)
                    Track(frame[0]);
            }

            if (ErrorHandler.Succeeded(monitorSelection.GetCurrentElementValue((uint)VSConstants.VSSELELEMID.SEID_DocumentFrame, out object active)))
                Track(active as IVsWindowFrame);
        }

        private void Track(IVsWindowFrame frame)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (frame == null)
                return;

            try
            {
                if (ErrorHandler.Failed(frame.GetProperty((int)__VSFPROPID.VSFPROPID_DocView, out object view)) || view == null)
                    return;

                if (_windows.ContainsKey(view) || !QueryWindow.IsQueryEditor(view))
                    return;

                var window = new QueryWindow(++_lastId, frame, view);
                Subscribe(window);
                _windows.Add(view, window);
            }
            catch (Exception ex)
            {
                if (!_hookFailureLogged)
                {
                    _hookFailureLogged = true;
                    _logger.LogError(ex, "Could not follow a query window, the warnings won't work for it");
                }
            }
        }

        private void Subscribe(QueryWindow window)
        {
            var handler = window.GetQueryExecutionHandler() ?? throw new InvalidOperationException("The query window has no execution handler");
            AddHandler(handler, "ScriptExecutionCompleted", new Subscription(this, window, QueryWindowEvent.ExecutionCompleted));
            AddHandler(window.Editor, "ConnectionChanged", new Subscription(this, window, QueryWindowEvent.ConnectionChanged));
            AddHandler(window.Editor, "ConnectionDisconnected", new Subscription(this, window, QueryWindowEvent.ConnectionChanged));

            if (window.Editor is IComponent component)
            {
                component.Disposed += (s, e) =>
                {
                    _windows.Remove(window.Editor);
                    Raise(window, QueryWindowEvent.Closed);
                };
            }
        }

        /// <summary>The SSMS event types are internal: bind a (object, EventArgs) handler to them.</summary>
        private static void AddHandler(object target, string eventName, Subscription subscription)
        {
            var eventInfo = target.GetType().GetEvent(eventName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? target.GetType().GetInterfaces().Select(i => i.GetEvent(eventName)).FirstOrDefault(e => e != null)
                ?? throw new MissingMemberException(target.GetType().FullName, eventName);

            eventInfo.AddEventHandler(target, Delegate.CreateDelegate(eventInfo.EventHandlerType, subscription, OnEventMethod));
        }

        private void Raise(QueryWindow window, QueryWindowEvent windowEvent)
        {
            // let SSMS finish its own handling of the execution first (status bar, current database...)
            _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                try
                {
                    WindowEvent?.Invoke(window, windowEvent);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while handling {Event} of a query window", windowEvent);
                }
            }));
        }

        public int OnSelectionChanged(IVsHierarchy pHierOld, uint itemidOld, IVsMultiItemSelect pMISOld, ISelectionContainer pSCOld, IVsHierarchy pHierNew, uint itemidNew, IVsMultiItemSelect pMISNew, ISelectionContainer pSCNew)
        {
            return VSConstants.S_OK;
        }

        public int OnElementValueChanged(uint elementid, object varValueOld, object varValueNew)
        {
            if (elementid == (uint)VSConstants.VSSELELEMID.SEID_DocumentFrame)
                Track(varValueNew as IVsWindowFrame);

            return VSConstants.S_OK;
        }

        public int OnCmdUIContextChanged(uint dwCmdUICookie, int fActive)
        {
            return VSConstants.S_OK;
        }

        /// <summary>The target of the delegates bound to the SSMS events.</summary>
        private sealed class Subscription
        {
            private readonly QueryWindowMonitor _monitor;
            private readonly QueryWindow _window;
            private readonly QueryWindowEvent _event;

            public Subscription(QueryWindowMonitor monitor, QueryWindow window, QueryWindowEvent windowEvent)
            {
                _monitor = monitor;
                _window = window;
                _event = windowEvent;
            }

            public void OnEvent(object sender, EventArgs e)
            {
                if (_event == QueryWindowEvent.ExecutionCompleted)
                {
                    // now: the checks run later, when SSMS may already have started another execution
                    try
                    {
                        _window.RecordExecutionEnd(e);
                    }
                    catch (Exception ex)
                    {
                        _monitor._logger.LogWarning(ex, "Could not read the execution information");
                    }
                }

                _monitor.Raise(_window, _event);
            }
        }
    }
}
