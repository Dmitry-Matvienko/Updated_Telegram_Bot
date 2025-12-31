using System.Diagnostics;
using System.Text;

namespace MyUpdatedBot.Services.OwnerTools
{
    public class ShowMemoryInfo : IShowMemoryInfo
    {
        private readonly double _warningThresholdMb = 800;

        public Task<string> ShowInfoAsync(CancellationToken ct = default)
        {
            var sb = new StringBuilder();
            var now = DateTime.UtcNow;

            // Process
            using var proc = Process.GetCurrentProcess();
            var startTimeUtc = proc.StartTime.ToUniversalTime();
            var uptime = now - startTimeUtc;

            // GC / managed memory
            long managedBytes = GC.GetTotalMemory(false);
            var gcInfo = GC.GetGCMemoryInfo();
            var gen0 = GC.CollectionCount(0);
            var gen1 = GC.CollectionCount(1);
            var gen2 = GC.CollectionCount(2);

            // Process memory
            long privateBytes = proc.PrivateMemorySize64;
            long workingSet = proc.WorkingSet64;
            long virtualBytes = proc.VirtualMemorySize64;
            int threads = proc.Threads.Count;
            int handles = proc.HandleCount;
            var cpuTime = proc.TotalProcessorTime;

            double managedMb = managedBytes / 1024.0 / 1024.0;
            double heapMb = gcInfo.HeapSizeBytes / 1024.0 / 1024.0;
            double fragmentedMb = gcInfo.FragmentedBytes / 1024.0 / 1024.0;
            double privateMb = privateBytes / 1024.0 / 1024.0;
            double workingSetMb = workingSet / 1024.0 / 1024.0;
            double virtualMb = virtualBytes / 1024.0 / 1024.0;

            sb.AppendLine($"Report time (UTC): {now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Process start (UTC): {startTimeUtc:yyyy-MM-dd HH:mm:ss} (uptime: {FormatTimeSpan(uptime)})");
            sb.AppendLine();

            sb.AppendLine("=== Managed (.NET) ===");
            sb.AppendLine($"Managed heap (GC.GetTotalMemory): {managedMb:F2} MB");
            sb.AppendLine($"GC HeapSize (GCMemoryInfo.HeapSizeBytes): {heapMb:F2} MB");
            sb.AppendLine($"Fragmented bytes (approx): {fragmentedMb:F2} MB");
            sb.AppendLine($"GC Collections: Gen0={gen0}, Gen1={gen1}, Gen2={gen2}");
            sb.AppendLine();

            sb.AppendLine("=== Process (OS) ===");
            sb.AppendLine($"Private bytes: {privateMb:F2} MB");
            sb.AppendLine($"Working set:   {workingSetMb:F2} MB");
            sb.AppendLine($"Virtual mem:   {virtualMb:F2} MB");
            sb.AppendLine($"Threads: {threads}, Handles: {handles}");
            sb.AppendLine($"CPU time (total): {cpuTime}");
            sb.AppendLine();

            // Additional GC info
            sb.AppendLine("=== GC info additional ===");
            sb.AppendLine($"TotalAvailableMemoryBytes: {FormatBytes(gcInfo.TotalAvailableMemoryBytes)}");
            sb.AppendLine($"MemoryLoadBytes: {FormatBytes(gcInfo.MemoryLoadBytes)}");
            sb.AppendLine($"HighMemoryLoadThresholdBytes: {FormatBytes(gcInfo.HighMemoryLoadThresholdBytes)}");
            sb.AppendLine();

            if (privateMb >= _warningThresholdMb || managedMb >= _warningThresholdMb * 0.9)
            {
                sb.AppendLine("!!! WARNING: memory usage is high (threshold reached).");
            }
            else
            {
                sb.AppendLine("Memory usage: OK.");
            }

            return Task.FromResult(sb.ToString());
        }

        private static string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalDays >= 1) return $"{(int) ts.TotalDays}d {ts:hh\\:mm\\:ss}";
            return ts.ToString(@"hh\:mm\:ss");
        }

        private static string FormatBytes(long b)
        {
            if (b < 0) return "n/a";
            string[] suf = { "B", "KB", "MB", "GB", "TB" };
            double val = b;
            int i = 0;
            while (val >= 1024 && i < suf.Length - 1) { val /= 1024; i++; }
            return $"{val:F2} {suf[i]}";
        }
    }
}
