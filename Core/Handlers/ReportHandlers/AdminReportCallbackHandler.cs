using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using MyUpdatedBot.Cache.ReportsStore;
using MyUpdatedBot.Core.Localization;

namespace MyUpdatedBot.Core.Handlers.ReportHandlers
{
    public class AdminReportCallbackHandler : IButtonHandlers
    {
        private readonly ILogger<AdminReportCallbackHandler > _logger;
        private readonly IReportsProcessedStore _processedStore;
        private readonly LocalizationUtil _loc;

        public AdminReportCallbackHandler (ILogger<AdminReportCallbackHandler > logger, IReportsProcessedStore processedStore, LocalizationUtil loc)
        {
            _logger = logger;
            _processedStore = processedStore;
            _loc = loc;
        }

        public bool CanHandle(CallbackQuery callback)
            => !string.IsNullOrEmpty(callback.Data) && callback.Data.StartsWith("compl:");

        public async Task HandleAsync(ITelegramBotClient botClient, CallbackQuery callback, CancellationToken ct)
        {
            var FromChatId = callback.Message!.Chat.Id;
            // data: compl:{chatId}:{messageId}:{targetUserId}:{action}
            var parts = (callback.Data ?? "").Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 5)
            {
                await botClient.AnswerCallbackQuery(callback.Id, await _loc.GetStringAsync(FromChatId, callback.Message, "WrongData"), showAlert: true, cancellationToken: ct);
                return;
            }

            if (!long.TryParse(parts[1], out var sourceChatId) ||
                !int.TryParse(parts[2], out var messageId) ||
                !long.TryParse(parts[3], out var targetUserId))
            {
                await botClient.AnswerCallbackQuery(callback.Id, await _loc.GetStringAsync(FromChatId, callback.Message, "WrongData"), showAlert: true, cancellationToken: ct);
                return;
            }

            var action = parts[4];

            try
            {
                var member = await botClient.GetChatMember(sourceChatId, callback.From.Id, ct);
                var isAdmin = member.Status == ChatMemberStatus.Administrator
                           || member.Status == ChatMemberStatus.Creator;
                if (!isAdmin)
                {
                    await botClient.AnswerCallbackQuery(callback.Id, await _loc.GetStringAsync(FromChatId, callback.Message, "NotAdmin"), showAlert: true, cancellationToken: ct);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AdminReportCallbackHandler]: Failed to verify admin rights {AdminId} in chat {ChatId}", callback.From.Id, sourceChatId);
                await botClient.AnswerCallbackQuery(callback.Id, await _loc.GetStringAsync(FromChatId, callback.Message, "CantCheckRights"), showAlert: true, cancellationToken: ct);
                return;
            }

            var complaintKey = (sourceChat: sourceChatId, sourceMessageId: messageId, targetUser: targetUserId);

            // Notify if complaint has already processed
            if (_processedStore.TryGet(complaintKey, out var existing))
            {
                await botClient.AnswerCallbackQuery(callback.Id,
                    $"{await _loc.GetStringAsync(FromChatId, callback.Message, "ComplaintProcessed")} {existing.AdminName}: {existing.Action}",
                    showAlert: true, cancellationToken: ct);

                await MarkAdminMessageProcessedAsync(botClient, callback, ct, existing);
                return;
            }

            var adminName = callback.From.FirstName ?? callback.From.Username ?? callback.From.Id.ToString();
            var info = new ProcessedInfo(action, callback.From.Id, adminName, DateTime.UtcNow);

            if (!_processedStore.TryAdd(complaintKey, info))
            {
                _processedStore.TryGet(complaintKey, out var info2);
                await botClient.AnswerCallbackQuery(callback.Id,
                    $"{await _loc.GetStringAsync(FromChatId, callback.Message, "ComplaintProcessed")} {info2?.AdminName}: {info2?.Action}",
                    showAlert: true, cancellationToken: ct);

                await MarkAdminMessageProcessedAsync(botClient, callback, ct, info2 ?? info);
                return;
            }

