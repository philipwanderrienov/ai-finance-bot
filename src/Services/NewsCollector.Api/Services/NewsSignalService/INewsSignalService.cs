using NewsCollector.Api.Models;

namespace NewsCollector.Api.Services.NewsSignalService;

public interface INewsSignalService
{
    IEnumerable<NewsSignal> Analyze(IEnumerable<NewsItem> items);
}
