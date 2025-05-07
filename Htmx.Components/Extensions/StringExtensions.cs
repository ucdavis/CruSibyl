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

}