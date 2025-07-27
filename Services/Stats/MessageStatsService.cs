using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyUpdatedBot.Core.Models.Entities;
using MyUpdatedBot.Infrastructure.Data;
using System.Collections.Concurrent;
using System.Threading.Channels;
using Telegram.Bot;

namespace MyUpdatedBot.Services.Stats
{
    /// <summary>
    /// Buffers message events and 
    /// periodically saves them to the DB in batches.
    /// </summary>
    public class MessageStatsService : BackgroundService, IMessageStatsService
    {
        // Unlimited channel for chatId accumulation
        private readonly Channel<(long TgUserId, long ChatId, string? FirstName, string? Username)> _channel
    = Channel.CreateUnbounded<(long, long, string?, string?)>();
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ITelegramBotClient _botClient;
        private readonly ILogger<MessageStatsService> _logger;

        public MessageStatsService(IServiceScopeFactory scopeFactory, ITelegramBotClient botClient, ILogger<MessageStatsService> logger)
        {
            _scopeFactory = scopeFactory;
            _botClient = botClient;
            _logger = logger;
        }
        public void EnqueueMessage(long userTelegramId, long chatId, string? userFirstName, string? tgUserName)
        {
            _channel.Writer.TryWrite((userTelegramId, chatId, userFirstName, tgUserName));
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[MessageStatsService]: Start");
        }
        
    }
}