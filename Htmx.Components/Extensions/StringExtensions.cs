using System.Globalization;
using System.Text.RegularExpressions;

namespace Htmx.Components.Extensions;

public static class StringExtensions
{
    public static string SanitizeForHtmlId(this string input)
    {
        // Replace invalid characters with underscores
        return string.Concat(input.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == ':' ? c : '_'));
    }

    // Naive utility for converting Serilog templates and arguments to a string
    internal static string FormatTemplate(this string messageTemplate, object parameter, params object[] additionalParameters)
    {
        return FormatTemplate(messageTemplate, new[] { parameter }.Concat(additionalParameters));
    }

    // Naive utility for converting Serilog templates and arguments to a string
    internal static string FormatTemplate(this string messageTemplate, IEnumerable<object> parameters)
    {
        var objects = parameters as object[] ?? parameters.ToArray();

        if (objects.Length == 0)
        {
            return messageTemplate;
        }

        if (objects.Length != Regex.Matches(messageTemplate, "{.*?}").Count)
        {
            throw new ArgumentException("Number of arguments does not match number of template parameters");
        }

        var i = 0;
        return Regex.Replace(messageTemplate, "{.*?}", _ => objects[i++]?.ToString() ?? string.Empty);
    }

    public static string ConvertToInputString<T>(this T value)
    {
        if (value == null)
            return string.Empty;

        var type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        // If T is object, use the runtime type of value
        if (type == typeof(object))
            type = value.GetType();

        if (type == typeof(DateTime))
            return ((DateTime)(object)value).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        if (type == typeof(DateTimeOffset))
            return ((DateTimeOffset)(object)value).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (type == typeof(TimeSpan))
            return ((TimeSpan)(object)value).ToString("HH\\:mm\\:ss", CultureInfo.InvariantCulture);
        if (type == typeof(Guid))
            return ((Guid)(object)value).ToString();
        if (type == typeof(DateOnly))
            return ((DateOnly)(object)value).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (type == typeof(TimeOnly))
            return ((TimeOnly)(object)value).ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        if (type == typeof(bool))
            return ((bool)(object)value) ? "true" : "false";
        if (type.IsEnum)
            return ((Enum)(object)value).ToString();

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }
}