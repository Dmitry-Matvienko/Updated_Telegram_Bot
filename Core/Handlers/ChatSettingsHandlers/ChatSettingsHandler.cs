using MyUpdatedBot.Cache.ChatSettingsStore;
using MyUpdatedBot.Core.Localization;
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
        private readonly LocalizationUtil _loc;

        public ChatSettingsHandler(IChatSettingsService settingsService, IChatSettingsStore settingsCache, LocalizationUtil loc)
        {
            _settingsService = settingsService;
            _settingsCache = settingsCache;
            _loc = loc;
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
                new[] { InlineKeyboardButton.WithCallbackData($"{_loc.GetString(settings, "ChatSettings_SpamProtectionButton")} {(settings.SpamProtectionEnabled ? "✅" : "❌")}", $"settings:toggle:spam") },
                new[] { InlineKeyboardButton.WithCallbackData($"{_loc.GetString(settings, "ChatSettings_LinksAllowedButton")} {(settings.LinksAllowed ? "✅" : "❌")}", $"settings:toggle:links") },
                new[] { InlineKeyboardButton.WithCallbackData($"{_loc.GetString(settings, "ChatSettings_LanguageTitle")} {_loc.GetLanguageDisplayName(settings.Language)}", $"settings:toggle:lang:{settings.Language}") },
            });
            await botClient.SendMessage(chatId, $"{_loc.GetString(settings, "ChatSettings_Title")}:", replyMarkup: buttons, cancellationToken: ct);
        }
    }
}
