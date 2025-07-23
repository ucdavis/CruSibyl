using System;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;
using Serilog.Exceptions.Core;
using Serilog.Exceptions.EntityFrameworkCore.Destructurers;
using Serilog.Sinks.Elasticsearch;

namespace CruSibyl.Functions
{
    public static class LogConfiguration
    {
        public static ILogger Setup(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));

            // create global logger with standard configuration
            var loggerConfig = GetConfiguration(configuration);
            Log.Logger = loggerConfig.CreateLogger();

#if DEBUG
            Serilog.Debugging.SelfLog.Enable(msg => Debug.WriteLine(msg));
#endif

            return Log.Logger;
        }

        /// <summary>
        /// Get a logger configuration that logs to elasticsearch and console.
        /// </summary>
        /// <returns></returns>
        public static LoggerConfiguration GetConfiguration(IConfiguration configuration)
        {

            // standard logger
            var logConfig = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                // .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning) // uncomment this to hide EF core general info logs
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithExceptionDetails(new DestructuringOptionsBuilder()
                    .WithDefaultDestructurers()
                    .WithDestructurers([new DbUpdateExceptionDestructurer()]));

            // various sinks
            logConfig = logConfig
                .WriteTo.Console()
                .WriteToElasticSearchCustom(configuration);

            return logConfig;
        }

        private static LoggerConfiguration WriteToElasticSearchCustom(this LoggerConfiguration logConfig, IConfiguration configuration)
        {
            // get logging config for ES endpoint (re-use some stackify settings for now)
            var loggingSection = configuration.GetSection("Serilog");

            var esUrl = loggingSection.GetValue<string>("ElasticUrl"); //logging

            // only continue if a valid http url is setup in the config
            if (esUrl == null || !esUrl.StartsWith("http"))
            {
                return logConfig;
            }

            logConfig.Enrich.WithProperty("Application", loggingSection.GetValue<string>("AppName"));
            logConfig.Enrich.WithProperty("AppEnvironment", loggingSection.GetValue<string>("Environment"));

            if (Uri.TryCreate(esUrl, UriKind.Absolute, out var elasticUri))
            {
                return logConfig.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(elasticUri)
                {
                    IndexFormat = "aspnet-CruSibyl-{0:yyyy.MM}",
                    TypeName = null
                });
            }

            throw new Exception("Couldn't get log configured");
        }
    }
}
