using Microsoft.Extensions.Logging;
using MyUpdatedBot.Cache.ChatSettingsStore;
using MyUpdatedBot.Services.ChatSettings;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace MyUpdatedBot.Core.Handlers.ChatSettingsHandlers
{
    public class SettingsCallbackHandler : IButtonHandlers
    {
        private readonly IChatSettingsService _settingsService;
        private readonly IChatSettingsStore _settingsCache;
        private readonly ILogger<SettingsCallbackHandler> _logger;

        public SettingsCallbackHandler(IChatSettingsService settingsService, IChatSettingsStore settingsCache, ILogger<SettingsCallbackHandler> logger)
        {
            _settingsService = settingsService;
            _settingsCache = settingsCache;
            _logger = logger;
        }

        public bool CanHandle(CallbackQuery callback)
        {
            var data = callback.Data ?? string.Empty;
            return data.StartsWith("settings:toggle:");
        }

        public async Task HandleAsync(ITelegramBotClient botClient, CallbackQuery callback, CancellationToken ct)
        {
            if (callback.Message == null) return;

            var chatId = callback.Message.Chat.Id;
            var fromId = callback.From.Id;
            var data = callback.Data ?? string.Empty;

            ChatMember? member = null;
            try
            {
                member = await botClient.GetChatMember(chatId, fromId, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SettingsCallback: GetChatMember failed for {Chat}/{User}", chatId, fromId);
            }

            if (member == null || !(member.Status == ChatMemberStatus.Administrator || member.Status == ChatMemberStatus.Creator))
            {
                await botClient.AnswerCallbackQuery(callback.Id, "Только администраторы могут менять настройки.", showAlert: true, cancellationToken: ct);
                return;
            }

            if (data == "settings:toggle:links")
            {
                var updated = await _settingsService.SetLinksAllowedAsync(chatId, !(await _settingsService.GetOrCreateAsync(chatId, ct)).LinksAllowed, ct);
                // update cache with the new settings
                _settingsCache.Set(chatId, updated);

                var kb = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData($"Защита от флуда {(updated.SpamProtectionEnabled ? "✅" : "❌")}", $"settings:toggle:spam") },
                    new[] { InlineKeyboardButton.WithCallbackData($"Разрешить ссылки {(updated.LinksAllowed ? "✅" : "❌")}", $"settings:toggle:links") }
                });

                try
                {
                    await botClient.EditMessageReplyMarkup(chatId, callback.Message.MessageId, kb, cancellationToken: ct);
                }
                catch { }

                await botClient.AnswerCallbackQuery(callback.Id, $"Разрешить ссылки {(updated.LinksAllowed ? "✅" : "❌")}", cancellationToken: ct);
                return;
            }

            if (data == "settings:toggle:spam")
            {
                var updated = await _settingsService.SetSpamProtectionAsync(chatId, !(await _settingsService.GetOrCreateAsync(chatId, ct)).SpamProtectionEnabled, ct);
                _settingsCache.Set(chatId, updated);

                var kb = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData($"Защита от флуда {(updated.SpamProtectionEnabled ? "✅" : "❌")}", $"settings:toggle:spam") },
                    new[] { InlineKeyboardButton.WithCallbackData($"Разрешить ссылки {(updated.LinksAllowed ? "✅" : "❌")}", $"settings:toggle:links") }
                });

                try
                {
                    await botClient.EditMessageReplyMarkup(chatId, callback.Message.MessageId, kb, cancellationToken: ct);
                }
                catch { }

                await botClient.AnswerCallbackQuery(callback.Id, $"Защита от флуда {(updated.SpamProtectionEnabled ? "✅" : "❌")}", cancellationToken: ct);
                return;
            }

            await botClient.AnswerCallbackQuery(callback.Id, "Неизвестная команда.", cancellationToken: ct);
        }
    }
}
