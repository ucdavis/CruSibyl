namespace Htmx.Components.Extensions;

public static class StringExtensions
{
    public static string SanitizeForHtmlId(this string input)
    {
        // Replace invalid characters with underscores
        return string.Concat(input.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == ':' ? c : '_'));
    }    
}