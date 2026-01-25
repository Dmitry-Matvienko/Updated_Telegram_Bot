using MyUpdatedBot.Cache.ChatSettingsStore;
using MyUpdatedBot.Core.Models.Entities;
using MyUpdatedBot.Services.ChatSettings;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace MyUpdatedBot.Core.Handlers.ChatSettingsHandlers
{
    public class ChatSettingsHandler : IMessageHandler
    {
        private readonly IChatSettingsService _settingsService;
        private readonly IChatSettingsStore _settingsCache;

        public ChatSettingsHandler(IChatSettingsService settingsService, IChatSettingsStore settingsCache)
        {
            _settingsService = settingsService;
            _settingsCache = settingsCache;
        }

        public bool CanHandle(Message? message)
        {
            if (message?.From == null || message.Chat == null || message.From.IsBot) return false;
            if (message.Chat.Type != ChatType.Group && message.Chat.Type != ChatType.Supergroup) return false;
            if (string.IsNullOrWhiteSpace(message.Text)) return false;

            return message.Text.StartsWith("/settings", StringComparison.OrdinalIgnoreCase);
        }

        public async Task HandleAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
        {
            var chatId = message.Chat.Id;
            ChatSettingsEntity? settings = null;

            if (!_settingsCache.TryGet(chatId, out settings) || settings == null)
            {
                settings = await _settingsService.GetOrCreateAsync(chatId, ct);
                _settingsCache.Set(chatId, settings);
            }

            var buttons = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData($"Защита от флуда {(settings.SpamProtectionEnabled ? "✅" : "❌")}", $"settings:toggle:spam") },
                new[] { InlineKeyboardButton.WithCallbackData($"Разрешить ссылки {(settings.LinksAllowed ? "✅" : "❌")}", $"settings:toggle:links") }
            });
            await botClient.SendMessage(chatId, "Настройки чата:", replyMarkup: buttons, parseMode: ParseMode.Html, cancellationToken: ct);
        }
    }
}
