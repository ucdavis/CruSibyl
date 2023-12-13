using System.Threading.Tasks;
using Serilog.Context;

namespace CruSibyl.Web.Middleware;

public class LogUserNameMiddleware
{
    private readonly RequestDelegate _next;

    public LogUserNameMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        using (LogContext.PushProperty("User", context?.User?.Identity?.Name ?? "anonymous"))
        {
            if (context != null)
            {
                await _next(context);
            }
        }
    }
}
