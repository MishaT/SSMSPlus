namespace SSMSPlusHistory.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    using EnvDTE;

    using Microsoft.Extensions.Logging;
    using Microsoft.SqlServer.Management.Smo.RegSvrEnum;
    using Microsoft.SqlServer.Management.UI.VSIntegration;
    using Microsoft.VisualStudio.Shell;

    using SSMSPlusCore.Integration;

    using SSMSPlusHistory.Entities;
    using SSMSPlusHistory.Repositories;

    using Task = System.Threading.Tasks.Task;

    public class QueryTracker
    {
        private ILogger<QueryTracker> _logger;
        private QueryItemRepository _queryItemRepository;
        private PackageProvider _packageProvider;

        private bool isTracking = false;
        private ConcurrentQueue<QueryItem> itemsQueue = new ConcurrentQueue<QueryItem>();

        public CommandEvents QueryExecuteEvent { get; private set; }

        public QueryTracker(PackageProvider packageProvider, ILogger<QueryTracker> logger, QueryItemRepository queryItemRepository)
        {
            _logger = logger;
            _queryItemRepository = queryItemRepository;
            _packageProvider = packageProvider;
        }

        public void StartTraking()
        {
            if (isTracking)
            {
                throw new Exception("HistoryPlugin is already registred");
            }

            isTracking = true;
            var dte2 = _packageProvider.Dte2;

            var command = dte2.Commands.Item("Query.Execute");

            QueryExecuteEvent = dte2.Events.get_CommandEvents(command.Guid, command.ID);

            // SSMS 22 redirects EnvDTE 8.0 to Microsoft.VisualStudio.Interop 18.0, whose event delegates have a different constructor
            // signature than the EnvDTE 8.0 assembly we compile against: create the delegate from the runtime type instead of "+=".
            var beforeExecute = typeof(_dispCommandEvents_Event).GetEvent(nameof(_dispCommandEvents_Event.BeforeExecute));
            beforeExecute.AddEventHandler(QueryExecuteEvent, Delegate.CreateDelegate(beforeExecute.EventHandlerType, this, nameof(CommandEvents_BeforeExecute)));
        }

        private void CommandEvents_BeforeExecute(string Guid, int ID, object CustomIn, object CustomOut, ref bool CancelDefault)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // cancelled before (UPDATE / DELETE without WHERE confirmation): not executed
            if (CancelDefault)
                return;

            try
            {
                string queryText = GetQueryText();

                if (string.IsNullOrWhiteSpace(queryText))
                    return;

                // Get Current Connection Information
                UIConnectionInfo connInfo = ServiceCache.ScriptFactory.CurrentlyActiveWndConnectionInfo?.UIConnectionInfo;
                if (connInfo == null)
                {
                    _logger.LogWarning("Query execution without connection information, skipping history entry");
                    return;
                }

                var queryItem = new QueryItem()
                {
                    Query = queryText,
                    Server = connInfo.ServerName,
                    Username = connInfo.UserName,
                    Database = connInfo.AdvancedOptions["DATABASE"],
                    ExecutionDateUtc = DateTime.UtcNow
                };

                _logger.LogInformation("Enqueued {@quetyItem}", queryItem.Query);

                itemsQueue.Enqueue(queryItem);

                _ = SaveSoonAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error on BeforeExecute tracking");
            }
        }

        private string GetQueryText()
        {
            Document document = _packageProvider.Dte2.ActiveDocument;
            if (document == null)
                return null;


            var textDocument = (TextDocument)document.Object("TextDocument");
            string queryText = textDocument.Selection.Text;

            if (string.IsNullOrEmpty(queryText))
            {
                EditPoint startPoint = textDocument.StartPoint.CreateEditPoint();
                queryText = startPoint.GetText(textDocument.EndPoint);
            }

            return queryText;
        }

        private async Task SaveSoonAsync()
        {
            try
            {
                await Task.Delay(1000).ConfigureAwait(false);
                await SavePendingItems(0).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while saving the execution history");
            }
        }

        /// <summary>
        /// A failed save (for example the history database locked by another SSMS instance)
        /// is logged and retried a few times instead of losing the items.
        /// </summary>
        private async Task SavePendingItems(int attempt)
        {
            List<QueryItem> pendingItems = new List<QueryItem>();
            lock (itemsQueue)
            {
                QueryItem queryItem;
                while (itemsQueue.TryDequeue(out queryItem))
                {
                    pendingItems.Add(queryItem);
                }
            }

            if (pendingItems.Count == 0)
                return;

            try
            {
                await _queryItemRepository.Insert(pendingItems).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not save {Count} history item(s) (attempt {Attempt})", pendingItems.Count, attempt + 1);
                if (attempt >= 5)
                    return;

                foreach (var item in pendingItems)
                    itemsQueue.Enqueue(item);

                await Task.Delay(2000 * (attempt + 1)).ConfigureAwait(false);
                await SavePendingItems(attempt + 1).ConfigureAwait(false);
            }
        }
    }
}
