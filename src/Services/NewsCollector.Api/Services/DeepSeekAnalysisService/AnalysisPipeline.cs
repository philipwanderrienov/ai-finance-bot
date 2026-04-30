using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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
        var articles = new List<NewsArticleEntity>();

        await _dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = _dbContext.Database.GetDbConnection();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, source_name, title, summary, url, published_at, sentiment_score, keywords
                FROM news_articles
                WHERE category::text = @category
                ORDER BY published_at DESC
                LIMIT @limit
                """;
            command.Parameters.Add(new NpgsqlParameter("category", request.Category.ToString().ToLowerInvariant()));
            command.Parameters.Add(new NpgsqlParameter("limit", lookbackCount * 3));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                articles.Add(new NewsArticleEntity
                {
                    Id = reader.GetGuid(reader.GetOrdinal("id")),
                    SourceName = reader.GetString(reader.GetOrdinal("source_name")),
                    Title = reader.GetString(reader.GetOrdinal("title")),
                    Summary = reader.GetString(reader.GetOrdinal("summary")),
                    Url = reader.GetString(reader.GetOrdinal("url")),
                    PublishedAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("published_at")),
                    SentimentScore = reader.IsDBNull(reader.GetOrdinal("sentiment_score"))
                        ? null
                        : reader.GetFieldValue<decimal>(reader.GetOrdinal("sentiment_score")),
                    Keywords = reader.IsDBNull(reader.GetOrdinal("keywords"))
                        ? new List<string>()
                        : JsonSerializer.Deserialize<List<string>>(reader.GetString(reader.GetOrdinal("keywords"))) ?? new List<string>()
                });
            }
        }
        finally
        {
            await _dbContext.Database.CloseConnectionAsync();
        }

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
