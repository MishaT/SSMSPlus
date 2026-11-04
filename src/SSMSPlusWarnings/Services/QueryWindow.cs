namespace SSMSPlusWarnings.Services
{
    using System;
    using System.Data;
    using System.Data.Common;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.ExceptionServices;

    using Microsoft.VisualStudio;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;

    /// <summary>
    /// A SSMS query window: its tab and its editor control (SqlScriptEditorControl).
    /// The editor members are internal to SSMS and read by reflection.
    /// </summary>
    public class QueryWindow
    {
        public const string EditorTypeName = "Microsoft.SqlServer.Management.UI.VSIntegration.Editors.SqlScriptEditorControl";
        private const string TransactionTrackerTypeName = EditorTypeName + "+TransactionTracker";

        private readonly IVsWindowFrame _frame;

        public QueryWindow(int id, IVsWindowFrame frame, object editor)
        {
            Id = id;
            _frame = frame;
            Editor = editor;
        }

        /// <summary>Unique while SSMS runs.</summary>
        public int Id { get; }

        public object Editor { get; }

        public static bool IsQueryEditor(object view)
        {
            for (var type = view?.GetType(); type != null; type = type.BaseType)
            {
                if (type.FullName == EditorTypeName)
                    return true;
            }

            return false;
        }

        /// <summary>The tab name, for example "SQLQuery13.sql".</summary>
        public string Name
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                try
                {
                    if (ErrorHandler.Succeeded(_frame.GetProperty((int)__VSFPROPID.VSFPROPID_pszMkDocument, out object moniker)) && moniker is string path && path.Length > 0)
                        return Path.GetFileName(path);

                    if (ErrorHandler.Succeeded(_frame.GetProperty((int)__VSFPROPID.VSFPROPID_Caption, out object caption)) && caption is string text)
                        return text;
                }
                catch (Exception)
                {
                    // closed window
                }

                return "query window";
            }
        }

        /// <summary>The live connection of the window (the one the query runs on), null when not connected.</summary>
        public IDbConnection Connection => GetField("m_connection") as IDbConnection;

        public bool IsConnected => Connection?.State == ConnectionState.Open;

        public bool IsExecuting => GetProperty("IsExecuting") as bool? ?? false;

        /// <summary>False for servers without transactions (Azure Synapse pools).</summary>
        public bool AreTransactionsSupported => GetField("areTransactionsSupported") as bool? ?? true;

        /// <summary>The last finished execution, recorded when it ended. Null before the first one.</summary>
        public ExecutionInfo LastExecution { get; private set; }

        /// <summary>Called when an execution ended, before SSMS starts another one.</summary>
        public void RecordExecutionEnd(EventArgs completedArgs)
        {
            var ended = DateTime.Now;
            // the start time of SSMS (the status bar timer)
            var started = GetField("m_dtExecBegan") as DateTime? ?? default(DateTime);
            if (started == default(DateTime) || started > ended)
                started = ended;

            LastExecution = new ExecutionInfo
            {
                Started = started,
                Ended = ended,
                Result = ReadResult(completedArgs),
                Connection = DescribeConnection(Connection),
            };
        }

        /// <summary>"Success", "Failure", "Cancelled", "Timeout" or "Halted" (ScriptExecutionCompletedEventArgs.ExecutionResult).</summary>
        private static string ReadResult(EventArgs completedArgs)
        {
            var result = completedArgs?.GetType().GetProperty("ExecutionResult")?.GetValue(completedArgs)?.ToString();
            return result == "Cancel" ? "Cancelled" : result ?? "Unknown";
        }

        /// <summary>"server.database (WinAuth)", "server.database (SQL: user)" or "server.database (ActiveDirectoryInteractive: user)".</summary>
        public static string DescribeConnection(IDbConnection connection)
        {
            if (connection == null)
                return "not connected";

            var builder = new DbConnectionStringBuilder();
            try
            {
                builder.ConnectionString = connection.ConnectionString;
            }
            catch (ArgumentException)
            {
                // not a key=value connection string
            }

            string Value(params string[] keys) => keys.Where(builder.ContainsKey).Select(k => Convert.ToString(builder[k])).FirstOrDefault(v => !string.IsNullOrEmpty(v));

            string server = Value("Data Source", "Server", "Address") ?? "?";
            string database = SafeDatabase(connection) ?? Value("Initial Catalog", "Database");
            string user = Value("User ID", "UID", "User");
            string authentication = Value("Authentication");
            bool integrated = string.Equals(Value("Integrated Security", "Trusted_Connection"), "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Value("Integrated Security"), "SSPI", StringComparison.OrdinalIgnoreCase);

            string auth;
            if (integrated)
                auth = "WinAuth";
            else if (!string.IsNullOrEmpty(authentication) && !authentication.Equals("SqlPassword", StringComparison.OrdinalIgnoreCase))
                auth = authentication + (user != null ? ": " + user : string.Empty);
            else
                auth = "SQL" + (user != null ? ": " + user : string.Empty);

            return server + (string.IsNullOrEmpty(database) ? string.Empty : "." + database) + " (" + auth + ")";
        }

        private static string SafeDatabase(IDbConnection connection)
        {
            try
            {
                return connection.State == ConnectionState.Open ? connection.Database : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The object raising ScriptExecutionCompleted (DisplaySQLResultsControl).</summary>
        public object GetQueryExecutionHandler()
        {
            var method = FindMethod(Editor.GetType(), "GetQueryExecutionHandler");
            return method?.Invoke(Editor, null);
        }

        /// <summary>
        /// Runs "select @@trancount" on the window connection, like SSMS does when the window is closed.
        /// Must run on the UI thread while the window is not executing, so that it can't overlap an execution.
        /// </summary>
        public bool HasOpenTransactions()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var connection = Connection;
            if (connection?.State != ConnectionState.Open || IsExecuting || !AreTransactionsSupported)
                return false;

            // SSMS' own check also handles a session left with SET SHOWPLAN ON
            var trackerType = Editor.GetType().Assembly.GetType(TransactionTrackerTypeName);
            var runQuery = trackerType?.GetMethod("RunTransactionQuery", new[] { typeof(IDbConnection) });
            var isActive = trackerType?.GetProperty("AreTransactionsActive");
            if (runQuery != null && isActive != null)
            {
                var tracker = Activator.CreateInstance(trackerType);
                try
                {
                    runQuery.Invoke(tracker, new object[] { connection });
                }
                catch (TargetInvocationException ex) when (ex.InnerException != null)
                {
                    ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                    throw;
                }

                return (bool)isActive.GetValue(tracker);
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "select @@trancount";
                command.CommandTimeout = 15;
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        /// <summary>Shows the tab.</summary>
        public void Activate()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _frame.Show();
        }

        private object GetField(string name)
        {
            for (var type = Editor.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                    return field.GetValue(Editor);
            }

            return null;
        }

        private object GetProperty(string name)
        {
            for (var type = Editor.GetType(); type != null; type = type.BaseType)
            {
                var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (property != null)
                    return property.GetValue(Editor);
            }

            return null;
        }

        private static MethodInfo FindMethod(Type type, string name)
        {
            for (; type != null; type = type.BaseType)
            {
                var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                if (method != null)
                    return method;
            }

            return null;
        }
    }
}
