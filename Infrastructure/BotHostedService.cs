using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using MyUpdatedBot.Core.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace MyUpdatedBot.Infrastructure;
public class BotHostedService : BackgroundService
{
    private readonly ITelegramBotClient _client;
    private readonly ILogger<BotHostedService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public BotHostedService(
        ITelegramBotClient client,
        ILogger<BotHostedService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _client = client;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting BotHostedService");
        var me = await _client.GetMe(ct);
        _logger.LogInformation($"[BotHostedService]: Bot @{me.Username} started");

        var receiverOptions = new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() };

        _logger.LogInformation("[BotHostedService]: Begin polling for updates");
        _client.StartReceiving(
            updateHandler: async (botClient, update, token) =>
            {
                _logger.LogDebug("[BotHostedService]: Received update {UpdateType} (Id={UpdateId})", update.Type, update.Id);
                try
                {
                    _logger.LogTrace("[BotHostedService]: Creating service scope for update {UpdateId}", update.Id);

                    using var scope = _scopeFactory.CreateScope();
                    var handler = scope.ServiceProvider
                                       .GetRequiredService<IUpdateHandlerService>();

                    _logger.LogInformation("[BotHostedService]: Handling update {UpdateId} with {Handler}", update.Id, handler.GetType().Name);

                    await handler.HandleUpdateAsync(botClient, update, token);

                    _logger.LogInformation("[BotHostedService]: Successfully handled update {UpdateId}", update.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[BotHostedService]: Error handling update {UpdateId}", update.Id);
                }
            },
            errorHandler: async (botClient, exception, token) =>
            {
                _logger.LogError(exception, "[BotHostedService]: Polling error, retry in 5s");
                await Task.Delay(TimeSpan.FromSeconds(5), token);
                _logger.LogInformation("[BotHostedService]: Retrying polling now");
            },
            receiverOptions: receiverOptions,
            cancellationToken: ct
        );
        // Just waiting for the signal to stop.
        await Task.Delay(Timeout.Infinite, ct);
    }
}