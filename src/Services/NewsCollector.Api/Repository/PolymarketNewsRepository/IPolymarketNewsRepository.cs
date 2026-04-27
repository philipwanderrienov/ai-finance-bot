using System;
using NewsCollector.Api.Models;

namespace NewsCollector.Api.Repository.PolymarketNewsRepository;

public interface IPolymarketNewsRepository
{
    Task<int> UpsertAsync(IEnumerable<NewsItem> items, CancellationToken cancellationToken);
}
