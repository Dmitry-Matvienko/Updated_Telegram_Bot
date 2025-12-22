using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace MyUpdatedBot.Core.Handlers.ReportHandlers
{
    public class AdminReportCallbackHandler : IButtonHandlers
    {
        private readonly ILogger<AdminReportCallbackHandler > _logger;
        private static readonly ConcurrentDictionary<(long sourceChat, int sourceMessageId, long targetUser), ProcessedInfo> _processed = new();
        record ProcessedInfo(string Action, long AdminId, string AdminName, DateTime When);

        public AdminReportCallbackHandler (ILogger<AdminReportCallbackHandler > logger)
        {
            _logger = logger;
        }

        public bool CanHandle(CallbackQuery callback)
            => !string.IsNullOrEmpty(callback.Data) && callback.Data.StartsWith("compl:");

        public async Task HandleAsync(ITelegramBotClient botClient, CallbackQuery callback, CancellationToken ct)
        {
            // data: compl:{chatId}:{messageId}:{targetUserId}:{action}
            var parts = (callback.Data ?? "").Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 5)
            {
                await botClient.AnswerCallbackQuery(callback.Id, "Неправильные данные.", showAlert: true, cancellationToken: ct);
                return;
            }

            if (!long.TryParse(parts[1], out var sourceChatId) ||
                !int.TryParse(parts[2], out var messageId) ||
                !long.TryParse(parts[3], out var targetUserId))
            {
                await botClient.AnswerCallbackQuery(callback.Id, "Неправильный формат данных.", showAlert: true, cancellationToken: ct);
                return;
            }

            var action = parts[4];

            try
            {
                var member = await botClient.GetChatMember(sourceChatId, callback.From.Id, ct);
                var isAdmin = member.Status == Telegram.Bot.Types.Enums.ChatMemberStatus.Administrator
                           || member.Status == Telegram.Bot.Types.Enums.ChatMemberStatus.Creator;
                if (!isAdmin)
                {
                    await botClient.AnswerCallbackQuery(callback.Id, "Вы не администратор этого чата.", showAlert: true, cancellationToken: ct);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AdminReportCallbackHandler]: Failed to verify admin rights {AdminId} in chat {ChatId}", callback.From.Id, sourceChatId);
                await botClient.AnswerCallbackQuery(callback.Id, "Не удалось проверить ваши права.", showAlert: true, cancellationToken: ct);
                return;
            }

            var complaintKey = (sourceChat: sourceChatId, sourceMessageId: messageId, targetUser: targetUserId);

            // Notify if complaint has already processed
            if (_processed.TryGetValue(complaintKey, out var alreadyAction))
            {
                await botClient.AnswerCallbackQuery(callback.Id,
                    $"Жалоба уже обработана ({alreadyAction.Action}) админом {alreadyAction.AdminName} в {alreadyAction.When}.",
                    showAlert: true, cancellationToken: ct);

                await MarkAdminMessageProcessedAsync(botClient, callback, ct, alreadyAction);
                return;
            }

            var adminName = callback.From.FirstName ?? callback.From.Username ?? callback.From.Id.ToString();
            var info = new ProcessedInfo(action, callback.From.Id, adminName, DateTime.UtcNow);

            if (!_processed.TryAdd(complaintKey, info))
            {
                _processed.TryGetValue(complaintKey, out var existingInfo);
                await botClient.AnswerCallbackQuery(callback.Id, $"Жалоба уже обработана ({existingInfo}).", showAlert: true, cancellationToken: ct);
                await MarkAdminMessageProcessedAsync(botClient, callback, ct, existingInfo);
                return;
            }

            try
            {
                switch (action)
                {
                    case "ignore":
                        await botClient.AnswerCallbackQuery(callback.Id, "Жалоба проигнорирована", showAlert: true, cancellationToken: ct);
                        await MarkAdminMessageProcessedAsync(botClient, callback, ct, info);
                        break;

                    case "mute30":
                        try
                        {
                            var until = DateTime.UtcNow.AddMinutes(30);
                            var permissions = new Telegram.Bot.Types.ChatPermissions
                            {
                                CanSendMessages = false,
                                CanSendOtherMessages = false,
                            };
                            await botClient.RestrictChatMember(sourceChatId, targetUserId, permissions, untilDate: until, cancellationToken: ct);

                            await botClient.AnswerCallbackQuery(callback.Id, "Пользователь заглушён на 30 минут.", showAlert: true, cancellationToken: ct);
                            await botClient.SendMessage(sourceChatId,
                                $"⏳ Пользователь [id{targetUserId}](tg://user?id={targetUserId}) заглушён на 30 минут (по решению администратора).",
                                ParseMode.Markdown,
                                cancellationToken: ct);

                            await MarkAdminMessageProcessedAsync(botClient, callback, ct, info);
                        }
                        catch (Exception ex)
                        {
                            _processed.TryRemove(complaintKey, out _);
                            _logger.LogError(ex, "[AdminReportCallbackHandler]: Failed to mute user {User} in chat {Chat}. Bot hasn't rights", targetUserId, sourceChatId);
                            await botClient.AnswerCallbackQuery(callback.Id, "Не удалось заглушить пользователя. Проверьте права бота.", showAlert: true, cancellationToken: ct);
                        }
                        break;

                    case "ban":
                        try
                        {
                            await botClient.BanChatMember(sourceChatId, targetUserId, cancellationToken: ct);
                            await botClient.AnswerCallbackQuery(callback.Id, "Пользователь забанен.", showAlert: true, cancellationToken: ct);
                            await botClient.SendMessage(sourceChatId,
                                $"⛔ Пользователь [id{targetUserId}](tg://user?id={targetUserId}) заблокирован (по решению администратора).",
                                ParseMode.Markdown, cancellationToken: ct);

                            await MarkAdminMessageProcessedAsync(botClient, callback, ct, info);
                        }
                        catch (Exception ex)
                        {
                            _processed.TryRemove(complaintKey, out _);
                            _logger.LogError(ex, "[AdminReportCallbackHandler]: Failed to ban user {User} in chat {Chat}. Bot hasn't rights", targetUserId, sourceChatId);
                            await botClient.AnswerCallbackQuery(callback.Id, "Не удалось забанить пользователя. Проверьте права бота.", showAlert: true, cancellationToken: ct);
                        }
                        break;

                    default:
                        _processed.TryRemove(complaintKey, out _);
                        await botClient.AnswerCallbackQuery(callback.Id, "Неизвестное действие.", showAlert: true, cancellationToken: ct);
                        break;
                }
            }
            catch (Exception ex)
            {
                _processed.TryRemove(complaintKey, out _);
                _logger.LogError(ex, "[AdminReportCallbackHandler]: Unexpected error while processing complaint {Key}", complaintKey);
                await botClient.AnswerCallbackQuery(callback.Id, "Ошибка при обработке. Попробуйте снова.", showAlert: true, cancellationToken: ct);
            }
        }

        private static async Task MarkAdminMessageProcessedAsync(ITelegramBotClient botClient, CallbackQuery callback, CancellationToken ct, ProcessedInfo info)
        {
            try
            {
                if (callback.Message == null)
                {
                    await botClient.AnswerCallbackQuery(callback.Id, "Готово.", showAlert: false, cancellationToken: ct);
                    return;
                }

                var chatId = callback.Message.Chat.Id;
                var messageId = callback.Message.MessageId;
                var originalText = callback.Message.Text ?? callback.Message.Caption ?? "";

                var adminLabel = string.IsNullOrWhiteSpace(info.AdminName)
                                 ? info.AdminId.ToString()
                                 : info.AdminName;

                var time = (int)(DateTime.UtcNow - info.When).TotalMinutes;

                var processedText = originalText + $"\n\n✅ Обработано админом [{adminLabel}](tg://user?id={info.AdminId})" +
                    $" {time} минут назад\nВыбранное действие: **{info.Action}**";

                await botClient.EditMessageText(chatId: chatId, messageId: messageId, processedText, parseMode: ParseMode.Markdown, cancellationToken: ct);
            }
            catch
            {
                try { await botClient.AnswerCallbackQuery(callback.Id, "Готово.", showAlert: false, cancellationToken: ct); } catch { }
            }
        }


    }
}
