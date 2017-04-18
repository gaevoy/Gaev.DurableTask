using System;
using System.Threading.Tasks;

namespace Gaev.DurableTask
{
    public class ProcessRegistration
    {
        private static readonly Func<IProcess, IProcess> NoProcessWrapper = p => p;
        public ProcessRegistration()
        {
            ProcessWrapper = NoProcessWrapper;
        }
        public Func<string, Task> EntryPoint { get; set; }
        public Func<string, bool> IdSelector { get; set; }
        public Func<IProcess, IProcess> ProcessWrapper { get; set; }
    }
}