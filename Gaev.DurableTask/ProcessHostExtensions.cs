using System;
using System.Threading.Tasks;

namespace Gaev.DurableTask
{
    public static class ProcessHostExtensions
    {
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