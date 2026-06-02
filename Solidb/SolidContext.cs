using System;
using System.Threading.Tasks;
using Solidb.Caching;
using Solidb.Diagnostics;
using Solidb.Mapping;
using Solidb.Materialization;
using Solidb.Providers;
using Solidb.Schema;
using Solidb.Tracking;

namespace Solidb
{
    public sealed class SolidContext : IDisposable
    {
        private readonly ISolidProvider _provider;
        private readonly SolidModel _model;
        private readonly ChangeTracker _changeTracker;
        private readonly IdentityMap _identityMap;
        private readonly ISolidCache? _cache;
        private bool _disposed;

        public SolidSchema Schema { get; }

        private SolidContext(
            ISolidProvider provider,
            SolidModel model,
            ISolidCache? cache = null)
        {
            _provider = provider;
            _model = model;
            _changeTracker = new ChangeTracker();
            _identityMap = new IdentityMap();
            _cache = cache;
            Schema = new SolidSchema(provider, model);
        }

        // ── Factory methods ───────────────────────────────────────────────────

        public static SolidContext SQLite(string connectionString, params Type[] entityTypes)
        {
            var model = SolidModelBuilder.Build(entityTypes);
            var provider = new SQLiteProvider(connectionString);
            return new SolidContext(provider, model);
        }

        public static SolidContext SqlServer(string connectionString, params Type[] entityTypes)
        {
            var model = SolidModelBuilder.Build(entityTypes);
            var provider = new SqlServerProvider(connectionString);
            return new SolidContext(provider, model);
        }

        public static SolidContext Json(string basePath, params Type[] entityTypes)
        {
            var model = SolidModelBuilder.Build(entityTypes);
            var provider = new JsonProvider(basePath);
            return new SolidContext(provider, model);
        }

        public static SolidContext FromProvider(ISolidProvider provider, params Type[] entityTypes)
        {
            var model = SolidModelBuilder.Build(entityTypes);
            return new SolidContext(provider, model);
        }

        // ── Configuration ─────────────────────────────────────────────────────

        public SolidContext WithLogger(ISolidLogger logger)
        {
            if (_provider is SqlProviderBase sqlProvider)
                sqlProvider.UseLogger(logger);
            return this;
        }

        public SolidContext WithCache(ISolidCache cache) =>
            new(_provider, _model, cache);

        // ── Entity sets ───────────────────────────────────────────────────────

        public SolidSet<T> Set<T>() where T : class, new()
        {
            var map = _model.GetEntity<T>();
            return new SolidSet<T>(_provider, map, _model, _changeTracker, _identityMap, _cache);
        }

        // ── Unit of work ──────────────────────────────────────────────────────

        public async Task<int> SaveChangesAsync()
        {
            _changeTracker.DetectChanges();
            var entries = _changeTracker.Entries();
            var affected = await _provider.SaveChangesAsync(entries, _model);
            _changeTracker.AcceptAllChanges();
            return affected;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _provider.Dispose();
            _disposed = true;
        }
    }

    /// <summary>Exposes schema management operations on a <see cref="SolidContext"/>.</summary>
    public sealed class SolidSchema
    {
        private readonly MigrationRunner _runner;

        internal SolidSchema(ISolidProvider provider, SolidModel model)
        {
            _runner = new MigrationRunner(provider, model);
        }

        public Task CreateAllAsync() => _runner.CreateAllAsync();
        public Task CreateTableAsync<T>() where T : class => _runner.CreateTableAsync<T>();
        public Task MigrateAsync() => _runner.MigrateAsync();
    }
}
