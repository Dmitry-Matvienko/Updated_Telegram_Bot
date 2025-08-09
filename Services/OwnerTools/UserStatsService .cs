using Microsoft.EntityFrameworkCore;
using MyUpdatedBot.Infrastructure.Data;

namespace MyUpdatedBot.Services.OwnerTools
{
    public class UserStatsService : IUserStatsService
    {
        private readonly MyDbContext _db;
        public UserStatsService(MyDbContext db) => _db = db;

        public Task<int> GetTotalUsersAsync(CancellationToken ct)
            => _db.Users.CountAsync(ct);
    }
}
