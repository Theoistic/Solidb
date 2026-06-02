using System.Collections.Generic;
using System.Linq;
using System.Text;
using Solidb.Mapping;
using Solidb.Query;

namespace Solidb.Caching
{
    public static class CacheKeyBuilder
    {
        public static string Build(EntityMap map, QueryState state)
        {
            var sb = new StringBuilder();
            sb.Append(map.TableName);
            sb.Append(':');

            foreach (var where in state.WhereExpressions)
                sb.Append(where.ToString()).Append('|');

            foreach (var order in state.OrderByExpressions)
                sb.Append(order.Expression.ToString()).Append(order.Ascending ? "ASC" : "DESC").Append('|');

            if (state.Skip.HasValue) sb.Append($"skip={state.Skip.Value}|");
            if (state.Take.HasValue) sb.Append($"take={state.Take.Value}|");

            foreach (var param in state.Parameters.OrderBy(kv => kv.Key))
                sb.Append($"{param.Key}={param.Value}|");

            return sb.ToString();
        }
    }
}
