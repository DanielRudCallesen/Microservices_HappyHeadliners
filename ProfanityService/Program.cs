using Microsoft.EntityFrameworkCore;
using ProfanityService.Data;
using Shared.Observability;

var builder = WebApplication.CreateBuilder(args);

// Shared.Observability
builder.AddObservability("ProfanityService");

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<ProfanityDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ProfanityDatabase")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ProfanityDbContext>();
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
            logger.LogWarning(ex, "Profanity DB migrate attempt {attempts} failed. Retrying...", attempts);
            await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempts))));
        }
    }
}

app.UseHttpsRedirection();

app.UseAuthorization();

// Shared.Observability
app.UseObservabilityRequestEnrichment();

app.MapControllers();

app.Run();
