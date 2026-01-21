using MyUpdatedBot.Cache.ChatSettingsStore;
using MyUpdatedBot.Core.Models.Entities;
using MyUpdatedBot.Services.ChatSettings;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace MyUpdatedBot.Core.Handlers.ChatSettingsHandlers
{
    public class ChatSettingsHandler : ICommandHandler
    {
        private readonly IChatSettingsService _settingsService;
        private readonly IChatSettingsStore _settingsCache;

        public ChatSettingsHandler(IChatSettingsService settingsService, IChatSettingsStore settingsCache)
        {
            _settingsService = settingsService;
            _settingsCache = settingsCache;
        }

        public bool CanHandle(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            return text.StartsWith("/settings", StringComparison.OrdinalIgnoreCase);
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

            var kb = BuildSettingsKeyboard(settings);
            await botClient.SendMessage(chatId, "Настройки чата:", replyMarkup: kb, parseMode: ParseMode.Html, cancellationToken: ct);
        }

        private InlineKeyboardMarkup BuildSettingsKeyboard(ChatSettingsEntity s)
        {
            var rows = new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData($"Защита от флуда {(s.SpamProtectionEnabled ? "✅" : "❌")}", $"settings:toggle:spam") },
                new[] { InlineKeyboardButton.WithCallbackData($"Разрешить ссылки {(s.LinksAllowed ? "✅" : "❌")}", $"settings:toggle:links") }
            };
            return new InlineKeyboardMarkup(rows);
        }
    }
}
