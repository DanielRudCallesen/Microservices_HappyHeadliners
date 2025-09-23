using DraftService.Data;
using DraftService.Interfaces;
using Microsoft.EntityFrameworkCore;
using Shared.Observability;

var builder = WebApplication.CreateBuilder(args);

// Shared.Observability
builder.AddObservability("DraftService");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<DraftDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DraftDatabase"),
        options => options.EnableRetryOnFailure()));

builder.Services.AddScoped<IDraftService, DraftService.Services.DraftService>();


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

// Shared.Observability
app.UseObservabilityRequestEnrichment();

app.UseAuthorization();

app.MapControllers();

app.Run();
