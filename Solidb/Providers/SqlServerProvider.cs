using Microsoft.Data.SqlClient;

namespace Solidb.Providers
{
    public sealed class SqlServerProvider : SqlProviderBase
    {
        public SqlServerProvider(string connectionString)
            : base(new SqlConnection(connectionString), SqlDialect.SqlServer)
        {
        }
    }
}
