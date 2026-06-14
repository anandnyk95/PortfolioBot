using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using PortfolioBots.Bots;

var builder = WebApplication.CreateBuilder(args);
// 1. Add API controller routing support
builder.Services.AddControllers();

// 2. Set up local authentication (bypasses required Azure App IDs during local dev)
builder.Services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();

// // 3. Register the standard messaging adapter
// builder.Services.AddSingleton<IBotFrameworkHttpAdapter, CloudAdapter>();

// 3. Register the standard messaging adapter (Explicitly resolving the ambiguous constructor)
builder.Services.AddSingleton<IBotFrameworkHttpAdapter, CloudAdapter>(sp =>
{
    var auth = sp.GetRequiredService<BotFrameworkAuthentication>();
    var logger = sp.GetRequiredService<ILogger<CloudAdapter>>();
    return new CloudAdapter(auth, logger);
});

// 4. Configure local in-memory storage to replace paid cloud databases
builder.Services.AddSingleton<IStorage, MemoryStorage>();
builder.Services.AddSingleton<ConversationState>();
builder.Services.AddSingleton<UserState>();

// Register the standard HttpClient factory
builder.Services.AddHttpClient();

// 5. Register our custom bot logic
builder.Services.AddTransient<IBot, MyPortfoliBot>();

var app = builder.Build();

// Direct incoming requests to our API Controllers
app.MapControllers();

app.Run();
