using System.Text.Json.Serialization;
using NpgsqlTypes;

namespace NewsCollector.Api.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NewsCategory
{
    [PgName("Geopolitics")]
    Geopolitics = 0,
    [PgName("Gold")]
    Gold = 1,
    [PgName("Crypto")]
    Crypto = 2,
    [PgName("Viral")]
    Viral = 3,
    [PgName("Market")]
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

public enum SignalAction
{
    [PgName("Buy")]
    Buy = 0,
    [PgName("Sell")]
    Sell = 1,
    [PgName("Hold")]
    Hold = 2
}
