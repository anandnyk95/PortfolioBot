using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;

namespace PortfolioBot.Controllers;

[Route("api/messages")]
[ApiController]

public class BotController : ControllerBase
{
    public readonly IBotFrameworkHttpAdapter _adapter;
    public readonly IBot _bot;

    public BotController(IBotFrameworkHttpAdapter adapter, IBot bot)
    {
        _adapter = adapter;
        _bot = bot;
    }

    [HttpPost]
    public async Task PostAsync()
    {
        // Pass the raw HTTP request directly to the Bot Adapter to parse
        await _adapter.ProcessAsync(Request, Response, _bot);
    }
}