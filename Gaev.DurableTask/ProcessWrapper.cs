using System;
using System.Threading;
using System.Threading.Tasks;

namespace Gaev.DurableTask
{
    public abstract class ProcessWrapper : IProcess
    {
        protected readonly IProcess Underlying;

        protected ProcessWrapper(IProcess underlying)
        {
            Underlying = underlying;
        }

        public CancellationToken Cancellation => Underlying.Cancellation;
        public void Dispose() => Underlying.Dispose();
        public Task<T> Do<T>(Func<Task<T>> act, string id, bool redo = false) => Underlying.Do(act, id, redo);
    }
}