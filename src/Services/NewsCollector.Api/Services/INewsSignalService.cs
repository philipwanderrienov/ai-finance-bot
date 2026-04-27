using NewsCollector.Api.Models;

namespace NewsCollector.Api.Services;

public interface INewsSignalService
{
    IEnumerable<NewsSignal> Analyze(IEnumerable<NewsItem> items);
}
