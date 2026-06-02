using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Solidb.Mapping;
using Solidb.Materialization;
using Solidb.Query;
using Solidb.Tracking;

namespace Solidb.Providers
{
    public interface ISolidProvider : IDisposable
    {
        Task<List<T>> QueryAsync<T>(
            EntityMap map,
            SolidModel model,
            QueryState state,
            IdentityMap identityMap) where T : class, new();

        Task<T?> FindAsync<T>(
            EntityMap map,
            object id,
            IdentityMap identityMap) where T : class, new();

        Task<int> CountAsync<T>(EntityMap map, QueryState state) where T : class, new();

        Task<int> SaveChangesAsync(IEnumerable<TrackedEntity> entries, SolidModel model);

        Task CreateTableAsync(EntityMap map);
        Task<bool> TableExistsAsync(string tableName);
        Task EnsureSchemaAsync(SolidModel model);
    }
}
