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
        // TODO: Pass a CancellationToken, don't save OperationCancalledException
        private bool _isDisposed;
        public CancellationToken Cancellation { get; }

        public Process(string id, IProcessStorage storage, CancellationToken cancellation)
        {
            _id = id;
            _storage = storage;
            Cancellation = cancellation;
        }

        public void Dispose()
        {
            if (!_isDisposed && !Cancellation.IsCancellationRequested)
            {
                _isDisposed = true;
                _storage.CleanProcess(_id);
            }
        }

        public async Task<T> Do<T>(Func<Task<T>> act, string id)
        {
            OperationState<T> result = await _storage.Get<T>(_id, id);
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
