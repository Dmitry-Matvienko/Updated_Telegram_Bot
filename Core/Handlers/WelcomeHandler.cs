using Microsoft.Extensions.Logging;
using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace MyUpdatedBot.Core.Handlers
{
    public class WelcomeHandler : IMessageHandler
    {
        private static long? _cachedBotId;
        private static readonly SemaphoreSlim _botIdLock = new SemaphoreSlim(1, 1);

        private static readonly InlineKeyboardMarkup _languageButtons = new InlineKeyboardMarkup(new[]
        {
            new []
            {
                InlineKeyboardButton.WithCallbackData("🇬🇧 English", "settings:toggle:lang:en"),
            },
            new []
            {
                InlineKeyboardButton.WithCallbackData("🇷🇺 Русский", "settings:toggle:lang:ru"),
            },
            new []
            {
                InlineKeyboardButton.WithCallbackData("🇺🇦 Українська", "settings:toggle:lang:uk")
            }
        });

        public bool CanHandle(Message? message)
        {
            if (message == null) return false;
            if (message.NewChatMembers != null && message.NewChatMembers.Length > 0) return true;
            if (string.IsNullOrWhiteSpace(message.Text)) return false;
            if (message.Text.StartsWith("/start", StringComparison.OrdinalIgnoreCase) && message.Chat.Type == ChatType.Private) return true;

            return false;
        }

        public async Task HandleAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
        {
            var text = message.Text?.Trim() ?? "";
            var chatId = message.Chat.Id;

            if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase) && message.Chat.Type == ChatType.Private)
            {
                await botClient.SendMessage(
                chatId: chatId,
                text: "👋",
                replyMarkup: _languageButtons,
                cancellationToken: ct);

                return;
            }

            var botId = await GetBotIdAsync(botClient, ct);
            if (message.NewChatMembers!.Any(u => u.Id == botId))
            {
                var fallbackCulture = NormalizeLanguageCode(message.From?.LanguageCode ?? "en");
                var cl = new CultureInfo(fallbackCulture);

                await botClient.SendMessage(
                    chatId: chatId,
                    text: Resources.Messages.ResourceManager.GetString("Welcome_Title", cl) ?? Resources.Messages.Welcome_Title,
                    replyMarkup: _languageButtons,
                    cancellationToken: ct);
            }
        }

        private static string NormalizeLanguageCode(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return string.Empty;
            var c = code.ToLowerInvariant();
            if (c == "ua") return "uk";
            if (c.Contains('-')) c = c.Split('-')[0];
            return c.Length >= 2 ? c.Substring(0, 2) : string.Empty;
        }

        private static async Task<long> GetBotIdAsync(ITelegramBotClient botClient, CancellationToken ct)
        {
            if (_cachedBotId.HasValue) return _cachedBotId.Value;

            await _botIdLock.WaitAsync(ct);
            try
            {
                if (_cachedBotId.HasValue) return _cachedBotId.Value;
                var me = await botClient.GetMe(ct);
                _cachedBotId = me.Id;
                return me.Id;
            }
            finally
            {
                _botIdLock.Release();
            }
        }
    }
}
