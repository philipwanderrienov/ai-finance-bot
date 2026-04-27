using System;
using NewsCollector.Api.Models;

namespace NewsCollector.Api.Services.DeepSeekAnalysisService;

public interface IDeepSeekAnalysisService
{
    Task<DeepSeekAnalysisResult> AnalyzeAsync(DeepSeekAnalysisRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<DeepSeekAnalysisResult>> GetLatestAsync(NewsCategory category, string symbol, int take, CancellationToken cancellationToken);
}
