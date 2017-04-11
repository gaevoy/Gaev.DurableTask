using System;
using System.Threading.Tasks;

namespace Gaev.DurableTask
{
    public interface IProcessFactory
    {
        Task<IProcess> Spawn(string id);
        void RestoreProcess(Func<string, bool> idSelector, Func<string, Task> entryPoint);
        Task Initialize();
    }
}