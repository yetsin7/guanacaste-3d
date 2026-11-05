namespace Guanacaste3D.Core.Rendering;

/// <summary>
/// Ensures sufficient .NET thread-pool workers for the renderer's short parallel tasks.
/// This changes the process-wide minimum; benchmark it alongside UI responsiveness.
/// </summary>
internal static class RenderThreads
{
    private static readonly object PreparationLock = new();
    private static bool _prepared;

    public static void EnsureCapacity()
    {
        lock (PreparationLock)
        {
            if (_prepared) return;

            ThreadPool.GetMinThreads(out var workers, out var completion);
            // Avoid overflowing when calculating the desired worker count.
            var wanted = (int)Math.Min(int.MaxValue, Math.Max(4L, (long)Environment.ProcessorCount * 2));
            if (workers < wanted && !ThreadPool.SetMinThreads(wanted, completion))
            {
                // Do not cache a failed attempt: a later call may retry.
                return;
            }

            _prepared = true;
        }
    }
}
