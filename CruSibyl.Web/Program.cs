using System.Diagnostics;
using CruSibyl.Web.Middleware;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;
using Serilog.Sinks.Elasticsearch;

#if DEBUG
Serilog.Debugging.SelfLog.Enable(msg => Debug.WriteLine(msg));
#endif

var isDevelopment = string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "development", StringComparison.OrdinalIgnoreCase);
var configBuilder = new ConfigurationBuilder()
    .SetBasePath(System.IO.Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables();

//only add secrets in development
if (isDevelopment)
{
    configBuilder.AddUserSecrets<Program>();
}
var configuration = configBuilder.Build();
var loggingSection = configuration.GetSection("Serilog");

// configure logging as delegate so it can be applied to both Log.Logger and appBuilder.Host.UseSerilog()
var configureLogging = (LoggerConfiguration cfg) =>
{
    cfg.MinimumLevel.Debug()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
        // .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning) // uncomment this to hide EF core general info logs
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("System", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.WithClientIp()
        .Enrich.WithExceptionDetails()
        .Enrich.WithProperty("Application", loggingSection.GetValue<string>("AppName"))
        .Enrich.WithProperty("AppEnvironment", loggingSection.GetValue<string>("Environment"))
        .WriteTo.Console();

    // add in elastic search sink if the uri is valid
    if (Uri.TryCreate(loggingSection.GetValue<string>("ElasticUrl"), UriKind.Absolute, out var elasticUri))
    {
        cfg.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(elasticUri)
        {
            IndexFormat = "aspnet-crusibyl-{0:yyyy.MM}",
            TypeName = null,
        });
    }

    return cfg;
};

Log.Logger = configureLogging(new LoggerConfiguration()).CreateBootstrapLogger();

try
{
    Log.Information("Starting web host");
    var appBuilder = WebApplication.CreateBuilder(args);
    appBuilder.Host.UseSerilog((ctx, lc) => configureLogging(lc));

    // Add services to the container.

    appBuilder.Services.AddControllersWithViews(options =>
    {
        options.Filters.Add<SerilogControllerActionFilter>();
    });
    appBuilder.Services.AddEndpointsApiExplorer();
    appBuilder.Services.AddSwaggerGen();

    var app = appBuilder.Build();

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();

    //app.UseMiddleware<LogUserNameMiddleware>();
    app.UseSerilogRequestLogging();

    app.MapControllers();

    app.MapFallbackToFile("index.html");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled exception");
    return 1;
}
finally
{
    Log.Information("Shut down complete");
    Log.CloseAndFlush();
}

return 0;
