using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Solidb.Diagnostics;
using Solidb.Mapping;
using Solidb.Materialization;
using Solidb.Query;
using Solidb.Schema;
using Solidb.Tracking;

namespace Solidb.Providers
{
    public abstract class SqlProviderBase : ISolidProvider
    {
        protected readonly DbConnection _connection;
        protected readonly SqlDialect _dialect;
        private ISolidLogger? _logger;

        protected SqlProviderBase(DbConnection connection, SqlDialect dialect)
        {
            _connection = connection;
            _dialect = dialect;
        }

        public void UseLogger(ISolidLogger logger) => _logger = logger;

        // ── Query ────────────────────────────────────────────────────────────

        public async Task<List<T>> QueryAsync<T>(
            EntityMap map,
            SolidModel model,
            QueryState state,
            IdentityMap identityMap) where T : class, new()
        {
            await EnsureOpenAsync();

            var results = new List<T>();

            // Execute the main SELECT (with any join-includes baked in)
            var cmdBuilder = new SqlCommandBuilder(map, state);
            var sql = cmdBuilder.BuildSelect();

            _logger?.Log(new SolidCommandLog(sql, state.Parameters));

            await using var cmd = CreateCommand(sql, state.Parameters);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var keyValue = Materialization.Materializer.ReadKey(reader, map);
                if (keyValue == null) continue;

                var entity = identityMap.GetOrAdd<T>(keyValue, () =>
                    Materialization.Materializer.Materialize<T>(reader, map));

                if (!results.Contains(entity))
                    results.Add(entity);
            }

            // Resolve split-query includes
            foreach (var includeExpr in state.IncludeExpressions)
            {
                var navName = IncludeBuilder.GetNavigationName(includeExpr);
                var relation = map.Relations.FirstOrDefault(r => r.NavigationName == navName);
                if (relation == null) continue;

                if (!model.HasEntity(relation.DependentType)) continue;
                var relatedMap = model.GetEntity(relation.DependentType);

                await LoadSplitIncludeAsync(results, relation, relatedMap, state, identityMap);
            }

            // Resolve join-includes (many-to-one / one-to-one)
            foreach (var includeExpr in state.JoinIncludeExpressions)
            {
                var navName = IncludeBuilder.GetNavigationName(includeExpr);
                var relation = map.Relations.FirstOrDefault(r => r.NavigationName == navName);
                if (relation == null) continue;

                if (!model.HasEntity(relation.DependentType)) continue;
                var relatedMap = model.GetEntity(relation.DependentType);

                await LoadJoinIncludeAsync(results, map, relation, relatedMap, state, identityMap);
            }

            return results;
        }

        private async Task LoadSplitIncludeAsync<T>(
            List<T> parents,
            RelationMap relation,
            EntityMap relatedMap,
            QueryState originalState,
            IdentityMap identityMap) where T : class
        {
            if (parents.Count == 0) return;

            var fkPropOnDependent = relation.Kind == RelationKind.OneToMany
                ? relation.ForeignKeyName
                : relatedMap.Key.PropertyName;

            // Collect the FK values (parent PKs for one-to-many)
            var pkProp = relation.PrincipalType
                .GetProperties()
                .FirstOrDefault(p => p.GetCustomAttributes(typeof(KeyAttribute), false).Length > 0
                    || string.Equals(p.Name, "Id", StringComparison.OrdinalIgnoreCase));
            if (pkProp == null) return;

            var pkValues = parents
                .Select(p => pkProp.GetValue(p)?.ToString())
                .Where(v => v != null)
                .Distinct()
                .ToList();

            if (pkValues.Count == 0) return;

            var splitState = new QueryState();
            var paramNames = pkValues.Select((_, i) => splitState.NextParam()).ToList();
            for (var i = 0; i < pkValues.Count; i++)
                splitState.Parameters[paramNames[i]] = pkValues[i];

            var splitBuilder = new SqlCommandBuilder(relatedMap, splitState);
            var sql = splitBuilder.BuildSelectIn(fkPropOnDependent, paramNames);

            _logger?.Log(new SolidCommandLog(sql, splitState.Parameters));

            var related = new List<object>();
            await using var cmd = CreateCommand(sql, splitState.Parameters);
            await using var reader = await cmd.ExecuteReaderAsync();

            var materializeMethod = typeof(Materialization.Materializer)
                .GetMethod(nameof(Materialization.Materializer.Materialize))!
                .MakeGenericMethod(relatedMap.ClrType);

            while (await reader.ReadAsync())
            {
                var keyValue = Materialization.Materializer.ReadKey(reader, relatedMap);
                if (keyValue == null) continue;

                var entity = identityMap.GetOrAdd(keyValue, relatedMap.ClrType, () =>
                    materializeMethod.Invoke(null, new object?[] { reader, relatedMap, null })!);

                related.Add(entity);
            }

            if (relation.Kind == RelationKind.OneToMany)
                RelationFixer.FixOneToMany((IList)parents, (IList)related, relation);
            else
                RelationFixer.FixManyToOne((IList)parents, (IList)related, relation);
        }

