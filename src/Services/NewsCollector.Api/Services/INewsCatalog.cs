using NewsCollector.Api.Models;

namespace NewsCollector.Api.Services;

public interface INewsCatalog
{
    IReadOnlyCollection<NewsItem> GetLatest();
}
