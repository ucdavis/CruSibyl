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

    /// <summary>
    /// Extracts the property name from an expression like x => x.Property
    /// </summary>
    public static string GetPropertyName<T, TProp>(this Expression<Func<T, TProp>> expression)
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

    /// <summary>
    /// Extracts the type of the property from an expression like x => x.Property
    /// </summary>
    public static Type GetMemberType<T>(this Expression<Func<T, object>> expression)
        where T : class
    {
        Type type;
        if (expression.Body is UnaryExpression unary && unary.Operand is MemberExpression memberUnary)
        {
            type = memberUnary.Type;
        }
        else if (expression.Body is MemberExpression member)
        {
            type = member.Type;
        }
        else
        {
            throw new ArgumentException("Expression must be a member access", nameof(expression));
        }

        // If the type is nullable, get the underlying type
        return Nullable.GetUnderlyingType(type) ?? type;
    }
}