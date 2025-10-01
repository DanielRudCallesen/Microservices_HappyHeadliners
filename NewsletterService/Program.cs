using NewsletterService.Messaging;
using NewsletterService.Services;
using Shared.Messaging;
using Shared.Observability;

var builder = WebApplication.CreateBuilder(args);

// Observability
builder.AddObservability("NewsletterService");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Messaging 
builder.Services.AddArticleQueue(builder.Configuration);

// Immediate in-memory store
builder.Services.AddSingleton<IImmediateArticleStore, InMemoryImmediateArticleStore>();

// Subscriber hosted service
builder.Services.AddHostedService<ArticleQueueSubscriber>();

// HTTP client to ArticleService for daily digest
builder.Services.AddHttpClient("ArticleService", (sp, client) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var baseUrl = cfg["ArticleService:BaseUrl"] ?? "http://article-service:8080";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
});

builder.Services.AddHostedService<DailyNewsletterWorker>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseObservabilityRequestEnrichment();

app.UseAuthorization();

app.MapControllers();

app.Run();
