using System;
using System.Threading.Tasks;

namespace Gaev.DurableTask
{
    public static class ProcessExt
    {
        public static Task Do(this IProcess process, Func<Task> act, string id)
        {
            return process.Do(async () =>
            {
                await act();
                return Void.Nothing;
            }, id);
        }
        public static Task Delay(this IProcess process, TimeSpan delay, string id)
        {
            return process.Do(async () =>
            {
                var runAt = await process.Attach(DateTime.UtcNow + delay, id + ".Saved");
                delay = runAt - DateTime.UtcNow;
                if (delay > TimeSpan.Zero)
                    await process.Do(() => Task.Delay(delay), id + ".Completed");
            }, id);
        }

        public static Task<T> Attach<T>(this IProcess process, T value, string id)
        {
            return process.Do(() => Task.FromResult(value), id);
        }
    }
}