using MyUpdatedBot.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyUpdatedBot.Core.Handlers;
using MyUpdatedBot.Infrastructure.Data;
using Telegram.Bot;
using MyUpdatedBot.Infrastructure;
using MyUpdatedBot.Services.Stats;
using Serilog;
using Microsoft.Extensions.DependencyInjection;

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
            services.Configure<BotSettings>(ctx.Configuration.GetSection("TelegramBot"));

            services.AddSingleton<ITelegramBotClient>(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<BotSettings>>().Value;
                return new TelegramBotClient(opts.Token);
            });

            services.AddSingleton<MessageStatsService>();
            services.AddSingleton<IMessageStatsService>(sp =>
        sp.GetRequiredService<MessageStatsService>());
            // Hosted Service for counter of messages
            services.AddHostedService(sp =>
        sp.GetRequiredService<MessageStatsService>());

            services.AddDbContext<MyDbContext>(options =>
                options.UseSqlServer(ctx.Configuration.GetConnectionString("DefaultConnection")));

            // Hosted Service for polling
            services.AddHostedService<BotHostedService>();
            

            // Core handlers
            services.AddTransient<IUpdateHandlerService, UpdateDispatcher>();
            services.AddTransient<ICommandHandler, CountMessageHandler>();
            services.AddTransient<ICommandHandler, OptionalHandler>();
            services.AddTransient<ICommandHandler, MessageRateHandler>();

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