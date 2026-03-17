using Microsoft.Extensions.Logging;
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
    public class SettingsCallbackHandler : IButtonHandlers
    {
        private readonly IChatSettingsService _settingsService;
        private readonly IChatSettingsStore _settingsCache;
        private readonly ILogger<SettingsCallbackHandler> _logger;
        private readonly LocalizationUtil _loc;

        public SettingsCallbackHandler(IChatSettingsService settingsService, IChatSettingsStore settingsCache, ILogger<SettingsCallbackHandler> logger, LocalizationUtil loc)
        {
            _settingsService = settingsService;
            _settingsCache = settingsCache;
            _logger = logger;
            _loc = loc;
        }

        public bool CanHandle(CallbackQuery callback)
        {
            var data = callback.Data ?? string.Empty;
            return data.StartsWith("settings:");
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
                member = await botClient.GetChatMember(chatId, fromId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SettingsCallbackHandler]: GetChatMember failed for {Chat}/{User}", chatId, fromId);
            }

            if (member == null || (callback.Message.Chat.Type != ChatType.Private && !(member.Status == ChatMemberStatus.Administrator || member.Status == ChatMemberStatus.Creator)))
            {
                await botClient.AnswerCallbackQuery(callback.Id, await _loc.GetStringAsync(chatId, callback.Message, "OnlyAdminsCanChange"), showAlert: true, cancellationToken: ct);
                return;
            }

            var current = await _settingsService.GetOrCreateAsync(chatId, ct);
            if (data == "settings:toggle:links")
            {
                var updated = await _settingsService.SetLinksAllowedAsync(chatId, !current.LinksAllowed, ct);
                // update cache with the new settings
                _settingsCache.Set(chatId, updated);

                var kb = BuildSettingsKeyboard(updated);

                try
                {
                    await botClient.EditMessageReplyMarkup(chatId, callback.Message.MessageId, kb, cancellationToken: ct);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[SettingsCallbackHandler]: Failed to EditMessageReplyMarkup for chat {Chat}, data: {data}", chatId, data);
                }

                await botClient.AnswerCallbackQuery(callback.Id, $"{_loc.GetString(updated, "ChatSettings_LinksAllowedButton")} {(updated.LinksAllowed ? "✅" : "❌")}", cancellationToken: ct);
                return;
            }

            if (data == "settings:toggle:spam")
            {
                var updated = await _settingsService.SetSpamProtectionAsync(chatId, !current.SpamProtectionEnabled, ct);
                _settingsCache.Set(chatId, updated);
                var kb = BuildSettingsKeyboard(updated);

                try
                {
                    await botClient.EditMessageReplyMarkup(chatId, callback.Message.MessageId, kb, cancellationToken: ct);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[SettingsCallbackHandler]: Failed to EditMessageReplyMarkup for chat {Chat}, data: {data}", chatId, data);
                }

                await botClient.AnswerCallbackQuery(callback.Id, $"{_loc.GetString(updated, "ChatSettings_SpamProtectionButton")} {(updated.SpamProtectionEnabled ? "✅" : "❌")}", cancellationToken: ct);
                return;
            }

            if (data.StartsWith("settings:set:lang:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = data.Split(':', StringSplitOptions.RemoveEmptyEntries);
                var arg = parts[3].ToLowerInvariant();

                if (arg == "en" || arg == "ru" || arg == "uk")
                {
                    ChatSettingsEntity updated;
                    try
                    {
                        updated = await _settingsService.SetLanguageAsync(chatId, arg, ct);
                        _settingsCache.Set(chatId, updated);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[SettingsCallbackHandler]: Failed to set language {Lang} for chat {Chat}", arg, chatId);
                        return;
                    }
                    try
                    {
                        var isGroup = callback.Message?.Chat?.Type == ChatType.Group || callback.Message?.Chat?.Type == ChatType.Supergroup;

                        if (isGroup)
                        {
                            var kb = BuildSettingsKeyboard(updated);
                            await botClient.EditMessageText(chatId, callback.Message!.MessageId, _loc.GetString(updated, "ChosenLanguage"), replyMarkup: kb, cancellationToken: ct);
                        }
                        else
                        {
                            await botClient.EditMessageText(chatId, callback.Message!.MessageId, _loc.GetString(updated, "ChosenLanguage"), cancellationToken: ct);

                            var me = await botClient.GetMe(ct);

                            var AddButton = new InlineKeyboardMarkup(new[]
                                    {
                                        new[] { InlineKeyboardButton.WithUrl(_loc.GetString(updated, "AddToGroupButton"), $"https://t.me/{me.Username}?startgroup=true") },
                                    });
                            await botClient.SendMessage(callback.Message.Chat.Id, _loc.GetString(updated, "MessageAfterChooseLang"), replyMarkup: AddButton, parseMode: ParseMode.Markdown, cancellationToken: ct);
                        }
                    }
                    catch {}
                    return;
                }
                
            }

            if (data.StartsWith("settings:toggle:lang:", StringComparison.OrdinalIgnoreCase))
            {
                var allowed = new[] { "en", "uk", "ru" };

                if (!_settingsCache.TryGet(chatId, out ChatSettingsEntity? CurrentSettings) || CurrentSettings == null)
                {
                    CurrentSettings = await _settingsService.GetOrCreateAsync(chatId, ct);
                    _settingsCache.Set(chatId, CurrentSettings);
                }

                string NewLang = GetNextLanguage(CurrentSettings.Language, allowed);
                
                ChatSettingsEntity updated;
                try
                {
                    updated = await _settingsService.SetLanguageAsync(chatId, NewLang, ct);
                    _settingsCache.Set(chatId, updated);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[SettingsCallbackHandler]: Failed to set language {Lang} for chat {Chat}", NewLang, chatId);
                    return;
                }

                var kb = BuildSettingsKeyboard(updated);

                try
                {
                    await botClient.EditMessageText(chatId, callback.Message!.MessageId, _loc.GetString(updated, "ChosenLanguage"), replyMarkup: kb, cancellationToken: ct);

                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[SettingsCallbackHandler]: Failed to update message/markup after language change for chat {Chat}", chatId);
                }
                return;
            }

            await botClient.AnswerCallbackQuery(callback.Id, await _loc.GetStringAsync(chatId, callback.Message, "UnknownCommand"), cancellationToken: ct);
        }

        private InlineKeyboardMarkup BuildSettingsKeyboard(ChatSettingsEntity settings)
        {
            var buttons = new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData($"{_loc.GetString(settings, "SpamProtectionButton")} {(settings.SpamProtectionEnabled ? "✅" : "❌")}", $"settings:toggle:spam") },
                new[] { InlineKeyboardButton.WithCallbackData($"{_loc.GetString(settings, "LinksAllowedButton")} {(settings.LinksAllowed ? "✅" : "❌")}", $"settings:toggle:links") },
                new[] { InlineKeyboardButton.WithCallbackData($"{_loc.GetString(settings, "LanguageButton")} {_loc.GetLanguageDisplayName(settings.Language)}", $"settings:toggle:lang:{settings.Language}") },
            };

            return new InlineKeyboardMarkup(buttons);
        }
        private static string GetNextLanguage(string? current, string[] allowed)
        {
            if (string.IsNullOrEmpty(current) || !allowed.Contains(current))
                return allowed[0];

            var idx = Array.FindIndex(allowed, l => l.Equals(current, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) return allowed[0];
            return allowed[(idx + 1) % allowed.Length];
        }
    }
}
