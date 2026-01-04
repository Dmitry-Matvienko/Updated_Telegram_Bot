using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace MyUpdatedBot.Services.Cleanup
{
    public class CleanupHostedService : BackgroundService
    {
        private readonly IEnumerable<IPeriodicCleanup> _cleanups;
        private readonly ILogger<CleanupHostedService> _logger;
        private readonly int _tickMinutes;
        private readonly int _maxParallel;

        public CleanupHostedService(
            IEnumerable<IPeriodicCleanup> cleanups,
            ILogger<CleanupHostedService> logger,
            int tickMinutes = 10,
            int maxParallel = 4)
        {
            _cleanups = cleanups ?? Array.Empty<IPeriodicCleanup>();
            _logger = logger;
            _tickMinutes = Math.Max(1, tickMinutes);
            _maxParallel = Math.Max(1, maxParallel);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[CleanupHostedService]: started. Found {Count} cleanup tasks", _cleanups.Count());

            // next run times
            var nextRun = _cleanups.ToDictionary(c => c, c => DateTime.UtcNow);

            using var semaphore = new SemaphoreSlim(_maxParallel);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.UtcNow;
                    var due = nextRun.Where(kv => kv.Value <= now).Select(kv => kv.Key).ToList();

                    foreach (var taskInfo in due)
                    {
                        await semaphore.WaitAsync(stoppingToken).ConfigureAwait(false);

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var sw = Stopwatch.StartNew();
                                _logger.LogInformation("[CleanupHostedService]: {Name} started", taskInfo.Name);
                                await taskInfo.CleanupAsync(stoppingToken).ConfigureAwait(false);
                                sw.Stop();
                                _logger.LogInformation("[CleanupHostedService]: {Name} finished in {Elapsed}ms", taskInfo.Name, sw.ElapsedMilliseconds);
                            }
                            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "[CleanupHostedService]: {Name} failed", taskInfo.Name);
                            }
                            finally
                            {
                                nextRun[taskInfo] = DateTime.UtcNow.Add(taskInfo.Interval);
                                semaphore.Release();
                            }
                        }, CancellationToken.None);
                    }

                    await Task.Delay(TimeSpan.FromMinutes(_tickMinutes), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[CleanupHostedService]: loop error");
                    try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false); } catch { }
                }
            }

            _logger.LogInformation("[CleanupHostedService]: stopping");
        }
    }
}
