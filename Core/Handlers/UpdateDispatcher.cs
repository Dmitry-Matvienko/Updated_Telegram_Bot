using Microsoft.Extensions.Logging;
using System.Diagnostics;
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

                        _logger.LogDebug("[UpdateDispatcher]: Update {UpdateId}: handler {Handler} will process message \"{message}\". User: {FristName} - {UserId}",
                            update.Id,
                            messHandler.GetType().Name,
                            msg.Type,
                            update.Message?.From?.FirstName,
                            update.Message?.From?.Id);

                        var sw = Stopwatch.StartNew();
                        await messHandler.HandleAsync(client, msg, ct);
                        sw.Stop();

                        _logger.LogInformation("[UpdateDispatcher]: Update {UpdateId}: handler {Handler} completed successfully in {ElapsedMs}ms",
                                update.Id,
                                messHandler.GetType().Name,
                                sw.ElapsedMilliseconds);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[UpdateDispatcher]: message-handler {Handler} threw for {MessageType}",
                            messHandler.GetType().Name, update.Type);
                    }
                }

                if (msg.Text is string text)
                {
                    // foreach all the text handlers and looking for the right one
                    foreach (var handler in _commandHandlers)
                    {
                        if (!handler.CanHandle(text))
                            continue;

                        try
                        {
                            _logger.LogDebug("[UpdateDispatcher]: User: {FirstName} sent message: {Text}. UserId: {UserId}, UpdateId: {UpdateId}",
                                    update.Message?.From?.FirstName,
                                    update.Message?.Text,
                                    update.Message?.From?.Id,
                                    update.Id);

                            var sw = Stopwatch.StartNew();
                            await handler.HandleAsync(client, msg, ct);
                            sw.Stop();

                            _logger.LogInformation("[UpdateDispatcher]: Update {UpdateId}: handler {Handler} completed successfully in {ElapsedMs}ms",
                                update.Id,
                                handler.GetType().Name,
                                sw.ElapsedMilliseconds);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "[UpdateDispatcher]: Update {UpdateId}: handler {Handler} threw exception",
                                update.Id,
                                handler.GetType().Name);
                            _logger.LogError("[UpdateDispatcher]: User: {FirstName} sent message: {Text}. UserId: {UserId}, UpdateId: {UpdateId}",
                                    update.Message?.From?.FirstName,
                                    update.Message?.Text,
                                    update.Message?.From?.Id,
                                    update.Id);
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

                    try
                    {
                        _logger.LogDebug("[UpdateDispatcher]: Update {UpdateId}: button-handler {Handler} will process data=\"{Data}\"",
                        update.Id, handler.GetType().Name, data);

                        var sw = Stopwatch.StartNew();
                        await handler.HandleAsync(client, callback, ct);
                        sw.Stop();

                        _logger.LogInformation("[UpdateDispatcher]: button-handler {Handler} completed successfully for data {Data} in {ElapsedMs}ms",
                            handler.GetType().Name, data, sw.ElapsedMilliseconds);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,"[UpdateDispatcher]: button-handler {Handler} threw exception for data {Data}",
                            handler.GetType().Name, data);
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
