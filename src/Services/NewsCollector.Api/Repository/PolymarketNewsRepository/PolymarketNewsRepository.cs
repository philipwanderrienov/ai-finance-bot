using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NewsCollector.Api.Data;
using NewsCollector.Api.Models;
using NewsCollector.Api.Services;

namespace NewsCollector.Api.Repository.PolymarketNewsRepository;

public sealed class PolymarketNewsRepository : IPolymarketNewsRepository
{
    private const string SourceName = "Polymarket";

    private readonly NewsDbContext _dbContext;
    private readonly string _baseUrl;

    public PolymarketNewsRepository(NewsDbContext dbContext)
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

        var recentArticles = await _dbContext.NewsArticles
            .AsNoTracking()
            .Where(x => x.SourceName == SourceName)
            .OrderByDescending(x => x.PublishedAt)
            .Take(500)
            .Select(x => new { x.Url, x.Title, x.Summary })
            .ToListAsync(cancellationToken);

        var existingUrls = new HashSet<string>(
            recentArticles.Select(x => x.Url).Where(x => !string.IsNullOrWhiteSpace(x)),
            StringComparer.OrdinalIgnoreCase);

        var existingSignatures = new HashSet<string>(
            recentArticles.Select(x => BuildSignature(x.Title, x.Summary)),
            StringComparer.OrdinalIgnoreCase);

        var inserted = 0;

        foreach (var item in polymarketItems)
        {
            if (!string.IsNullOrWhiteSpace(item.Url) && existingUrls.Contains(item.Url))
            {
                continue;
            }

            var signature = BuildSignature(item.Title, item.Summary);
            if (existingSignatures.Contains(signature))
            {
                continue;
            }

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

            if (!string.IsNullOrWhiteSpace(item.Url))
            {
                existingUrls.Add(item.Url);
            }

            existingSignatures.Add(signature);
            inserted++;
        }

        if (inserted == 0)
        {
            return 0;
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

    private static string BuildSignature(string title, string summary)
    {
        static string Normalize(string value)
            => new string((value ?? string.Empty)
                .ToLowerInvariant()
                .Where(char.IsLetterOrDigit)
                .ToArray());

        return $"{Normalize(title)}|{Normalize(summary)}";
    }

    private static IConfigurationRoot BuildConfiguration()
        => new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
}
