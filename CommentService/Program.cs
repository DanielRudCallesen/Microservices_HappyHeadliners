using CommentService.Data;
using CommentService.Interface;
using CommentService.Services;
using Microsoft.EntityFrameworkCore;
using Polly;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
builder.Services.AddDbContext<CommentDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CommentDatabase")));

// Local Fallback filter
builder.Services.AddSingleton<ILocalProfanityFilter, LocalProfanityFilter>();

// Profanity Client, circuit breaker to Profanity Service
var baseUrl = builder.Configuration.GetValue<string>("ProfanityService:BaseUrl") ?? "http://profanityservice:8080";

builder.Services.AddHttpClient<IProfanityClient, ProfanityHttpClient>(client =>
{
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
}).AddStandardResilienceHandler();

// Background refresh for local dictionary
builder.Services.AddHostedService<ProfanityDictionaryRefresher>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Database with retries for container startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CommentDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    var attempts = 0;
    while (true)
    {
        try
        {
            await db.Database.MigrateAsync();
            break;
        }
        catch (Exception ex) when (attempts++ < 10)
        {
            logger.LogWarning(ex, "Comment Database migrate attempt {Attempt} failed. Retrying...", attempts);
            await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempts))));
        }
    }
}

// No redirect to HTTPS in containers (only HTTP published)
var inContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_In_Container") == "true";
if (!inContainer)
{
    app.UseHttpsRedirection();
}


app.UseAuthorization();

app.MapControllers();

app.Run();
