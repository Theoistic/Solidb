using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Solidb.Caching;
using Solidb.Mapping;
using Solidb.Materialization;
using Solidb.Providers;
using Solidb.Tracking;

namespace Solidb.Query
{
    public sealed class SolidQuery<T> where T : class, new()
    {
        private readonly ISolidProvider _provider;
        private readonly EntityMap _map;
        private readonly SolidModel _model;
        private readonly IdentityMap _identityMap;
        private readonly ChangeTracker _tracker;
        private readonly ISolidCache? _cache;
        private readonly QueryState _state = new();

        internal SolidQuery(
            ISolidProvider provider,
            EntityMap map,
            SolidModel model,
            IdentityMap identityMap,
            ChangeTracker tracker,
            ISolidCache? cache = null)
        {
            _provider = provider;
            _map = map;
            _model = model;
            _identityMap = identityMap;
            _tracker = tracker;
            _cache = cache;
        }

        public SolidQuery<T> Where(Expression<Func<T, bool>> predicate)
        {
            _state.WhereExpressions.Add(predicate);
            return this;
        }

        public SolidQuery<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            _state.OrderByExpressions.Add((keySelector, true));
            return this;
        }

        public SolidQuery<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            _state.OrderByExpressions.Add((keySelector, false));
            return this;
        }

        public SolidQuery<T> ThenBy<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            _state.OrderByExpressions.Add((keySelector, true));
            return this;
        }

        public SolidQuery<T> ThenByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            _state.OrderByExpressions.Add((keySelector, false));
            return this;
        }

        public SolidQuery<T> Skip(int count)
        {
            _state.Skip = count;
            return this;
        }

        public SolidQuery<T> Take(int count)
        {
            _state.Take = count;
            return this;
        }

        /// <summary>
        /// Include a related collection via a split query (default for one-to-many).
        /// </summary>
        public SolidQuery<T> Include<TProperty>(Expression<Func<T, TProperty>> navigation)
        {
            _state.IncludeExpressions.Add(navigation);
            return this;
        }

        /// <summary>
        /// Include a single related entity via a LEFT JOIN (many-to-one / one-to-one).
        /// </summary>
        public SolidQuery<T> IncludeJoin<TProperty>(Expression<Func<T, TProperty>> navigation)
        {
            _state.JoinIncludeExpressions.Add(navigation);
            return this;
        }

        public SolidQuery<T> CacheFor(TimeSpan duration)
        {
            _state.CacheDuration = duration;
            return this;
        }

        public async Task<List<T>> ToListAsync()
        {
            if (_cache != null && _state.CacheDuration.HasValue)
            {
                var cacheKey = CacheKeyBuilder.Build(_map, _state);
                if (_cache.TryGet<List<T>>(cacheKey, out var cached) && cached != null)
                    return cached;

                var result = await ExecuteQueryAsync();
                _cache.Set(cacheKey, result, _state.CacheDuration.Value);
                return result;
            }

            return await ExecuteQueryAsync();
        }

        public async Task<T?> FirstOrDefaultAsync()
        {
            _state.Take = 1;
            var results = await ToListAsync();
            return results.FirstOrDefault();
        }

        public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            _state.WhereExpressions.Add(predicate);
            return await FirstOrDefaultAsync();
        }

        public async Task<int> CountAsync() =>
            await _provider.CountAsync<T>(_map, _state);

        public async Task<bool> AnyAsync() =>
            await CountAsync() > 0;

        public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
        {
            _state.WhereExpressions.Add(predicate);
            return await AnyAsync();
        }

        private async Task<List<T>> ExecuteQueryAsync()
        {
            var results = await _provider.QueryAsync<T>(_map, _model, _state, _identityMap);
            foreach (var entity in results)
                _tracker.Track(entity);
            return results;
        }
    }
}
