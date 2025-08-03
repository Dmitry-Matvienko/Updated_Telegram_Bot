using Microsoft.EntityFrameworkCore;
using MyUpdatedBot.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyUpdatedBot.Services.AdminPanel
{
    public class AdminStatsService : IAdminStatsService
    {
        private readonly MyDbContext _db;
        public AdminStatsService(MyDbContext db) => _db = db;

        public Task<int> GetTotalUsersAsync(CancellationToken ct)
            => _db.Users.CountAsync(ct);
    }
}
