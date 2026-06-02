using Microsoft.Data.Sqlite;

namespace Solidb.Providers
{
    public sealed class SQLiteProvider : SqlProviderBase
    {
        public SQLiteProvider(string connectionString)
            : base(new SqliteConnection(connectionString), SqlDialect.SQLite)
        {
        }
    }
}
