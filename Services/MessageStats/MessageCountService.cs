using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyUpdatedBot.Core.Models.Entities;
using MyUpdatedBot.Infrastructure.Data;
using System.Threading.Channels;
using Telegram.Bot;

namespace MyUpdatedBot.Services.MessageStats
{
    /// <summary>
    /// Buffers message events and 
    /// periodically saves them to the DB in batches.
    /// </summary>
    public class MessageCountService : BackgroundService, IMessageCountStatsService
    {
        // Unlimited channel for chatId accumulation
        private readonly Channel<(long TgUserId, long ChatId, string? FirstName, string? Username)> _channel
    = Channel.CreateUnbounded<(long, long, string?, string?)>();
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ITelegramBotClient _botClient;
        private readonly ILogger<MessageCountService> _logger;

        private static readonly SortedDictionary<int, string> _ranks = new SortedDictionary<int, string>
        {
            { 0,     "Без ранга" },
            { 60,    "Новичок (lvl 1) 🔰" },
            { 350,   "Дилетант (lvl 2) 🔰" },
            { 2000,  "Люмпен-пролетарий (lvl 3) 👷‍♂️" },
            { 3500,  "Барыга (lvl 4) 😐" },
            { 5000,  "Сержант терпения (lvl 5) 🗿" },
            { 7000,  "Местный гопник (lvl 6) 🦾" },
            { 9500,  "Средний класс (lvl 7) 👮🏻‍♂️" },
            { 12000, "Интеллигенция (lvl 8) 🎖" },
            { 15000, "Боярин (lvl 9) 👨🏻‍⚖️" },
            { 18000, "Элитный диванный стратег (lvl 10) 🛋" },
            { 22000, "Вершитель судеб (lvl 11) ✝️" },
            { 26000, "Гражданин мира (lvl 12) 🌎" },
            { 30000, "Продвинутый терпилон (lvl 13) 🔥" },
            { 35000, "Терпиларожденный (lvl 14) 🏆" }
        };

        public MessageCountService(IServiceScopeFactory scopeFactory, ITelegramBotClient botClient, ILogger<MessageCountService> logger)
        {
            _scopeFactory = scopeFactory;
            _botClient = botClient;
            _logger = logger;
        }
        public void EnqueueMessage(long userTelegramId, long chatId, string? userFirstName, string? tgUserName)
        {
            _channel.Writer.TryWrite((userTelegramId, chatId, userFirstName, tgUserName));
        }
        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            _logger.LogInformation("[MessageStatsService]: Start service");
            var counter = new Dictionary<(long TgUserId, long ChatId), int>(); // Necessary for the number of messages
            var profileMap = new Dictionary<long, (string? FirstName, string? Username)>(); // Necessary for user data

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    while (_channel.Reader.TryRead(out var tuple))
                    {
                        // Count by chat
                        counter[(tuple.TgUserId, tuple.ChatId)] = counter.GetValueOrDefault((tuple.TgUserId, tuple.ChatId)) + 1;

                        // Remember for this user
                        profileMap[tuple.TgUserId] = (tuple.FirstName, tuple.Username);
                    }

                    if (counter.Count > 0)
                    {
                        _logger.LogDebug("[MessageStatsService]: {Items} elements of statistic is wtiring ti DB…", counter.Count);
                        using var scope = _scopeFactory.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<MyDbContext>();

                        //Batch registration of new users
                        var tgIds = profileMap.Keys.ToArray();   // All encountered Telegram IDs
                        var existIds = await db.Users
                            .Where(u => tgIds.Contains(u.UserId))
                            .Select(u => u.UserId)
                            .ToArrayAsync(ct);

                        // Recieved only those who we don't have
                        var newIds = tgIds.Except(existIds);

                        var newUsers = newIds.Select(id =>
                        {
                            var (first, user) = profileMap[id];
                            return new UserEntity
                            {
                                UserId = id,
                                FirstName = first,
                                Username = user,
                                CreatedAt = DateTime.UtcNow
                            };
                        }).ToList();

                        if (newUsers.Any())
                        {
                            _logger.LogInformation("[MessageStatsService]: Registering {Count} new users: {UserName} - {UserId}",
                                newUsers.Count, newUsers.Select(f => f.FirstName), newUsers.Select(u => u.UserId));
                            db.Users.AddRange(newUsers);
                            await db.SaveChangesAsync(ct);
                        }

                        foreach (var ((tgUserId, chatId), cnt) in counter)
                        {
                            _logger.LogDebug("[MessageStatsService]: Updating statistic: user {UserId} in chat {ChatId} add {Count} messages", tgUserId, chatId, cnt);
                            var user = await db.Users.SingleOrDefaultAsync(u => u.UserId == tgUserId, ct);
                            if (user is null) continue;

                            // Searching for an existing record for this chat
                            var stat = await db.MessageStats
                                .SingleOrDefaultAsync(ms =>
                                    ms.UserRefId == user.Id
                                 && ms.ChatId == chatId,
                                 ct);

                            var oldCount = stat?.MessageCount ?? 0;
                            var newCount = oldCount + cnt;
                            var newRank = GetRank(newCount);
                            var oldRank = GetRank(oldCount);

                            if (stat is null)
                            {
                                db.MessageStats.Add(new MessageCountEntity
                                {
                                    UserRefId = user.Id,
                                    ChatId = chatId,
                                    Rank = "Без ранга",
                                    MessageCount = cnt
                                });
                            }
                            else
                            {
                                stat.MessageCount = newCount;
                                stat.Rank = newRank;
                            }

                            if (newRank != oldRank)
                            {
                                _logger.LogInformation("[MessageStatsService]: User: {User} upgraded from {Old} to {New}", tgUserId, oldRank, newRank);
                                // congratulations on up rank
                                await _botClient.SendMessage(
                                    chatId: chatId,
                                    text: $"🎉 [{user.FirstName}](tg://user?id={tgUserId}), поздравляю! Ты получили новый ранг: *{newRank}* за {stat?.MessageCount} сообщений!",
                                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                                    disableNotification: false,
                                    cancellationToken: ct
                                );
                            }
                            _logger.LogInformation("[MessageStatsService]: Push to DB ChatId: {chatid}, UserRefId: {UserRefId}, Count: {count}", tgUserId, user.Id, cnt);
                        }

                        await db.SaveChangesAsync(ct);
                        counter.Clear();
                        profileMap.Clear();
                    }
                }
                catch (Exception ex) { _logger.LogError(ex, "[MessageStatsService]: unexpected error in ExecuteAsync"); }

                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
        private string GetRank(int count)
        {
            // select all thresholds <= count, obtain the maximum and safely take the value
            var threshold = _ranks.Keys
                .Where(t => t <= count)
                .DefaultIfEmpty(_ranks.Keys.Min())
                .Max();

            return _ranks.TryGetValue(threshold, out var rank)
                ? rank
                : _ranks[_ranks.Keys.Min()];
        }
    }
}