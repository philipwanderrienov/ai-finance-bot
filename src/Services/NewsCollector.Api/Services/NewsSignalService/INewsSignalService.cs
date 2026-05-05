using NewsCollector.Api.Models;

namespace NewsCollector.Api.Services.NewsSignalService;

public interface INewsSignalService
{
    IReadOnlyCollection<NewsSignal> Analyze(IEnumerable<NewsItem> items);

    Task<IReadOnlyCollection<NewsSignal>> AnalyzeAndPersistAsync(IEnumerable<NewsItem> items, CancellationToken cancellationToken);
}
