using System;
using System.Threading.Tasks;

namespace Gaev.DurableTask
{
    public interface IProcess : IDisposable
    {
        Task<T> Do<T>(Func<Task<T>> act, string id);
        Task Send<T>(T message);
        Task<T> Receive<T>();
    }
}
