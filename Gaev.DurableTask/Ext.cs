using System;
using System.Threading.Tasks;

namespace Gaev.DurableTask
{
    public static class Ext
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

        public static T As<T>(this IProcess process) where T : IProcess
        {
            return (T)process;
        }

        public static Task<T> Attach<T>(this IProcess process, T value, string id)
        {
            return process.Do(() => Task.FromResult(value), id);
        }

        public static void Register(this IProcessHost processHost, Func<string, bool> idSelector, Func<string, Task> entryPoint)
        {
            processHost.Register(new ProcessRegistration
            {
                IdSelector = idSelector,
                EntryPoint = entryPoint
            });
        }
    }
}