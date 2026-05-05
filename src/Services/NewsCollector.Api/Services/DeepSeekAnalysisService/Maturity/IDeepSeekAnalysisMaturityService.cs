using System;
using NewsCollector.Api.Models;

namespace NewsCollector.Api.Services.DeepSeekAnalysisService.Maturity;

public interface IDeepSeekAnalysisMaturityService
{
    DeepSeekAnalysisMaturityLayer BuildMaturityLayer(DeepSeekAnalysisResult result, bool simulatorMode, string? modelName = null);
}
