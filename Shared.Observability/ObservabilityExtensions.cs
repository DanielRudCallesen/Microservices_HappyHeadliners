using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Context;
using Serilog.Formatting.Compact;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;

namespace Shared.Observability;

    public static class ObservabilityExtensions
    {
        public static void AddObservability(this WebApplicationBuilder builder, string serviceName)
        {
            var enabled = builder.Configuration.GetValue("Observability:Enabled", true);
            if (!enabled) return;


            // Allowing config/env to override the passed service name
            var configuredName = builder.Configuration["Observability:ServiceName"];
            if (!string.IsNullOrWhiteSpace(configuredName))
            {
                serviceName = configuredName;
            }

        // Read configuration from appsettings.json, but never throws for fallback to console if config is invalid or missing
            try
            {
                Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(builder.Configuration).WriteTo
                    .Console(new RenderedCompactJsonFormatter()).CreateLogger();
                builder.Host.UseSerilog();
                
            }
            catch
            {
                // Fail-Open, use default logging
                builder.Logging.ClearProviders();
                builder.Logging.AddSimpleConsole(o => o.TimestampFormat = "0 ");
                // NO THROW; Lets the service continue without Serilog
            }

            // OpenTelemetry Tracing, if endpoint is invalid skips exporter
            try
            {
                var otlp = builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://otel-collector:4317";
                var endpointOk = Uri.TryCreate(otlp, UriKind.Absolute, out var endpointUri);

                builder.Services.AddOpenTelemetry().ConfigureResource(r => r.AddService(serviceName)
                        .AddAttributes(new[]
                            {
                                new KeyValuePair<string, object>("deployment.environment",
                                    builder.Environment.EnvironmentName)
                            }))
                    .WithTracing(tp =>
                    {
                        tp.AddSource(serviceName).AddAspNetCoreInstrumentation(o => o.RecordException = true)
                            .AddHttpClientInstrumentation().AddSqlClientInstrumentation(o =>
                            {
                                // Disable if SQL contains sensitive data
                                o.SetDbStatementForText = true;
                                o.RecordException = true;
                            })
                            .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(1.0)));
                        if (endpointOk)
                        {
                            tp.AddOtlpExporter(o => o.Endpoint = endpointUri!);
                        }
                    }).WithMetrics(mp =>
                    {
                        mp.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddRuntimeInstrumentation().AddMeter("HappyHeadlines.Cache").AddPrometheusExporter();
                    });
            }
            catch
            {
                // Fail Open, if OpenTelemetry setup fails, continue without it
            }




        }
        // Adds TraceId/SpanId to log and request logging
        public static void UseObservabilityRequestEnrichment(this WebApplication app)
        {
            try
            {
                app.MapPrometheusScrapingEndpoint();
                app.Use(async (_, next) =>
                {
                    var act = Activity.Current;
                    using (LogContext.PushProperty("TraceId", act?.TraceId.ToString() ?? string.Empty))
                    using (LogContext.PushProperty("SpanId", act?.SpanId.ToString() ?? string.Empty))
                    {
                        await next();
                    }
                });

                app.UseSerilogRequestLogging(o =>
                {
                    o.MessageTemplate =
                        "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
                });
            }
            catch
            {
                // Fail-open, keeps serving even if Serilog middleware is not avaiable
            }
        }
    }

