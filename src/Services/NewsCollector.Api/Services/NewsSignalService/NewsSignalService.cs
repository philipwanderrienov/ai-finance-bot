using Microsoft.EntityFrameworkCore;
using NewsCollector.Api.Data;
using NewsCollector.Api.Models;

namespace NewsCollector.Api.Services.NewsSignalService;

public sealed class NewsSignalService : INewsSignalService
{
    private readonly NewsDbContext _dbContext;

    public NewsSignalService(NewsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IReadOnlyCollection<NewsSignal> Analyze(IEnumerable<NewsItem> items)
    {
        var grouped = items
            .GroupBy(item => item.Category)
            .Select(group => BuildSignal(group.Key, group.OrderByDescending(item => item.SentimentScore ?? 0m).ToList()));

        return grouped.OrderByDescending(signal => signal.Confidence).ToArray();
    }

    public async Task<IReadOnlyCollection<NewsSignal>> AnalyzeAndPersistAsync(IEnumerable<NewsItem> items, CancellationToken cancellationToken)
    {
        var signals = Analyze(items);

        foreach (var signal in signals)
        {
            var category = Enum.Parse<NewsCategory>(signal.Symbol, ignoreCase: true);
            var action = Enum.Parse<SignalAction>(signal.Action, ignoreCase: true);

            _dbContext.NewsSignals.Add(new NewsSignalEntity
            {
                Id = Guid.NewGuid(),
                NewsCategory = category,
                Symbol = signal.Symbol,
                Action = action,
                Confidence = signal.Confidence,
                SuggestedPrice = signal.SuggestedPrice,
                Reason = signal.Reason,
                GeneratedAt = DateTimeOffset.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return signals;
    }

    private static NewsSignal BuildSignal(NewsCategory category, IReadOnlyList<NewsItem> items)
    {
        var confidence = Math.Clamp(items.Average(item => item.SentimentScore ?? 0.5m), 0.1m, 0.99m);
        var action = confidence >= 0.65m ? "BUY" : confidence <= 0.35m ? "SELL" : "HOLD";
        var suggestedPrice = category switch
        {
            NewsCategory.Geopolitics => action == "BUY" ? 1.15m : 0.85m,
            NewsCategory.Gold => action == "BUY" ? 2.25m : 1.95m,
            NewsCategory.Crypto => action == "BUY" ? 3.40m : 2.75m,
            NewsCategory.Viral => action == "BUY" ? 0.95m : 0.70m,
            _ => action == "BUY" ? 1.00m : 0.90m
        };

        var symbol = category.ToString().ToUpperInvariant();
        var reason = $"Category={category}, avg sentiment={confidence:0.00}, news count={items.Count}";

        return new NewsSignal(symbol, action, confidence, suggestedPrice, reason, items);
    }
}
