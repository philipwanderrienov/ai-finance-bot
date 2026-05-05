using System;
using NewsCollector.Api.Models;

namespace NewsCollector.Api.Services.DeepSeekAnalysisService.Reporting;

public interface IDeepSeekAnalysisReportingService
{
Task<DeepSeekAnalysisPerformanceReport> BuildAsync(NewsCategory? category, string? symbol, CancellationToken cancellationToken);
}
