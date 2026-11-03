using System;
using System.Data.Common;
using System.IO;
using System.Runtime.InteropServices;

using Dapper;

using Microsoft.Data.Sqlite;

using SSMSPlusCore.App;
using SSMSPlusCore.Dab.Dapper;

namespace SSMSPlusCore.Database
{
    public class SqliteConnectionFactory
    {
        static SqliteConnectionFactory()
        {
            PreloadNativeSqlite();
            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());
            SqlMapper.AddTypeHandler(new NullableLongHandler());
        }

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        // The extension folder is not on the native dll search path of the SSMS process,
        // so load e_sqlite3 from <extension dir>\x64 (or x86) before the provider P/Invokes into it.
        private static void PreloadNativeSqlite()
        {
            var extensionDir = Path.GetDirectoryName(typeof(SqliteConnectionFactory).Assembly.Location);
            var nativePath = Path.Combine(extensionDir, Environment.Is64BitProcess ? "x64" : "x86", "e_sqlite3.dll");
            if (File.Exists(nativePath))
            {
                LoadLibrary(nativePath);
            }
        }

        private IWorkingDirProvider _workingDirProvider;
        public SqliteConnectionFactory(IWorkingDirProvider workingDirProvider)
        {
            _workingDirProvider = workingDirProvider;
        }

        public DbConnection GetConnection()
        {
            return new SqliteConnection("Filename=" + Path.Combine(_workingDirProvider.GetWorkingDir(), "SSMS-Plus.sqlite"));
        }
    }
}
