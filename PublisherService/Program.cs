using Shared.Messaging;
using Shared.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability("PublisherService");

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddArticleQueue(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();
// Shared.Observability
app.UseObservabilityRequestEnrichment();
app.MapControllers();

app.Run();
