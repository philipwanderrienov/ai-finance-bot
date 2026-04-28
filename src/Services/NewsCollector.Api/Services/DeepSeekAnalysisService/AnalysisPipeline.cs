using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NewsCollector.Api.Data;
using NewsCollector.Api.Models;

namespace NewsCollector.Api.Services.DeepSeekAnalysisService;

public interface IAnalysisCandidateBuilder
{
    Task<IReadOnlyList<AnalysisCandidate>> BuildAsync(DeepSeekAnalysisRequest request, CancellationToken cancellationToken);
}

public interface IAnalysisFingerprintService
{
    string CreateFingerprint(NewsCategory category, string symbol, IReadOnlyList<AnalysisCandidate> candidates);
}

public sealed record AnalysisCandidate(
    Guid Id,
    string SourceName,
    string Title,
    string Summary,
    string Url,
    DateTimeOffset PublishedAt,
    decimal? SentimentScore,
    IReadOnlyCollection<string> Keywords);

public sealed class AnalysisCandidateBuilder : IAnalysisCandidateBuilder
{
    private readonly NewsDbContext _dbContext;

    public AnalysisCandidateBuilder(NewsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<AnalysisCandidate>> BuildAsync(DeepSeekAnalysisRequest request, CancellationToken cancellationToken)
    {
        var lookbackCount = Math.Clamp(request.LookbackCount, 1, 50);

        var articles = await _dbContext.NewsArticles
            .AsNoTracking()
            .Where(x => x.Category == request.Category)
            .OrderByDescending(x => x.PublishedAt)
            .Take(lookbackCount * 3)
            .ToListAsync(cancellationToken);

        if (articles.Count == 0)
        {
            return Array.Empty<AnalysisCandidate>();
        }

        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenSignatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<AnalysisCandidate>(lookbackCount);

        foreach (var article in articles)
        {
            if (candidates.Count >= lookbackCount)
            {
                break;
            }

            if (!string.IsNullOrWhiteSpace(article.Url) && !seenUrls.Add(article.Url))
            {
                continue;
            }

            var signature = BuildSignature(article.Title, article.Summary);
            if (!seenSignatures.Add(signature))
            {
                continue;
            }

            candidates.Add(new AnalysisCandidate(
                article.Id,
                article.SourceName,
                article.Title,
                article.Summary,
                article.Url,
                article.PublishedAt,
                article.SentimentScore,
                article.Keywords ?? new List<string>()));
        }

        return candidates;
    }

    private static string BuildSignature(string title, string summary)
    {
        static string Normalize(string value)
            => new string(value
                .ToLowerInvariant()
                .Where(char.IsLetterOrDigit)
                .ToArray());

        return $"{Normalize(title)}|{Normalize(summary)}";
    }
}

public sealed class AnalysisFingerprintService : IAnalysisFingerprintService
{
    public string CreateFingerprint(NewsCategory category, string symbol, IReadOnlyList<AnalysisCandidate> candidates)
    {
        var builder = new StringBuilder();
        builder.Append(category);
        builder.Append('|');
        builder.Append(symbol.Trim().ToLowerInvariant());

        foreach (var candidate in candidates)
        {
            builder.Append('|');
            builder.Append(candidate.Id);
            builder.Append(':');
            builder.Append(candidate.Url.Trim().ToLowerInvariant());
            builder.Append(':');
            builder.Append(candidate.Title.Trim().ToLowerInvariant());
            builder.Append(':');
            builder.Append(candidate.PublishedAt.UtcDateTime.Ticks);
        }

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}
