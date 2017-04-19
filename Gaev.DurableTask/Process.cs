using System;
using System.Threading;
using System.Threading.Tasks;
using Gaev.DurableTask.Storage;

namespace Gaev.DurableTask
{
    public class Process : IProcess
    {
        private readonly string _id;
        private readonly IProcessStorage _storage;
        private readonly Action<string> _onDisposed;
        public CancellationToken Cancellation { get; }

        public Process(string id, IProcessStorage storage, CancellationToken cancellation, Action<string> onDisposed)
        {
            _id = id;
            _storage = storage;
            _onDisposed = onDisposed;
            Cancellation = cancellation;
        }

        public void Dispose()
        {
            if (!Cancellation.IsCancellationRequested)
            {
                _storage.CleanProcess(_id);
                _onDisposed(_id);
            }
        }

        public async Task<T> Do<T>(Func<Task<T>> act, string id, bool redo = false)
        {
            OperationState<T> result = redo ? null : await _storage.Get<T>(_id, id);
            if (result == null)
            {
                result = new OperationState<T>();
                try
                {
                    result.Value = await act();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    result.Exception = new ProcessException(e.Message, e.GetType().Name);
                }
                await _storage.Set(_id, id, result);
            }
            if (result.Exception != null)
                throw result.Exception;
            return result.Value;
        }
    }
}
