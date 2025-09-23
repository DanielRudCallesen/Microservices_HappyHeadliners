using System.Diagnostics;
using DraftService.Data;
using DraftService.Interfaces;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Context;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Serilog ocnfiguration
Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(builder.Configuration).CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<DraftDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DraftDatabase"),
        options => options.EnableRetryOnFailure()));

builder.Services.AddScoped<IDraftService, DraftService.Services.DraftService>();


// OpenTelemetry Tracing

var serviceName = builder.Configuration["OpenTelemetry:ServiceName"] ?? "DraftService";
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://otel-collector:4317";
var environment = builder.Environment.EnvironmentName;

builder.Services.AddOpenTelemetry().ConfigureResource(r => r.AddService(serviceName).AddAttributes(new[]
{
    new KeyValuePair<string, object>("deployment.environment", environment)
})).WithTracing(tp => tp.AddSource(serviceName)
    .AddAspNetCoreInstrumentation(o => o.RecordException = true)
    .AddHttpClientInstrumentation()
    .AddSqlClientInstrumentation(o =>
    {
        o.SetDbStatementForText = true; // For future references. REMOVE if risk of sensitive data
        o.RecordException = true;
    })
    .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(1.0)))
    .AddOtlpExporter(o =>
    {
        o.Endpoint = new Uri(otlpEndpoint);
    }));



var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
// Automatic DB migrate with retries
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DraftDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    var attempts = 0;
    while (true)
    {
        try
        {
            await db.Database.MigrateAsync();
            break;
        }
        catch (Exception ex) when (attempts++ < 8)
        {
            logger.LogWarning(ex, "Draft DB migrate attempt {attempts} failed. Retrying...", attempts);
            await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempts))));
        }
    }
}

// Enrich Serilog logs with Trace & Span Ids
app.Use(async (ctx, next) =>
{
    var act = Activity.Current;
    using (LogContext.PushProperty("TraceId", act?.TraceId.ToString() ?? string.Empty))
    using (LogContext.PushProperty("SpanId", act?.SpanId.ToString() ?? string.Empty))
    {
        await next();
    }
});

app.UseSerilogRequestLogging(opts =>
{
    opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
});


app.UseAuthorization();

app.MapControllers();

app.Run();
