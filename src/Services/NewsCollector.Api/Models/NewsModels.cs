namespace NewsCollector.Api.Models;

public enum NewsCategory
{
    Geopolitics = 0,
    Gold = 1,
    Crypto = 2,
    Viral = 3,
    Market = 4
}

public sealed record NewsItem(
    string Source,
    string Title,
    string Summary,
    string Url,
    DateTimeOffset PublishedAt,
    NewsCategory Category,
    decimal? SentimentScore = null,
    IReadOnlyCollection<string>? Keywords = null);

public sealed record NewsSignal(
    string Symbol,
    string Action,
    decimal Confidence,
    decimal SuggestedPrice,
    string Reason,
    IReadOnlyCollection<NewsItem> SupportingNews);
