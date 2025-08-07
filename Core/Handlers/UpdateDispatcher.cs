using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace MyUpdatedBot.Core.Handlers
{
    public class UpdateDispatcher : IUpdateHandlerService
    {
        private readonly IEnumerable<ICommandHandler> _commandHandlers;
        private readonly IEnumerable<IButtonHandlers> _buttonHandlers;
        private readonly ILogger<UpdateDispatcher> _logger;

        public UpdateDispatcher(IEnumerable<ICommandHandler> commandHandlers, ILogger<UpdateDispatcher> logger, IEnumerable<IButtonHandlers> buttonHandlers)
        {
            _commandHandlers = commandHandlers;
            _logger = logger;
            _buttonHandlers = buttonHandlers;
        }

        public async Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken ct)
        {
            _logger.LogInformation("[UpdateDispatcher]: update {UpdateId} of type {UpdateType}",
            update.Id,
            update.Type);

            if (update.Message?.Text is string text)
            {
                // foreach all the text handlers and looking for the right one
                foreach (var handler in _commandHandlers)
                {
                    if (!handler.CanHandle(text))
                        continue;

                    _logger.LogInformation("[UpdateDispatcher]: Update {UpdateId}: handler {Handler} will process text \"{Text}\". User: {FristName} - {UserId}",
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

                        _logger.LogInformation("[UpdateDispatcher]: Update {UpdateId}: handler {Handler} completed successfully",
                            update.Id,
                            handler.GetType().Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[UpdateDispatcher]: Update {UpdateId}: handler {Handler} threw exception",
                            update.Id,
                            handler.GetType().Name);
                    }
                }
            }

            // foreach all the inline button handlers and looking for the right one
            if (update.CallbackQuery is CallbackQuery callback)
            {
                var data = callback.Data ?? string.Empty;
                foreach (var handler in _buttonHandlers)
                {
                    if (!handler.CanHandle(callback))
                        continue;

                    _logger.LogInformation("[UpdateDispatcher]: Update {UpdateId}: button-handler {Handler} will process data=\"{Data}\"",
                        update.Id, handler.GetType().Name, data);

                    try
                    {
                        await handler.HandleAsync(client, callback, ct);
                        _logger.LogInformation("[UpdateDispatcher]: button-handler {Handler} completed successfully for update {UpdateId}",
                            handler.GetType().Name, update.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,"[UpdateDispatcher]: button-handler {Handler} threw exception for update {UpdateId}",
                            handler.GetType().Name, update.Id);
                    }
                }
            }
            else
            {
                _logger.LogDebug("[UpdateDispatcher]: Update {UpdateId} has unknown message to dispatch",
                    update.Id);
            }
        }
    }
}
