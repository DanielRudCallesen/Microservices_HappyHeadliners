using Shared.Observability;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.AddObservability("NewsletterService");

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IImmediateArticleStore, InMemoryImmediateArticleStore>();

builder.Services.AddHostedService<ArticleQueueSubscriber>();

builder.Services.AddHttpClient("ArticleService", (sp, client) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var baseUrl = cfg["ArticleService:BaseUrl"] ?? "http://articleservice";
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
