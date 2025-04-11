using System.Linq.Expressions;

namespace Htmx.Components.Extensions;

public static class ExpressionExtensions
{
    /// <summary>
    /// Extracts the property name from an expression like x => x.Property
    /// </summary>
    public static string GetPropertyName<T>(this Expression<Func<T, object>> expression)
    {
        if (expression.Body is UnaryExpression unary && unary.Operand is MemberExpression memberUnary)
        {
            return memberUnary.Member.Name;
        }
        else if (expression.Body is MemberExpression member)
        {
            return member.Member.Name;
        }

        throw new ArgumentException("Expression must be a member access", nameof(expression));
    }    
}