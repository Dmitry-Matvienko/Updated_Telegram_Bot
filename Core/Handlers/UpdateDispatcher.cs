using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace MyUpdatedBot.Core.Handlers
{
    public class UpdateDispatcher : IUpdateHandlerService
    {
        private readonly IEnumerable<ICommandHandler> _commandHandlers;
        private readonly IEnumerable<IButtonHandlers> _buttonHandlers;
        private readonly IEnumerable<IMessageHandler> _messageHandlers;
        private readonly ILogger<UpdateDispatcher> _logger;

        public UpdateDispatcher(IEnumerable<ICommandHandler> commandHandlers, ILogger<UpdateDispatcher> logger, 
                                IEnumerable<IButtonHandlers> buttonHandlers, IEnumerable<IMessageHandler> messageHandlers)
        {
            _commandHandlers = commandHandlers;
            _logger = logger;
            _buttonHandlers = buttonHandlers;
            _messageHandlers = messageHandlers;
        }

        public async Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken ct)
        {
            _logger.LogDebug("[UpdateDispatcher]: update {UpdateId} of type {UpdateType}",
            update.Id,
            update.Type);

            if (update.Message is Message msg)
            {
                foreach (var messHandler in _messageHandlers)
                {
                    try
                    {
                        if (!messHandler.CanHandle(msg)) continue;

                        _logger.LogInformation("[UpdateDispatcher]: Update {UpdateId}: handler {Handler} will process message \"{message}\". User: {FristName} - {UserId}",
                            update.Id,
                            messHandler.GetType().Name,
                            msg.Type,
                            update.Message?.From?.FirstName,
                            update.Message?.From?.Id);

                        await messHandler.HandleAsync(client, msg, ct);

                        _logger.LogInformation("[UpdateDispatcher]: Update {UpdateId}: handler {Handler} completed successfully",
                                update.Id,
                                messHandler.GetType().Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[UpdateDispatcher]: message-handler {Handler} threw for update {UpdateId}",
                            messHandler.GetType().Name, update.Id);
                    }
                }

                if (msg.Text is string text)
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

                            await handler.HandleAsync(client, msg, ct);

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
