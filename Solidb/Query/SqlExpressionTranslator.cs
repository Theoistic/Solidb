using System;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Solidb.Mapping;

namespace Solidb.Query
{
    public sealed class SqlExpressionTranslator
    {
        private readonly EntityMap _map;
        private readonly QueryState _state;
        private readonly StringBuilder _sql = new();

        public SqlExpressionTranslator(EntityMap map, QueryState state)
        {
            _map = map;
            _state = state;
        }

        public string Translate(Expression expression)
        {
            _sql.Clear();
            Visit(expression);
            return _sql.ToString();
        }

        private void Visit(Expression expr)
        {
            switch (expr)
            {
                case LambdaExpression lambda:
                    Visit(lambda.Body);
                    break;
                case BinaryExpression binary:
                    VisitBinary(binary);
                    break;
                case MemberExpression member:
                    VisitMember(member);
                    break;
                case ConstantExpression constant:
                    VisitConstant(constant.Value);
                    break;
                case UnaryExpression unary:
                    VisitUnary(unary);
                    break;
                case MethodCallExpression call:
                    VisitMethodCall(call);
                    break;
                default:
                    throw new NotSupportedException(
                        $"Expression type '{expr.GetType().Name}' cannot be translated to SQL. " +
                        "Materialize the query first or use a supported expression.");
            }
        }

        private void VisitBinary(BinaryExpression expr)
        {
            // Special-case null comparisons → IS NULL / IS NOT NULL
            if (expr.Right is ConstantExpression { Value: null })
            {
                Visit(expr.Left);
                _sql.Append(expr.NodeType == ExpressionType.Equal ? " IS NULL" : " IS NOT NULL");
                return;
            }
            if (expr.Left is ConstantExpression { Value: null })
            {
                Visit(expr.Right);
                _sql.Append(expr.NodeType == ExpressionType.Equal ? " IS NULL" : " IS NOT NULL");
                return;
            }

            _sql.Append('(');
            Visit(expr.Left);
            _sql.Append(GetSqlOperator(expr.NodeType));
            Visit(expr.Right);
            _sql.Append(')');
        }

        private void VisitMember(MemberExpression expr)
        {
            if (expr.Expression is ParameterExpression)
            {
                var prop = _map.Properties.FirstOrDefault(p => p.PropertyName == expr.Member.Name);
                _sql.Append(prop?.ColumnName ?? expr.Member.Name);
            }
            else
            {
                // Captured variable or property of a local object — evaluate it
                var value = Expression.Lambda(expr).Compile().DynamicInvoke();
                VisitConstant(value);
            }
        }

        private void VisitConstant(object? value)
        {
            if (value == null)
            {
                _sql.Append("NULL");
                return;
            }

            var paramName = _state.NextParam();
            _state.Parameters[paramName] = NormalizeValue(value);
            _sql.Append($"@{paramName}");
        }

        private void VisitUnary(UnaryExpression expr)
        {
            switch (expr.NodeType)
            {
                case ExpressionType.Not:
                    _sql.Append("NOT (");
                    Visit(expr.Operand);
                    _sql.Append(')');
                    break;
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                    Visit(expr.Operand);
                    break;
                default:
                    throw new NotSupportedException($"Unary operator '{expr.NodeType}' is not supported.");
            }
        }

        private void VisitMethodCall(MethodCallExpression expr)
        {
            if (expr.Method.DeclaringType == typeof(string))
            {
                switch (expr.Method.Name)
                {
                    case nameof(string.Contains):
                        Visit(expr.Object!);
                        _sql.Append(" LIKE ");
                        AppendLikeParam($"%{EvalString(expr.Arguments[0])}%");
                        return;
                    case nameof(string.StartsWith):
                        Visit(expr.Object!);
                        _sql.Append(" LIKE ");
                        AppendLikeParam($"{EvalString(expr.Arguments[0])}%");
                        return;
                    case nameof(string.EndsWith):
                        Visit(expr.Object!);
                        _sql.Append(" LIKE ");
                        AppendLikeParam($"%{EvalString(expr.Arguments[0])}");
                        return;
                }
            }

            throw new NotSupportedException(
                $"Method '{expr.Method.DeclaringType?.Name}.{expr.Method.Name}' cannot be translated to SQL. " +
                "Materialize the query first or register a custom SQL function.");
        }

        private void AppendLikeParam(string pattern)
        {
            var paramName = _state.NextParam();
            _state.Parameters[paramName] = pattern;
            _sql.Append($"@{paramName}");
        }

        private static string? EvalString(Expression expr) =>
            Expression.Lambda(expr).Compile().DynamicInvoke()?.ToString();

        private static object? NormalizeValue(object? value) => value switch
        {
            bool b => b ? 1 : 0,
            Guid g => g.ToString(),
            _ => value
        };

        private static string GetSqlOperator(ExpressionType nodeType) => nodeType switch
        {
            ExpressionType.Equal => " = ",
            ExpressionType.NotEqual => " <> ",
            ExpressionType.GreaterThan => " > ",
            ExpressionType.GreaterThanOrEqual => " >= ",
            ExpressionType.LessThan => " < ",
            ExpressionType.LessThanOrEqual => " <= ",
            ExpressionType.AndAlso => " AND ",
            ExpressionType.OrElse => " OR ",
            _ => throw new NotSupportedException($"Binary operator '{nodeType}' is not supported.")
        };
    }
}
