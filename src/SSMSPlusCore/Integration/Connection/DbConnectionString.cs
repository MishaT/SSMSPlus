namespace SSMSPlusCore.Integration.Connection
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Text.RegularExpressions;

    public class DbConnectionString : IEquatable<DbConnectionString>
    {
        public string ConnectionString { get; }
        public string Database { get; }
        public string Server { get; }

        public string DisplayName => Database + " @ " + Server;

        public DbConnectionString(string connectionString, string database)
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(NormalizeToLegacyConnectionString(connectionString));
            if (database != null)
            {
                builder.InitialCatalog = database;
            }

            ConnectionString = builder.ToString();
            Database = builder.InitialCatalog;
            Server = builder.DataSource;
        }

        public DbConnectionString(string connectionString)
            : this(connectionString, null)
        {
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 23 + (Server == null ? 0 : Server.GetHashCode());
            hash = hash * 23 + (Database == null ? 0 : Database.GetHashCode());
            return hash;
        }

        public bool Equals(DbConnectionString other)
        {
            return string.Equals(Server, other.Server, StringComparison.InvariantCultureIgnoreCase)
                &&
                string.Equals(Database, other.Database, StringComparison.InvariantCultureIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is DbConnectionString cnx))
                return false;

            return this.Equals(cnx);
        }

        private static readonly (string @new, string old)[] SqlPropertyRenames = new (string, string)[]
        {
            ("Application Intent", "ApplicationIntent"),
            ("Connect Retry Count", "ConnectRetryCount"),
            ("Connect Retry Interval", "ConnectRetryInterval"),
            ("Pool Blocking Period", "PoolBlockingPeriod"),
            ("Multiple Active Result Sets", "MultipleActiveResultSets"),
            ("Multi Subnet Failover", "MultiSubnetFailover"),
            ("Transparent Network IP Resolution", "TransparentNetworkIPResolution"),
            ("Trust Server Certificate", "TrustServerCertificate"),
            // Microsoft.Data.SqlClient encrypt modes (SSMS 21+) are not understood by System.Data.SqlClient
            (@"Encrypt\s*=\s*(Mandatory|Strict)", "Encrypt=True"),
            (@"Encrypt\s*=\s*Optional", "Encrypt=False")
        };

        // Microsoft.Data.SqlClient only keywords, rejected by System.Data.SqlClient
        private static readonly string UnsupportedSqlProperties =
            @"(?<=^|;)\s*(Host\s*Name\s*In\s*Certificate|Server\s*Certificate|Server\s*SPN|Failover\s*Partner\s*SPN|IP\s*Address\s*Preference)\s*=[^;]*;?";

        public static string NormalizeToLegacyConnectionString(string connectionString)
        {
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                foreach (var replacement in SqlPropertyRenames)
                {
                    connectionString = Regex.Replace(connectionString, replacement.@new, replacement.old, RegexOptions.IgnoreCase);
                }

                connectionString = Regex.Replace(connectionString, UnsupportedSqlProperties, string.Empty, RegexOptions.IgnoreCase);
            }

            return connectionString;
        }
    }
}
