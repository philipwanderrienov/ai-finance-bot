using NewsCollector.Api.Models;

namespace NewsCollector.Api.Services.NewsCatalog;

public interface INewsCatalog
{
    IReadOnlyCollection<NewsItem> GetLatest();
}
