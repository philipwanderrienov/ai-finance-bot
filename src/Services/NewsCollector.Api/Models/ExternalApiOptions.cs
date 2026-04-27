namespace NewsCollector.Api.Models;

public sealed class PolymarketOptions
{
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
}

public sealed class KalshiOptions
{
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
}

public sealed class DeepSeekOptions
{
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
}

public sealed class NewsRefreshSchedulerOptions
{
    public int InitialDelaySeconds { get; set; } = 0;
    public int IntervalMinutes { get; set; } = 1;
}
