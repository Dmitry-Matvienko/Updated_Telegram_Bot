using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyUpdatedBot.Services.OwnerTools
{
    public interface IShowMemoryInfo
    {
        Task<string> ShowInfoAsync(CancellationToken ct = default);
    }
}
