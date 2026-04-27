using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NewsCollector.Api.Data;
using NewsCollector.Api.Models;

namespace NewsCollector.Api.Services;

public interface IPolymarketNewsRepository
{
    Task<int> UpsertAsync(IEnumerable<NewsItem> items, CancellationToken cancellationToken);
}

public sealed class EfPolymarketNewsRepository : IPolymarketNewsRepository
{
    private const string SourceName = "Polymarket";

    private readonly NewsDbContext _dbContext;
    private readonly string _baseUrl;

    public EfPolymarketNewsRepository(NewsDbContext dbContext)
    {
        _dbContext = dbContext;
        _baseUrl = BuildConfiguration().GetSection("ExternalApis:Polymarket")["BaseUrl"]
            ?? throw new InvalidOperationException("ExternalApis:Polymarket:BaseUrl is missing.");
    }

    public async Task<int> UpsertAsync(IEnumerable<NewsItem> items, CancellationToken cancellationToken)
    {
        var polymarketItems = items
            .Where(item => string.Equals(item.Source, SourceName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (polymarketItems.Count == 0)
        {
            return 0;
        }

        await EnsureSourceAsync(cancellationToken);

        var source = await _dbContext.NewsSources
            .AsNoTracking()
            .SingleAsync(x => x.Name == SourceName, cancellationToken);

        var urls = polymarketItems.Select(x => x.Url).ToList();
        var existingArticles = await _dbContext.NewsArticles
            .Where(x => urls.Contains(x.Url))
            .ToDictionaryAsync(x => x.Url, cancellationToken);

        foreach (var item in polymarketItems)
        {
            if (existingArticles.TryGetValue(item.Url, out var existing))
            {
                existing.Title = item.Title;
                existing.Summary = item.Summary;
                existing.PublishedAt = item.PublishedAt;
                existing.Category = item.Category;
                existing.SentimentScore = item.SentimentScore;
                existing.Keywords = item.Keywords?.ToList();
                existing.RawPayload = JsonSerializer.Serialize(item);
                existing.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                _dbContext.NewsArticles.Add(new NewsArticleEntity
                {
                    Id = Guid.NewGuid(),
                    SourceId = source.Id,
                    SourceExternalId = null,
                    SourceName = SourceName,
                    Title = item.Title,
                    Summary = item.Summary,
                    Url = item.Url,
                    PublishedAt = item.PublishedAt,
                    Category = item.Category,
                    SentimentScore = item.SentimentScore,
                    Keywords = item.Keywords?.ToList(),
                    RawPayload = JsonSerializer.Serialize(item),
                    IngestedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        return await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureSourceAsync(CancellationToken cancellationToken)
    {
        var source = await _dbContext.NewsSources.SingleOrDefaultAsync(x => x.Name == SourceName, cancellationToken);
        if (source is not null)
        {
            return;
        }

        _dbContext.NewsSources.Add(new NewsSourceEntity
        {
            Name = SourceName,
            BaseUrl = _baseUrl,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IConfigurationRoot BuildConfiguration()
        => new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
}
