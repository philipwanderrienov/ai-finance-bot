using Microsoft.AspNetCore.Mvc;
using NewsCollector.Api.Models;
using NewsCollector.Api.Services;

namespace NewsCollector.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class NewsController : ControllerBase
{
    private readonly INewsCatalog _catalog;
    private readonly INewsSignalService _signalService;

    public NewsController(INewsCatalog catalog, INewsSignalService signalService)
    {
        _catalog = catalog;
        _signalService = signalService;
    }

    [HttpGet]
    public ActionResult<IReadOnlyCollection<NewsItem>> GetLatestNews()
        => Ok(_catalog.GetLatest());

    [HttpGet("signals")]
    public ActionResult<IReadOnlyCollection<NewsSignal>> GetSignals()
        => Ok(_signalService.Analyze(_catalog.GetLatest()).ToArray());
}
