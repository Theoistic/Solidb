using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Solidb.Query
{
    public sealed class QueryState
    {
        private int _paramCounter;

        public List<LambdaExpression> WhereExpressions { get; } = new();
        public List<(LambdaExpression Expression, bool Ascending)> OrderByExpressions { get; } = new();

        /// <summary>Navigation property names to load via split queries (one-to-many).</summary>
        public List<LambdaExpression> IncludeExpressions { get; } = new();

        /// <summary>Navigation property names to load via LEFT JOIN (many-to-one / one-to-one).</summary>
        public List<LambdaExpression> JoinIncludeExpressions { get; } = new();

        public int? Skip { get; set; }
        public int? Take { get; set; }
        public TimeSpan? CacheDuration { get; set; }
        public Dictionary<string, object?> Parameters { get; } = new();

        public string NextParam()
        {
            var name = $"p{_paramCounter}";
            _paramCounter++;
            return name;
        }
    }
}
