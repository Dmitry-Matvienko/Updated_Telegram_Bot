using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MyUpdatedBot.Core.Handlers.ReportHandlers
{
    public class AdminReportCallbackHandler : IButtonHandlers
    {
        private readonly ILogger<AdminReportCallbackHandler > _logger;

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

            if (!long.TryParse(parts[1], out var chatId) ||
                !int.TryParse(parts[2], out var messageId) ||
                !long.TryParse(parts[3], out var targetUserId))
            {
                await botClient.AnswerCallbackQuery(callback.Id, "Неправильный формат данных.", showAlert: true, cancellationToken: ct);
                return;
            }

            var action = parts[4];

            try
            {
                var member = await botClient.GetChatMember(chatId, callback.From.Id, ct);
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
                _logger.LogWarning(ex, "[AdminReportCallbackHandler]: Failed to verify admin rights {AdminId} in chat {ChatId}", callback.From.Id, chatId);
                await botClient.AnswerCallbackQuery(callback.Id, "Не удалось проверить ваши права.", showAlert: true, cancellationToken: ct);
                return;
            }

            switch (action)
            {
                case "ignore":
                    await botClient.AnswerCallbackQuery(callback.Id, "Пометка: игнорировано", showAlert: true, cancellationToken: ct);
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
                        await botClient.RestrictChatMember(chatId, targetUserId, permissions, untilDate: until, cancellationToken: ct);
                        await botClient.AnswerCallbackQuery(callback.Id, "Пользователь заглушён на 30 мин.", showAlert: true, cancellationToken: ct);
                        await botClient.SendMessage(chatId,
                            $"⏳ Пользователь [id{targetUserId}](tg://user?id={targetUserId}) заглушён на 30 мин. (по решению администратора)",
                            ParseMode.Markdown,
                            cancellationToken: ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[AdminReportCallbackHandler]: Failed to mute user {User} in chat {Chat}. Bot hasn't rights", targetUserId, chatId);
                        await botClient.AnswerCallbackQuery(callback.Id, "Не удалось заглушить пользователя. Проверьте права бота.", showAlert: true, cancellationToken: ct);
                    }
                    break;

                case "ban":
                    try
                    {
                        await botClient.BanChatMember(chatId, targetUserId, cancellationToken: ct);
                        await botClient.AnswerCallbackQuery(callback.Id, "Пользователь забанен.", showAlert: true, cancellationToken: ct);
                        await botClient.SendMessage(chatId,
                            $"⛔ Пользователь [id{targetUserId}](tg://user?id={targetUserId}) заблокирован (по решению администратора).",
                            ParseMode.Markdown, cancellationToken: ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[AdminReportCallbackHandler]: Failed to ban user {User} in chat {Chat}. Bot hasn't rights", targetUserId, chatId);
                        await botClient.AnswerCallbackQuery(callback.Id, "Не удалось забанить пользователя. Проверьте права бота.", showAlert: true, cancellationToken: ct);
                    }
                    break;

                default:
                    await botClient.AnswerCallbackQuery(callback.Id, "Неизвестное действие.", showAlert: true, cancellationToken: ct);
                    break;
            }
        }
    }
}
