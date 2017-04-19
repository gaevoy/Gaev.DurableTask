using System;
using System.Threading;
using System.Threading.Tasks;

namespace Gaev.DurableTask
{
    public interface IProcess : IDisposable
    {
        CancellationToken Cancellation { get; }
        Task<T> Do<T>(Func<Task<T>> act, string id, bool redo = false);
    }
}
