using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace MyUpdatedBot.Core.Handlers
{
    public class UpdateDispatcher : IUpdateHandlerService
    {
        private readonly IEnumerable<ICommandHandler> _commandHandlers;
        private readonly ILogger<UpdateDispatcher> _logger;

        public UpdateDispatcher(IEnumerable<ICommandHandler> commandHandlers, ILogger<UpdateDispatcher> logger)
        {
            _commandHandlers = commandHandlers;
            _logger = logger;
        }

        public async Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken ct)
        {
            _logger.LogInformation(
            "[UpdateDispatcher]: update {UpdateId} of type {UpdateType}",
            update.Id,
            update.Type);

            if (update.Message?.Text is string text)
            {
                // foreach all the handlers and looking for the right one
                foreach (var handler in _commandHandlers)
                {
                    if (!handler.CanHandle(text))
                        continue;

                    _logger.LogInformation(
                        "[UpdateDispatcher]: Update {UpdateId}: handler {Handler} will process text \"{Text}\". User: {FristName} - {UserId}",
                        update.Id,
                        handler.GetType().Name,
                        text,
                        update.Message?.From?.FirstName,
                        update.Message?.From?.Id);

                    try
                    {
                        _logger.LogDebug("[UpdateDispatcher]: User: {FirstName} sent message: {Text}. UserId: {UserId}, UpdateId: {UpdateId}",
                                update.Message?.From?.FirstName,
                                update.Message?.Text,
                                update.Message?.From?.Id,
                                update.Id);

                        await handler.HandleAsync(client, update.Message, ct);

                        _logger.LogInformation(
                            "[UpdateDispatcher]: Update {UpdateId}: handler {Handler} completed successfully",
                            update.Id,
                            handler.GetType().Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "[UpdateDispatcher]: Update {UpdateId}: handler {Handler} threw exception",
                            update.Id,
                            handler.GetType().Name);
                    }
                }
            }
            else
            {
                _logger.LogDebug(
                    "[UpdateDispatcher]: Update {UpdateId} has unknown message to dispatch",
                    update.Id);
            }
            // TODO: add CallbackQuery, InlineQuery etc.
        }
    }
}
