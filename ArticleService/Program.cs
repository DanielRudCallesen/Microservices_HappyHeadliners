using ArticleService.Data;
using ArticleService.Infrastructure.Interface;
using ArticleService.Models;
using Microsoft.EntityFrameworkCore;
using Shared.Observability;

var builder = WebApplication.CreateBuilder(args);

// Shared.Observability
builder.AddObservability("ArticleService");

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Dependency Injection
builder.Services.AddSingleton<ArticleService.Infrastructure.Interface.IConnectionStringResolver, ArticleService.Infrastructure.ConfigurationConnectionString>();
builder.Services.AddSingleton<ArticleService.Interfaces.IArticleRepositoryFactory, ArticleService.Data.ArticleRepositoryFactory>();
builder.Services.AddScoped<ArticleService.Interfaces.IArticleService, ArticleService.Data.ArticleService>();

builder.Services.AddHostedService<ArticleService.Infrastructure.ShardMigratorHostedService>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var inContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
if (!inContainer)
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();
// Shared.Observability
app.UseObservabilityRequestEnrichment();

app.MapControllers();

app.Run();
