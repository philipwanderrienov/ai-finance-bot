using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NewsCollector.Api.Models;
using NewsCollector.Api.Repository.PolymarketNewsRepository;
using NewsCollector.Api.Services.NewsSignalService;

namespace NewsCollector.Api.Services;

public sealed class NewsRefreshWorker : BackgroundService
{
    private readonly ILogger<NewsRefreshWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NewsRefreshSchedulerOptions _schedulerOptions;

    public NewsRefreshWorker(
        ILogger<NewsRefreshWorker> logger,
        IServiceScopeFactory scopeFactory,
        IOptions<NewsRefreshSchedulerOptions> schedulerOptions)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _schedulerOptions = schedulerOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_schedulerOptions.InitialDelaySeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(_schedulerOptions.InitialDelaySeconds), stoppingToken);
        }

        await ProcessBatchAsync(stoppingToken);

        var interval = TimeSpan.FromMinutes(Math.Max(1, _schedulerOptions.IntervalMinutes));
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessBatchAsync(stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();

            var catalog = scope.ServiceProvider.GetRequiredService<INewsCatalog>();
            var signalService = scope.ServiceProvider.GetRequiredService<INewsSignalService>();
            var repository = scope.ServiceProvider.GetRequiredService<IPolymarketNewsRepository>();

            var news = catalog.GetLatest().ToList();
            _logger.LogInformation("Fetched {NewsCount} news items before Polymarket upsert.", news.Count);

            var inserted = await repository.UpsertAsync(news, cancellationToken);
            var signals = signalService.Analyze(news).ToList();

            _logger.LogInformation(
                "Refreshed {NewsCount} news items, inserted {InsertedCount} Polymarket rows, and generated {SignalCount} signals at {Time}",
                news.Count,
                inserted,
                signals.Count,
                DateTimeOffset.UtcNow);

            foreach (var signal in signals)
            {
                _logger.LogInformation(
                    "Signal {Action} {Symbol} confidence={Confidence} price={Price} reason={Reason}",
                    signal.Action,
                    signal.Symbol,
                    signal.Confidence,
                    signal.SuggestedPrice,
                    signal.Reason);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "News refresh batch failed.");
        }
    }
}
