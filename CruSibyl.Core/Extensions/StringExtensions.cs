using System.Security.Claims;
using System.Text;
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
}
