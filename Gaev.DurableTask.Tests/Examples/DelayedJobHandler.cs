using System;
using System.Threading.Tasks;

namespace Gaev.DurableTask.Tests.Examples
{
    public class DelayedJobHandler
    {
        private readonly IProcessFactory _factory;

        public DelayedJobHandler(IProcessFactory factory)
        {
            _factory = factory;
        }

        public Task<DateTime> StartJob(TimeSpan delay)
        {
            return Run(delay, nameof(DelayedJobHandler) + Guid.NewGuid());
        }

        private async Task<DateTime> Run(TimeSpan delay, string id)
        {
            using (var process = _factory.Spawn(id))
            {
                await process.Delay(delay, "Delayed");
                return await process.Do(() => Task.FromResult(DateTime.UtcNow), "Executed");
            }
        }

        public void RegisterProcess()
        {
            _factory.SetEntryPoint(id => id.StartsWith(nameof(DelayedJobHandler)), id => Run(default(TimeSpan), id));
        }
    }
}
