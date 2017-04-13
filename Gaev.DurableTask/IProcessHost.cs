using System;
using System.Threading.Tasks;

namespace Gaev.DurableTask
{
    public interface IProcessHost
    {
        IProcess Spawn(string id);
        void SetEntryPoint(Func<string, bool> idSelector, Func<string, Task> entryPoint);
        // TODO: Pass a CancellationToken, use the CancellationToken to every process
        Task Run();
    }
}