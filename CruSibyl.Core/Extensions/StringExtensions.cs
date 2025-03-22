using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using CruSibyl.Core.Domain;
using CruSibyl.Core.Services;

namespace CruSibyl.Core.Extensions;

public static class StringExtensions
{
    public static string EncodeBase64(this string value)
    {
        var valueBytes = Encoding.UTF8.GetBytes(value);
        return Convert.ToBase64String(valueBytes);
    }

    public static string DecodeBase64(this string value)
    {
        var valueBytes = Convert.FromBase64String(value);
        return Encoding.UTF8.GetString(valueBytes);
    }

    // Naive utility for converting Serilog templates and arguments to a string
    public static string FormatTemplate(this string messageTemplate, object parameter, params object[] additionalParameters)
    {
        return FormatTemplate(messageTemplate, new[] { parameter }.Concat(additionalParameters));
    }

    // Naive utility for converting Serilog templates and arguments to a string
    public static string FormatTemplate(this string messageTemplate, IEnumerable<object> parameters)
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
    
    public static string SafeTruncate(this string value, int max)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= max)
        {
            return value;
        }

        if (max <= 0)
        {
            return String.Empty;
        }

        return value.Substring(0, max);
    }
}
