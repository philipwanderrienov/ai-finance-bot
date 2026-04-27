using Microsoft.Extensions.DependencyInjection;
using NewsCollector.Api.Models;

namespace NewsCollector.Api.Services;

public sealed class NewsRefreshWorker : BackgroundService
{
    private readonly ILogger<NewsRefreshWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public NewsRefreshWorker(
        ILogger<NewsRefreshWorker> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ProcessBatchAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessBatchAsync(stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var catalog = scope.ServiceProvider.GetRequiredService<INewsCatalog>();
        var signalService = scope.ServiceProvider.GetRequiredService<INewsSignalService>();
        var repository = scope.ServiceProvider.GetRequiredService<IPolymarketNewsRepository>();

        var news = catalog.GetLatest().ToList();
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
}
