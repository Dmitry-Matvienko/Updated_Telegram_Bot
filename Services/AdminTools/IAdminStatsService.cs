using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyUpdatedBot.Services.AdminPanel
{
    public interface IAdminStatsService
    {
        Task<int> GetTotalUsersAsync(CancellationToken ct);
    }
}