            try
            {
                switch (action)
                {
                    case "ignore":
                        await botClient.AnswerCallbackQuery(callback.Id, await _loc.GetStringAsync(FromChatId, callback.Message, "ComplaintIgnored"), showAlert: true, cancellationToken: ct);
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

                            await botClient.AnswerCallbackQuery(callback.Id, await _loc.GetStringAsync(FromChatId, callback.Message, "UserMuted"), showAlert: true, cancellationToken: ct);
                            await botClient.SendMessage(sourceChatId,
                                $"⏳ [id{targetUserId}](tg://user?id={targetUserId}) {await _loc.GetStringAsync(FromChatId, callback.Message, "UserMutedByAdmin")}",
                                ParseMode.Markdown,
                                cancellationToken: ct);

                            await MarkAdminMessageProcessedAsync(botClient, callback, ct, info);
                        }
                        catch (Exception ex)
                        {
                            _processedStore.TryRemove(complaintKey, out _);
                            _logger.LogError(ex, "[AdminReportCallbackHandler]: Failed to mute user {User} in chat {Chat}. Bot hasn't rights", targetUserId, sourceChatId);
                            await botClient.AnswerCallbackQuery(callback.Id, await _loc.GetStringAsync(FromChatId, callback.Message, "BotCouldntMuteUser"), showAlert: true, cancellationToken: ct);
                        }
                        break;

                    case "ban":
                        try
                        {
                            await botClient.BanChatMember(sourceChatId, targetUserId, cancellationToken: ct);
                            await botClient.AnswerCallbackQuery(callback.Id, await _loc.GetStringAsync(FromChatId, callback.Message, "UserBanned"), showAlert: true, cancellationToken: ct);
                            await botClient.SendMessage(sourceChatId,
                                $"⛔ [id{targetUserId}](tg://user?id={targetUserId}) {await _loc.GetStringAsync(FromChatId, callback.Message, "UserBannedByAdmin")}",
                                ParseMode.Markdown, cancellationToken: ct);

                            await MarkAdminMessageProcessedAsync(botClient, callback, ct, info);
                        }
                        catch (Exception ex)
                        {
                            _processedStore.TryRemove(complaintKey, out _);
                            _logger.LogError(ex, "[AdminReportCallbackHandler]: Failed to ban user {User} in chat {Chat}. Bot hasn't rights", targetUserId, sourceChatId);
                            await botClient.AnswerCallbackQuery(callback.Id, await _loc.GetStringAsync(FromChatId, callback.Message, "BotCouldntBanUser"), showAlert: true, cancellationToken: ct);
                        }
                        break;

                    default:
                        _processedStore.TryRemove(complaintKey, out _);
                        await botClient.AnswerCallbackQuery(callback.Id, await _loc.GetStringAsync(FromChatId, callback.Message, "UnknownCommand"), showAlert: true, cancellationToken: ct);
                        break;
                }
            }
            catch (Exception ex)
            {
                _processedStore.TryRemove(complaintKey, out _);
                _logger.LogError(ex, "[AdminReportCallbackHandler]: Unexpected error while processing complaint {Key}", complaintKey);
                await botClient.AnswerCallbackQuery(callback.Id, await _loc.GetStringAsync(FromChatId, callback.Message, "ErrorOccured"), showAlert: true, cancellationToken: ct);
            }
        }

        private async Task MarkAdminMessageProcessedAsync(ITelegramBotClient botClient, CallbackQuery callback, CancellationToken ct, ProcessedInfo info)
        {
            try
            {
                if (callback.Message == null)
                {
                    await botClient.AnswerCallbackQuery(callback.Id, "✅", showAlert: false, cancellationToken: ct);
                    return;
                }

                var chatId = callback.Message.Chat.Id;
                var messageId = callback.Message.MessageId;
                var originalText = callback.Message.Text ?? callback.Message.Caption ?? "";

                var adminLabel = string.IsNullOrWhiteSpace(info.AdminName)
                                 ? info.AdminId.ToString()
                                 : info.AdminName;

                var time = (int)(DateTime.UtcNow - info.When).TotalMinutes;

                var processedText = originalText + $"\n\n✅ {await _loc.GetStringAsync(chatId, callback.Message, "ProcessedByAdmin")} [{adminLabel}](tg://user?id={info.AdminId})" +
                    $" {time} {await _loc.GetStringAsync(chatId, callback.Message, "ChosenAction")} **{info.Action}**";

                await botClient.EditMessageText(chatId: chatId, messageId: messageId, processedText, parseMode: ParseMode.Markdown, cancellationToken: ct);
            }
            catch
            {
                try { await botClient.AnswerCallbackQuery(callback.Id, "✅", showAlert: false, cancellationToken: ct); } catch { }
            }
        }


    }
}
