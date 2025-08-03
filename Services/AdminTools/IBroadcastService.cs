using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyUpdatedBot.Services.AdminPanel
{
    public interface IBroadcastService
    {
        Task BroadcastAsync(string text, CancellationToken ct);
    }
}
