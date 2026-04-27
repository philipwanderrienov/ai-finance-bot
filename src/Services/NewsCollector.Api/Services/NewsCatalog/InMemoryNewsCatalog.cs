using System.Text.Json;
using Microsoft.Extensions.Options;
using NewsCollector.Api.Models;

namespace NewsCollector.Api.Services.NewsCatalog;

public sealed class InMemoryNewsCatalog : INewsCatalog
{
    private const int PageSize = 100;
    private const int MaxPages = 3;
    private static readonly HttpClient HttpClient = new();

    private readonly string _baseUrl;

    public InMemoryNewsCatalog(IOptions<PolymarketOptions> options)
    {
        _baseUrl = NormalizeBaseUrl(options.Value.BaseUrl);
    }

    public IReadOnlyCollection<NewsItem> GetLatest()
        => FetchLatest().GetAwaiter().GetResult();

    private async Task<IReadOnlyCollection<NewsItem>> FetchLatest()
    {
        var results = new List<NewsItem>();

        for (var page = 0; page < MaxPages; page++)
        {
            var offset = page * PageSize;
            var url = $"{_baseUrl}/events?active=true&closed=false&order=volume_24hr&ascending=false&limit={PageSize}&offset={offset}";

            using var response = await HttpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(payload))
            {
                break;
            }

            using var document = JsonDocument.Parse(payload);
            var items = ReadItems(document.RootElement).ToList();
            if (items.Count == 0)
            {
                break;
            }

            results.AddRange(items);

            if (items.Count < PageSize)
            {
                break;
            }
        }

        return results
            .GroupBy(item => item.Url, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.PublishedAt).First())
            .OrderByDescending(item => item.PublishedAt)
            .ToList();
    }

    private static IEnumerable<NewsItem> ReadItems(JsonElement root)
    {
        var elements = root.ValueKind switch
        {
            JsonValueKind.Array => root.EnumerateArray(),
            JsonValueKind.Object when root.TryGetProperty("events", out var events) && events.ValueKind == JsonValueKind.Array => events.EnumerateArray(),
            JsonValueKind.Object when root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array => items.EnumerateArray(),
            _ => Enumerable.Empty<JsonElement>()
        };

        foreach (var element in elements)
        {
            var slug = GetString(element, "slug")
                ?? GetString(element, "event_slug")
                ?? GetString(element, "id")
                ?? GetString(element, "question")
                ?? Guid.NewGuid().ToString("N");

            var title = GetString(element, "title")
                ?? GetString(element, "question")
                ?? GetString(element, "name")
                ?? "Polymarket market";

            var summary = GetString(element, "description")
                ?? GetString(element, "subtitle")
                ?? title;

            var publishedAt = GetDateTime(element, "updated_at")
                ?? GetDateTime(element, "created_at")
                ?? GetDateTime(element, "start_date")
                ?? GetDateTime(element, "end_date")
                ?? DateTimeOffset.UtcNow;

            var url = BuildMarketUrl(slug);
            var tags = ReadTags(element);
            var category = DetermineCategory(title, summary, tags);
            var sentiment = EstimateSentiment(title, summary, tags);

            yield return new NewsItem(
                "Polymarket",
                title,
                summary,
                url,
                publishedAt,
                category,
                sentiment,
                tags);
        }
    }

    private static string[] ReadTags(JsonElement element)
    {
        var tags = new List<string>();

        if (element.TryGetProperty("tags", out var tagElement) && tagElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var tag in tagElement.EnumerateArray())
            {
                var value = tag.ValueKind == JsonValueKind.String
                    ? tag.GetString()
                    : GetString(tag, "label") ?? GetString(tag, "name");

                if (!string.IsNullOrWhiteSpace(value))
                {
                    tags.Add(value.Trim());
                }
            }
        }

        if (element.TryGetProperty("categories", out var categoryElement) && categoryElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var category in categoryElement.EnumerateArray())
            {
                var value = category.ValueKind == JsonValueKind.String
                    ? category.GetString()
                    : GetString(category, "label") ?? GetString(category, "name");

                if (!string.IsNullOrWhiteSpace(value))
                {
                    tags.Add(value.Trim());
                }
            }
        }

        return tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static NewsCategory DetermineCategory(string title, string summary, IReadOnlyCollection<string> tags)
    {
        var text = string.Join(' ', new[] { title, summary }.Concat(tags)).ToLowerInvariant();

        if (ContainsAny(text, "gold", "inflation", "cpi", "fed", "rate", "rates", "yield"))
        {
            return NewsCategory.Gold;
        }

        if (ContainsAny(text, "crypto", "bitcoin", "btc", "ethereum", "eth", "solana", "sol", "defi", "blockchain"))
        {
            return NewsCategory.Crypto;
        }

        if (ContainsAny(text, "geopolit", "war", "russia", "ukraine", "israel", "iran", "china", "taiwan", "middle east"))
        {
            return NewsCategory.Geopolitics;
        }

        if (ContainsAny(text, "viral", "trending", "meme", "social", "buzz"))
        {
            return NewsCategory.Viral;
        }

        return NewsCategory.Market;
    }

    private static decimal EstimateSentiment(string title, string summary, IReadOnlyCollection<string> tags)
    {
        var text = string.Join(' ', new[] { title, summary }.Concat(tags)).ToLowerInvariant();

        var score = 0.5m;

        if (ContainsAny(text, "bull", "surge", "rise", "support", "strong", "gain", "positive", "approve"))
        {
            score += 0.12m;
        }

        if (ContainsAny(text, "risk", "pressure", "crash", "drop", "ban", "decline", "uncertain", "fear"))
        {
            score -= 0.12m;
        }

        if (ContainsAny(text, "war", "escalation", "attack", "sanction"))
        {
            score -= 0.10m;
        }

        return Math.Clamp(score, 0.05m, 0.95m);
    }

    private static bool ContainsAny(string text, params string[] tokens)
        => tokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static string BuildMarketUrl(string slug)
        => $"https://polymarket.com/event/{slug}";

    private static string NormalizeBaseUrl(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return "https://gamma-api.polymarket.com";
        }

        return baseUrl.Contains("api.polymarket.com", StringComparison.OrdinalIgnoreCase)
            ? "https://gamma-api.polymarket.com"
            : baseUrl.TrimEnd('/');
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static DateTimeOffset? GetDateTime(JsonElement element, string propertyName)
    {
        var value = GetString(element, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(value, out var parsed)
            ? parsed
            : null;
    }
}
