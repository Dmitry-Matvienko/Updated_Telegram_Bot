using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MyUpdatedBot.Cache;
using MyUpdatedBot.Cache.ChatSettingsStore;
using MyUpdatedBot.Cache.ReportsStore;
using MyUpdatedBot.Core.Handlers;
using MyUpdatedBot.Core.Handlers.ChatSettingsHandlers;
using MyUpdatedBot.Core.Handlers.CrocodileHandlers;
using MyUpdatedBot.Core.Handlers.ReportHandlers;
using MyUpdatedBot.Core.Handlers.RollGameHandlers;
using MyUpdatedBot.Core.Models;
using MyUpdatedBot.Infrastructure;
using MyUpdatedBot.Infrastructure.Data;
using MyUpdatedBot.Services.ChatSettings;
using MyUpdatedBot.Services.Cleanup;
using MyUpdatedBot.Services.CrocodileGame;
using MyUpdatedBot.Services.MessageStats;
using MyUpdatedBot.Services.OwnerTools;
using MyUpdatedBot.Services.RollGame;
using MyUpdatedBot.Services.SpamProtection;
using MyUpdatedBot.Services.UserLeaderboard;
using MyUpdatedBot.Services.UserReputation;
using Serilog;
using Telegram.Bot;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

bool useSecrets = configuration.GetValue<bool>("UseUserSecrets");

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .Enrich.FromLogContext()
    .CreateLogger(); // To see any logs before CreateDefaultBuilder

try
{
    Log.Information("Starting bot host");

    await Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((ctx, cfg) =>
            {
                cfg.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                if(useSecrets) cfg.AddUserSecrets<Program>(optional: true, reloadOnChange: true); // Connect "User Secrets" where are sensitive data
                cfg.AddEnvironmentVariables();
            }).UseSerilog((ctx, services, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .ReadFrom.Services(services))
        .ConfigureServices((ctx, services) =>
        {
            services.Configure<BotSettings>(ctx.Configuration.GetSection("TelegramBot"))
                    .Configure<OwnerSettings>(ctx.Configuration.GetSection("Admin"));

            // Telegram-client
            services.AddSingleton<ITelegramBotClient>(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<BotSettings>>().Value;
                return new TelegramBotClient(opts.Token);
            });

            // Dictionary of words and service of the game “Crocodile”
            services.AddSingleton<ICrocodileService, CrocodileService>();
            services.AddSingleton<WordRepository>();

            // Roll game
            services.AddSingleton<IRollService, RollService>();

            // Message statistics service and hosted service for counter of messages
            services.AddSingleton<MessageCountService>();
            services.AddSingleton<IMessageCountStatsService>(sp => sp.GetRequiredService<MessageCountService>());
            services.AddHostedService(sp => sp.GetRequiredService<MessageCountService>());

            // EF Core
            services.AddDbContext<MyDbContext>(options =>
               options.UseSqlServer(ctx.Configuration.GetConnectionString("DefaultConnection")));

            // Hosted Service for polling
            services.AddHostedService<BotHostedService>();

            // Cleanup hosted service
            services.AddHostedService<CleanupHostedService>();
            services.AddSingleton<IPeriodicCleanup, WarningCleanup>();

            //Cache
            services.AddMemoryCache();
            services.AddSingleton<IUserReportsThrottleStore, UserReportsThrottleStore>();
            services.AddSingleton<IReportsProcessedStore, ReportsProcessedStore>();
            services.AddSingleton<ISpamStore, SpamStore>();
            services.AddSingleton<IChatSettingsStore, ChatSettingsStore>();

            // Other scoped-services
            services.AddScoped<IReputationService, ReputationService>();
            services.AddScoped<IUserLeaderboardService, UserLeaderboardService>();
            services.AddScoped<IBroadcastService, BroadcastService>();
            services.AddScoped<IUserStatsService, UserStatsService>();
            services.AddScoped<IWarning, WarningService>();
            services.AddScoped<IChatSettingsService, ChatSettingsService>();

            // Other singleton-services
            services.AddSingleton<IShowMemoryInfo, ShowMemoryInfo>();

            // Core handlers
            services.AddTransient<IUpdateHandlerService, UpdateDispatcher>();

            // Message handlers
            services.AddTransient<IMessageHandler, CountMessageHandler>();
            services.AddTransient<IMessageHandler, SpamMessageHandler>();
            services.AddTransient<IMessageHandler, CrocodileGuessHandler>();
            services.AddTransient<IMessageHandler, LinkDetectionHandler>();
            services.AddTransient<IMessageHandler, UserReportHandler>();

            // Command handlers
            services.AddTransient<ICommandHandler, TopMessageCountHandler>();
            services.AddTransient<ICommandHandler, ReputationHandler>();
            services.AddTransient<ICommandHandler, OptionalHandler>();
            services.AddTransient<ICommandHandler, CrocodileHandler>();
            services.AddTransient<ICommandHandler, OwnerCommandHandler>();
            services.AddTransient<ICommandHandler, RollGameHandler>();
            services.AddTransient<ICommandHandler, ChatSettingsHandler>();

            // Button handlers
            services.AddTransient<IButtonHandlers, CrocodileButtonHandler>();
            services.AddTransient<IButtonHandlers, RollGameButtonHandler>();
            services.AddTransient<IButtonHandlers, AdminReportCallbackHandler>();
            services.AddTransient<IButtonHandlers, SettingsCallbackHandler>();

        })
        .Build()
        .RunAsync();
}
catch (Exception ex)  { Log.Fatal(ex, "Host terminated unexpectedly"); }
finally { Log.CloseAndFlush(); }