        private async Task LoadJoinIncludeAsync<T>(
            List<T> results,
            EntityMap mainMap,
            RelationMap relation,
            EntityMap joinMap,
            QueryState originalState,
            IdentityMap identityMap) where T : class
        {
            // Re-execute with a LEFT JOIN for the related entity
            var joinState = new QueryState();
            foreach (var w in originalState.WhereExpressions) joinState.WhereExpressions.Add(w);
            foreach (var o in originalState.OrderByExpressions) joinState.OrderByExpressions.Add(o);
            joinState.Skip = originalState.Skip;
            joinState.Take = originalState.Take;
            foreach (var kv in originalState.Parameters) joinState.Parameters[kv.Key] = kv.Value;

            const string mainAlias = "t0";
            const string joinAlias = "t1";

            var cmdBuilder = new SqlCommandBuilder(mainMap, joinState);
            var sql = cmdBuilder.BuildSelectWithJoin(joinMap, relation, mainAlias, joinAlias);

            _logger?.Log(new SolidCommandLog(sql, joinState.Parameters));

            var materializeRelated = typeof(Materialization.Materializer)
                .GetMethod(nameof(Materialization.Materializer.Materialize))!
                .MakeGenericMethod(joinMap.ClrType);

            var navProp = mainMap.ClrType.GetProperty(relation.NavigationName);

            await using var cmd = CreateCommand(sql, joinState.Parameters);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var mainKey = Materialization.Materializer.ReadKey(reader, mainMap, mainAlias);
                if (mainKey == null) continue;

                if (!identityMap.TryGetByType(mainMap.ClrType, mainKey, out var mainObj) || mainObj is not T main)
                    continue;

                var joinKey = Materialization.Materializer.ReadKey(reader, joinMap, joinAlias);
                if (joinKey == null) continue;

                var related = identityMap.GetOrAdd(joinKey, joinMap.ClrType, () =>
                    materializeRelated.Invoke(null, new object?[] { reader, joinMap, joinAlias })!);

                navProp?.SetValue(main, related);
            }
        }

        public async Task<T?> FindAsync<T>(EntityMap map, object id, IdentityMap identityMap)
            where T : class, new()
        {
            if (identityMap.TryGet<T>(id, out var cached))
                return cached;

            await EnsureOpenAsync();

            var state = new QueryState();
            var paramName = state.NextParam();
            state.Parameters[paramName] = NormalizeId(id);

            var cols = string.Join(", ", map.Properties.Select(p => p.ColumnName));
            var limitClause = _dialect == SqlDialect.SQLite
                ? $"WHERE {map.Key.ColumnName} = @{paramName} LIMIT 1"
                : $"WHERE {map.Key.ColumnName} = @{paramName}";
            var selectTop = _dialect == SqlDialect.SqlServer ? "TOP 1 " : "";

            var sql = $"SELECT {selectTop}{cols} FROM {map.TableName} {limitClause}";

            _logger?.Log(new SolidCommandLog(sql, state.Parameters));

            await using var cmd = CreateCommand(sql, state.Parameters);
            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync()) return null;

            var entity = Materialization.Materializer.Materialize<T>(reader, map);
            identityMap.Set<T>(id, entity);
            return entity;
        }

        public async Task<int> CountAsync<T>(EntityMap map, QueryState state) where T : class, new()
        {
            await EnsureOpenAsync();
            var cmdBuilder = new SqlCommandBuilder(map, state);
            var sql = cmdBuilder.BuildCount();
            _logger?.Log(new SolidCommandLog(sql, state.Parameters));
            await using var cmd = CreateCommand(sql, state.Parameters);
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        // ── Persistence ──────────────────────────────────────────────────────

        public async Task<int> SaveChangesAsync(IEnumerable<TrackedEntity> entries, SolidModel model)
        {
            await EnsureOpenAsync();
            var entriesList = entries.ToList();
            if (entriesList.Count == 0) return 0;

            await using var tx = await _connection.BeginTransactionAsync();
            var affected = 0;
            try
            {
                foreach (var entry in entriesList)
                {
                    var map = model.GetEntity(entry.Entity.GetType());
                    affected += entry.State switch
                    {
                        EntityState.Added => await ExecuteInsertAsync(entry.Entity, map, tx),
                        EntityState.Modified => await ExecuteUpdateAsync(entry, map, tx),
                        EntityState.Deleted => await ExecuteDeleteAsync(entry.Entity, map, tx),
                        _ => 0
                    };
                }
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
            return affected;
        }

        private async Task<int> ExecuteInsertAsync(object entity, EntityMap map, DbTransaction tx)
        {
            var state = new QueryState();
            var cmdBuilder = new SqlCommandBuilder(map, state);
            var sql = cmdBuilder.BuildInsert();

            var parameters = map.Properties
                .Where(p => !p.IsGenerated)
                .ToDictionary(p => p.PropertyName, p => NormalizeParam(p.PropertyInfo.GetValue(entity)));

            _logger?.Log(new SolidCommandLog(sql, parameters!));
            await using var cmd = CreateCommand(sql, parameters!, tx);
            return await cmd.ExecuteNonQueryAsync();
        }

        private async Task<int> ExecuteUpdateAsync(TrackedEntity entry, EntityMap map, DbTransaction tx)
        {
            var entity = entry.Entity;
            var changed = map.Properties
                .Where(p => !p.IsKey && HasChanged(p, entity, entry.OriginalValues))
                .Select(p => p.PropertyName)
                .ToList();

            if (changed.Count == 0) return 0;

            var state = new QueryState();
            var cmdBuilder = new SqlCommandBuilder(map, state);
            var sql = cmdBuilder.BuildUpdate(changed);

            var parameters = map.Properties
                .Where(p => p.IsKey || changed.Contains(p.PropertyName))
                .ToDictionary(p => p.PropertyName, p => NormalizeParam(p.PropertyInfo.GetValue(entity)));

            _logger?.Log(new SolidCommandLog(sql, parameters!));
            await using var cmd = CreateCommand(sql, parameters!, tx);
            return await cmd.ExecuteNonQueryAsync();
        }

        private async Task<int> ExecuteDeleteAsync(object entity, EntityMap map, DbTransaction tx)
        {
            var state = new QueryState();
            var cmdBuilder = new SqlCommandBuilder(map, state);
            var sql = cmdBuilder.BuildDelete();
            var parameters = new Dictionary<string, object?>
            {
                [map.Key.PropertyName] = NormalizeParam(map.Key.PropertyInfo.GetValue(entity))
            };
            _logger?.Log(new SolidCommandLog(sql, parameters));
            await using var cmd = CreateCommand(sql, parameters, tx);
            return await cmd.ExecuteNonQueryAsync();
        }

        // ── Schema ───────────────────────────────────────────────────────────

        public async Task CreateTableAsync(EntityMap map)
        {
            await EnsureOpenAsync();
            var schema = new Schema.SchemaBuilder(_dialect);
            var sql = schema.BuildCreateTable(map);
            _logger?.Log(new SolidCommandLog(sql, new Dictionary<string, object?>()));
            await using var cmd = CreateCommand(sql, new Dictionary<string, object?>());
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<bool> TableExistsAsync(string tableName)
        {
            await EnsureOpenAsync();
            var safeTableName = tableName.Replace("'", "''");
            var sql = _dialect == SqlDialect.SQLite
                ? "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@tableName"
                : "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME=@tableName";
            var parameters = new Dictionary<string, object?> { ["tableName"] = safeTableName };
            await using var cmd = CreateCommand(sql, parameters);
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }

        public async Task EnsureSchemaAsync(SolidModel model)
        {
            foreach (var entityMap in model.All())
            {
                if (!await TableExistsAsync(entityMap.TableName))
                    await CreateTableAsync(entityMap);
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        protected async Task EnsureOpenAsync()
        {
            if (_connection.State != ConnectionState.Open)
                await _connection.OpenAsync();
        }

        private DbCommand CreateCommand(string sql, IDictionary<string, object?> parameters, DbTransaction? tx = null)
        {
            var cmd = _connection.CreateCommand();
            cmd.CommandText = sql;
            if (tx != null) cmd.Transaction = tx;
            foreach (var kv in parameters)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = kv.Key;
                p.Value = kv.Value ?? DBNull.Value;
                cmd.Parameters.Add(p);
            }
            return cmd;
        }

        private static bool HasChanged(PropertyMap prop, object entity, IReadOnlyDictionary<string, object?> original)
        {
            var current = prop.PropertyInfo.GetValue(entity);
            original.TryGetValue(prop.PropertyName, out var orig);
            return !Equals(current, orig);
        }

        private static object? NormalizeParam(object? value) => value switch
        {
            bool b => b ? 1 : 0,
            Guid g => g.ToString(),
            _ => value
        };

        private static object? NormalizeId(object id) =>
            id is Guid g ? g.ToString() : id;

        public void Dispose() => _connection.Dispose();
    }
}
