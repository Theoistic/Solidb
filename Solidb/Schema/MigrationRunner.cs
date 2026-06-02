using System.Threading.Tasks;
using Solidb.Mapping;
using Solidb.Providers;

namespace Solidb.Schema
{
    public sealed class MigrationRunner
    {
        private readonly ISolidProvider _provider;
        private readonly SolidModel _model;

        public MigrationRunner(ISolidProvider provider, SolidModel model)
        {
            _provider = provider;
            _model = model;
        }

        /// <summary>Creates any missing tables. Does not modify or drop existing tables.</summary>
        public async Task MigrateAsync()
        {
            foreach (var map in _model.All())
            {
                if (!await _provider.TableExistsAsync(map.TableName))
                    await _provider.CreateTableAsync(map);
            }
        }

        /// <summary>Creates all tables (CREATE TABLE IF NOT EXISTS).</summary>
        public Task CreateAllAsync() => _provider.EnsureSchemaAsync(_model);

        /// <summary>Creates a single table for <typeparamref name="T"/>.</summary>
        public Task CreateTableAsync<T>() where T : class =>
            _provider.CreateTableAsync(_model.GetEntity<T>());
    }
}
