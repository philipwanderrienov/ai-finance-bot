using Microsoft.EntityFrameworkCore;
using NewsCollector.Api.Data;
using NewsCollector.Api.Models;
using NewsCollector.Api.Repository.PolymarketNewsRepository;
using NewsCollector.Api.Services;
using NewsCollector.Api.Services.DeepSeekAnalysisService;
using NewsCollector.Api.Services.NewsSignalService;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<PolymarketOptions>(builder.Configuration.GetSection("ExternalApis:Polymarket"));
builder.Services.Configure<KalshiOptions>(builder.Configuration.GetSection("ExternalApis:Kalshi"));
builder.Services.Configure<DeepSeekOptions>(builder.Configuration.GetSection("ExternalApis:DeepSeek"));
builder.Services.Configure<NewsRefreshSchedulerOptions>(builder.Configuration.GetSection("NewsRefreshScheduler"));

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is missing.");

builder.Services.AddDbContext<NewsDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.MapEnum<NewsCategory>("news_category");
        npgsqlOptions.MapEnum<SignalAction>("signal_action");
    }));

builder.Services.AddSingleton<INewsCatalog, InMemoryNewsCatalog>();
builder.Services.AddSingleton<INewsSignalService, NewsSignalService>();
builder.Services.AddScoped<IPolymarketNewsRepository, PolymarketNewsRepository>();
builder.Services.AddScoped<IDeepSeekAnalysisService, DeepSeekAnalysisService>();
builder.Services.AddHostedService<NewsRefreshWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
