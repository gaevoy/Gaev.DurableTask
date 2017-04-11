using System;
using System.Threading.Tasks;

namespace Gaev.DurableTask
{
    public interface IProcessFactory
    {
        IProcess Spawn(string id);
        void SetEntryPoint(Func<string, bool> idSelector, Func<string, Task> entryPoint);
        Task RunSuspended();
    }
}