using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyUpdatedBot.Core.Handlers;
using MyUpdatedBot.Core.Handlers.CrocodileHandlers;
using MyUpdatedBot.Core.Models;
using MyUpdatedBot.Infrastructure;
using MyUpdatedBot.Infrastructure.Data;
using MyUpdatedBot.Services.AdminPanel;
using MyUpdatedBot.Services.CrocodileGame;
using MyUpdatedBot.Services.Rating;
using MyUpdatedBot.Services.Stats;
using MyUpdatedBot.Services.UserLeaderboard;
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
                    .Configure<AdminSettings>(ctx.Configuration.GetSection("Admin"));

            services.AddSingleton<ITelegramBotClient>(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<BotSettings>>().Value;
                return new TelegramBotClient(opts.Token);
            });

            services.AddSingleton<MessageStatsService>();
            services.AddSingleton<IMessageStatsService>(sp =>
        sp.GetRequiredService<MessageStatsService>());

            services.AddSingleton<ICrocodileService, CrocodileService>();
            services.AddSingleton<WordRepository>();
            // Hosted Service for counter of messages
            services.AddHostedService(sp =>
        sp.GetRequiredService<MessageStatsService>());

            services.AddDbContext<MyDbContext>(options =>
                options.UseSqlServer(ctx.Configuration.GetConnectionString("DefaultConnection")));

            // Hosted Service for polling
            services.AddHostedService<BotHostedService>();

            // Other services
            services.AddScoped<IRatingService, RatingService>();
            services.AddScoped<IUserLeaderboardService, UserLeaderboardService>();
            services.AddScoped<IBroadcastService, BroadcastService>();
            services.AddScoped<IAdminStatsService, AdminStatsService>();

            // Core handlers
            services.AddTransient<IUpdateHandlerService, UpdateDispatcher>();
            services.AddTransient<ICommandHandler, CountMessageHandler>();
            services.AddTransient<ICommandHandler, AdminCommandHandler>();
            services.AddTransient<ICommandHandler, OptionalHandler>();
            services.AddTransient<ICommandHandler, CrocodileHandler>();
            services.AddTransient<ICommandHandler, CrocodileGuessHandler>();
            services.AddTransient<ICommandHandler, MessageRateHandler>();
            services.AddTransient<ICommandHandler, RatingHandler>();
            services.AddTransient<IButtonHandlers, CrocodileButtonHandler>();

        })
        .ConfigureLogging(log =>
        {
            log.ClearProviders();
            log.AddConsole();
        })
        .Build()
        .RunAsync();
}
catch (Exception ex)  { Log.Fatal(ex, "Host terminated unexpectedly"); }
finally { Log.CloseAndFlush(); }