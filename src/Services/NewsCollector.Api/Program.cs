using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.NameTranslation;
using NewsCollector.Api.Data;
using NewsCollector.Api.Models;
using NewsCollector.Api.Repository.PolymarketNewsRepository;
using NewsCollector.Api.Services;
using NewsCollector.Api.Services.DeepSeekAnalysisService;
using NewsCollector.Api.Services.DeepSeekAnalysisService.Backtesting;
using NewsCollector.Api.Services.DeepSeekAnalysisService.Explainability;
using NewsCollector.Api.Services.DeepSeekAnalysisService.RegimeAwareness;
using NewsCollector.Api.Services.DeepSeekAnalysisService.RiskManagement;
using NewsCollector.Api.Services.DeepSeekAnalysisService.SourceWeighting;
using NewsCollector.Api.Services.DeepSeekAnalysisService.Reporting;
using NewsCollector.Api.Services.NewsSignalService;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<PolymarketOptions>(builder.Configuration.GetSection("ExternalApis:Polymarket"));
builder.Services.Configure<KalshiOptions>(builder.Configuration.GetSection("ExternalApis:Kalshi"));
builder.Services.Configure<DeepSeekOptions>(builder.Configuration.GetSection("ExternalApis:DeepSeek"));
builder.Services.Configure<NewsRefreshSchedulerOptions>(builder.Configuration.GetSection("NewsRefreshScheduler"));

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is missing.");

var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.MapEnum<NewsCategory>("news_category", nameTranslator: new NpgsqlNullNameTranslator());
dataSourceBuilder.MapEnum<SignalAction>("signal_action", nameTranslator: new NpgsqlNullNameTranslator());
dataSourceBuilder.EnableUnmappedTypes();
dataSourceBuilder.EnableDynamicJson();
var dataSource = dataSourceBuilder.Build();

builder.Services.AddSingleton(dataSource);
builder.Services.AddDbContext<NewsDbContext>(options =>
    options.ConfigureWarnings(warnings =>
        warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.ManyServiceProvidersCreatedWarning))
        .UseNpgsql(dataSource, npgsqlOptions =>
        {
            npgsqlOptions.MapEnum<NewsCategory>("news_category", nameTranslator: new NpgsqlNullNameTranslator());
            npgsqlOptions.MapEnum<SignalAction>("signal_action", nameTranslator: new NpgsqlNullNameTranslator());
        }));

builder.Services.AddSingleton<INewsCatalog, InMemoryNewsCatalog>();
builder.Services.AddSingleton<INewsSignalService, NewsSignalService>();
builder.Services.AddScoped<IPolymarketNewsRepository, PolymarketNewsRepository>();
builder.Services.AddScoped<IAnalysisCandidateBuilder, AnalysisCandidateBuilder>();
builder.Services.AddSingleton<IAnalysisFingerprintService, AnalysisFingerprintService>();
builder.Services.AddScoped<IDeepSeekAnalysisService, DeepSeekAnalysisService>();
builder.Services.AddScoped<NewsCollector.Api.Services.DeepSeekAnalysisService.Persistence.IDeepSeekAnalysisPersistenceService, NewsCollector.Api.Services.DeepSeekAnalysisService.Persistence.DeepSeekAnalysisPersistenceService>();
builder.Services.AddScoped<IDeepSeekRegimeAwarenessService, DeepSeekRegimeAwarenessService>();
builder.Services.AddScoped<IDeepSeekSourceWeightingService, DeepSeekSourceWeightingService>();
builder.Services.AddScoped<IDeepSeekBacktestingService, DeepSeekBacktestingService>();
builder.Services.AddScoped<IDeepSeekRiskManagementService, DeepSeekRiskManagementService>();
builder.Services.AddScoped<IDeepSeekExplainabilityService, DeepSeekExplainabilityService>();
builder.Services.AddScoped<IDeepSeekAnalysisMaturityService, DeepSeekAnalysisMaturityService>();
builder.Services.AddScoped<IDeepSeekAnalysisReportingService, DeepSeekAnalysisReportingService>();
builder.Services.AddHostedService<NewsRefreshWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
