using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Solidb.Mapping;
using Solidb.Materialization;
using Solidb.Query;
using Solidb.Tracking;

namespace Solidb.Providers
{
    public sealed class JsonProvider : ISolidProvider
    {
        private readonly string _basePath;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public JsonProvider(string basePath)
        {
            _basePath = basePath;
            Directory.CreateDirectory(basePath);
        }

        public Task<List<T>> QueryAsync<T>(
            EntityMap map,
            SolidModel model,
            QueryState state,
            IdentityMap identityMap) where T : class, new()
        {
            var all = LoadAll<T>(map);

            // Apply WHERE via compiled lambdas
            IEnumerable<T> query = all;
            foreach (var expr in state.WhereExpressions)
            {
                var predicate = ((Expression<Func<T, bool>>)expr).Compile();
                query = query.Where(predicate);
            }

            // Apply ORDER BY
            IOrderedEnumerable<T>? ordered = null;
            foreach (var (expr, ascending) in state.OrderByExpressions)
            {
                if (expr.Body is not MemberExpression member) continue;
                var prop = typeof(T).GetProperty(member.Member.Name);
                if (prop == null) continue;

                Func<T, object?> keySelector = x => prop.GetValue(x);

                if (ordered == null)
                    ordered = ascending ? query.OrderBy(keySelector) : query.OrderByDescending(keySelector);
                else
                    ordered = ascending ? ordered.ThenBy(keySelector) : ordered.ThenByDescending(keySelector);
            }
            if (ordered != null) query = ordered;

            // Apply Skip/Take
            if (state.Skip.HasValue) query = query.Skip(state.Skip.Value);
            if (state.Take.HasValue) query = query.Take(state.Take.Value);

            var results = query.ToList();

            // Register in identity map
            var keyProp = typeof(T).GetProperty(map.Key.PropertyName);
            foreach (var entity in results)
            {
                var key = keyProp?.GetValue(entity);
                if (key != null) identityMap.Set<T>(key, entity);
            }

            // Resolve includes in memory
            foreach (var includeExpr in state.IncludeExpressions)
            {
                var navName = IncludeBuilder.GetNavigationName(includeExpr);
                var relation = map.Relations.FirstOrDefault(r => r.NavigationName == navName);
                if (relation == null || !model.HasEntity(relation.DependentType)) continue;

                var relatedMap = model.GetEntity(relation.DependentType);
                var related = LoadAllUntyped(relatedMap);
                ResolveInclude(results, related, relation);
            }

            return Task.FromResult(results);
        }

        public Task<T?> FindAsync<T>(EntityMap map, object id, IdentityMap identityMap)
            where T : class, new()
        {
            if (identityMap.TryGet<T>(id, out var cached))
                return Task.FromResult(cached);

            var all = LoadAll<T>(map);
            var keyProp = typeof(T).GetProperty(map.Key.PropertyName);
            var entity = all.FirstOrDefault(e => Equals(keyProp?.GetValue(e)?.ToString(), id.ToString()));

            if (entity != null) identityMap.Set<T>(id, entity);
            return Task.FromResult(entity);
        }

        public Task<int> CountAsync<T>(EntityMap map, QueryState state) where T : class, new()
        {
            var all = LoadAll<T>(map);
            IEnumerable<T> query = all;
            foreach (var expr in state.WhereExpressions)
            {
                var predicate = ((Expression<Func<T, bool>>)expr).Compile();
                query = query.Where(predicate);
            }
            return Task.FromResult(query.Count());
        }

        public Task<int> SaveChangesAsync(IEnumerable<TrackedEntity> entries, SolidModel model)
        {
            // Group changes by entity type
            var grouped = entries
                .GroupBy(e => e.Entity.GetType())
                .ToList();

            var affected = 0;
            foreach (var group in grouped)
            {
                var map = model.GetEntity(group.Key);
                var all = LoadAllUntyped(map).ToList();
                var keyProp = group.Key.GetProperty(map.Key.PropertyName)!;

                foreach (var entry in group)
                {
                    var entityKey = keyProp.GetValue(entry.Entity)?.ToString();

                    switch (entry.State)
                    {
                        case EntityState.Added:
                            all.Add(entry.Entity);
                            affected++;
                            break;
                        case EntityState.Modified:
                            var idx = all.FindIndex(e => keyProp.GetValue(e)?.ToString() == entityKey);
                            if (idx >= 0) { all[idx] = entry.Entity; affected++; }
                            break;
                        case EntityState.Deleted:
                            var removed = all.RemoveAll(e => keyProp.GetValue(e)?.ToString() == entityKey);
                            affected += removed;
                            break;
                    }
                }

                SaveAll(map, all);
            }
            return Task.FromResult(affected);
        }

        public Task CreateTableAsync(EntityMap map)
        {
            var path = GetFilePath(map.TableName);
            if (!File.Exists(path))
                File.WriteAllText(path, "[]");
            return Task.CompletedTask;
        }

        public Task<bool> TableExistsAsync(string tableName) =>
            Task.FromResult(File.Exists(GetFilePath(tableName)));

        public Task EnsureSchemaAsync(SolidModel model)
        {
            foreach (var map in model.All())
            {
                var path = GetFilePath(map.TableName);
                if (!File.Exists(path))
                    File.WriteAllText(path, "[]");
            }
            return Task.CompletedTask;
        }

        public void Dispose() { }

        // ── Helpers ──────────────────────────────────────────────────────────

        private List<T> LoadAll<T>(EntityMap map) where T : class, new()
        {
            var path = GetFilePath(map.TableName);
            if (!File.Exists(path)) return new List<T>();
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<T>>(json, _jsonOptions) ?? new List<T>();
        }

        private List<object> LoadAllUntyped(EntityMap map)
        {
            var path = GetFilePath(map.TableName);
            if (!File.Exists(path)) return new List<object>();

            var json = File.ReadAllText(path);
            var listType = typeof(List<>).MakeGenericType(map.ClrType);
            var deserialized = JsonSerializer.Deserialize(json, listType, _jsonOptions);
            if (deserialized is not System.Collections.IList list) return new List<object>();

            return list.Cast<object>().ToList();
        }

        private void SaveAll(EntityMap map, List<object> entities)
        {
            var path = GetFilePath(map.TableName);
            var listType = typeof(List<>).MakeGenericType(map.ClrType);
            var typedList = (System.Collections.IList)Activator.CreateInstance(listType)!;
            foreach (var e in entities) typedList.Add(e);
            var json = JsonSerializer.Serialize(typedList, _jsonOptions);
            File.WriteAllText(path, json);
        }

        private static void ResolveInclude(
            System.Collections.IList parents,
            List<object> related,
            RelationMap relation)
        {
            if (relation.Kind == RelationKind.OneToMany)
                RelationFixer.FixOneToMany(parents, (System.Collections.IList)related, relation);
            else
                RelationFixer.FixManyToOne(parents, (System.Collections.IList)related, relation);
        }

        private string GetFilePath(string tableName) =>
            Path.Combine(_basePath, $"{tableName}.json");
    }
}
