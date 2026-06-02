using System;
using System.Threading.Tasks;
using Solidb.Caching;
using Solidb.Mapping;
using Solidb.Materialization;
using Solidb.Providers;
using Solidb.Query;
using Solidb.Tracking;

namespace Solidb
{
    public sealed class SolidSet<T> where T : class, new()
    {
        private readonly ISolidProvider _provider;
        private readonly EntityMap _map;
        private readonly SolidModel _model;
        private readonly ChangeTracker _tracker;
        private readonly IdentityMap _identityMap;
        private readonly ISolidCache? _cache;

        internal SolidSet(
            ISolidProvider provider,
            EntityMap map,
            SolidModel model,
            ChangeTracker tracker,
            IdentityMap identityMap,
            ISolidCache? cache = null)
        {
            _provider = provider;
            _map = map;
            _model = model;
            _tracker = tracker;
            _identityMap = identityMap;
            _cache = cache;
        }

        public SolidQuery<T> Where(System.Linq.Expressions.Expression<Func<T, bool>> predicate) =>
            NewQuery().Where(predicate);

        public SolidQuery<T> OrderBy<TKey>(System.Linq.Expressions.Expression<Func<T, TKey>> keySelector) =>
            NewQuery().OrderBy(keySelector);

        public SolidQuery<T> Include<TProperty>(
            System.Linq.Expressions.Expression<Func<T, TProperty>> navigation) =>
            NewQuery().Include(navigation);

        public SolidQuery<T> IncludeJoin<TProperty>(
            System.Linq.Expressions.Expression<Func<T, TProperty>> navigation) =>
            NewQuery().IncludeJoin(navigation);

        public SolidQuery<T> AsQuery() => NewQuery();

        public void Add(T entity) => _tracker.Track(entity, EntityState.Added);

        public void Remove(T entity) => _tracker.Track(entity, EntityState.Deleted);

        public void Update(T entity) => _tracker.Track(entity, EntityState.Modified);

        public Task<T?> FindAsync(object id) => _provider.FindAsync<T>(_map, id, _identityMap);

        private SolidQuery<T> NewQuery() =>
            new(_provider, _map, _model, _identityMap, _tracker, _cache);
    }
}
