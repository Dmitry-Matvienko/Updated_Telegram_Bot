using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyUpdatedBot.Cache.SpamStore;
using MyUpdatedBot.Infrastructure.Data;

namespace MyUpdatedBot.Services.Cleanup
{
    public class WarningCleanup : IPeriodicCleanup
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<WarningCleanup> _logger;
        public string Name => "WarningCleanup";
        public TimeSpan Interval { get; } = TimeSpan.FromMinutes(1); // how often to call this task

        private static readonly TimeSpan Window = TimeSpan.FromMinutes(3); // remove warn if it;s older than this time
        private const int BatchSize = 200;
        private const int MaxRetries = 3;

        public WarningCleanup(IServiceScopeFactory scopeFactory, ILogger<WarningCleanup> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task CleanupAsync(CancellationToken cancellationToken)
        {
            var cutoff = DateTime.UtcNow - Window;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<MyDbContext>();
                var spamStore = scope.ServiceProvider.GetService<IFloodStore>();

                while (!cancellationToken.IsCancellationRequested)
                {
                    var items = await db.WarningRecords
                        .Include(w => w.User)
                        .Where(w => w.WarningsCount > 0 && w.CreatedAtUtc <= cutoff)
                        .OrderBy(w => w.CreatedAtUtc)
                        .Take(BatchSize)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);

                    if (items.Count == 0) break;

                    foreach (var rec in items)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var attempt = 0;

                        // rtry if concurrency
                        while (true)
                        {
                            attempt++;
                            try
                            {
                                var curNow = DateTime.UtcNow;
                                var diff = curNow - rec.CreatedAtUtc;
                                var expired = (int) (diff.Ticks / Window.Ticks);
                                if (expired <= 0)
                                {
                                    // someone has already updated
                                    break;
                                }

                                var newCount = Math.Max(0, rec.WarningsCount - expired);
                                rec.WarningsCount = newCount;
                                rec.CreatedAtUtc = curNow;

                                db.WarningRecords.Update(rec);
                                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                                // update cache if exist
                                if (spamStore != null && rec.User != null)
                                {
                                    spamStore.SetCachedWarningsCount(rec.ChatId, rec.User.UserId, rec.WarningsCount);
                                }

                                break;
                            }
                            catch (DbUpdateConcurrencyException ex) when (attempt < MaxRetries)
                            {
                                _logger.LogWarning(ex, "[WarningCleanup]: Concurrency while cleaning warning {Id}, retry {Attempt}", rec.Id, attempt);
                                //try to refresh rec from db
                                try
                                {
                                    var fresh = await db.WarningRecords
                                        .Include(w => w.User)
                                        .FirstOrDefaultAsync(w => w.Id == rec.Id, cancellationToken)
                                        .ConfigureAwait(false);
                                    if (fresh == null) break;
                                    rec.WarningsCount = fresh.WarningsCount;
                                    rec.CreatedAtUtc = fresh.CreatedAtUtc;
                                }
                                catch { }

                                await Task.Delay(50 * attempt, cancellationToken).ConfigureAwait(false);
                                continue;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "[WarningCleanup]: Failed to clean warning {Id}", rec.Id);
                                break;
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WarningCleanup]: WarningCleanup failed");
            }
        }
    }
}
