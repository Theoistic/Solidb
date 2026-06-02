using System;
using System.Collections;
using System.Linq.Expressions;

namespace Solidb.Query
{
    public static class IncludeBuilder
    {
        public static string GetNavigationName(LambdaExpression includeExpr)
        {
            if (includeExpr.Body is MemberExpression member)
                return member.Member.Name;
            throw new InvalidOperationException(
                "Include expression must be a direct property access, e.g. x => x.Posts.");
        }

        public static bool IsCollectionNavigation(LambdaExpression includeExpr, Type ownerType)
        {
            if (includeExpr.Body is not MemberExpression member) return false;
            var prop = ownerType.GetProperty(member.Member.Name);
            if (prop == null) return false;
            return typeof(IEnumerable).IsAssignableFrom(prop.PropertyType)
                && prop.PropertyType != typeof(string);
        }
    }
}
