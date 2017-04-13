using System;
using System.Threading.Tasks;
using Gaev.DurableTask.Storage;

namespace Gaev.DurableTask
{
    public class Process : IProcess
    {
        private readonly string _id;
        private readonly IProcessStorage _storage;
        private bool _isCompleted;
        private TaskCompletionSource<object> _pendingMessage = new TaskCompletionSource<object>();
        private TaskCompletionSource<object> _onMessageReceived = new TaskCompletionSource<object>();

        // TODO: Pass a CancellationToken, don't save OperationCancalledException
        public Process(string id, IProcessStorage storage)
        {
            _id = id;
            _storage = storage;
            Start(); // Start on demand only when 1st Do requested
        }

        private void Start()
        {
            var state = _storage.Get(_id);
            if (state == null)
            {
                state = new ProcessState();
                _storage.Set(_id, state);
            }
            _isCompleted = state.IsCompleted;
        }

        private void Complete()
        {
            if (!_isCompleted)
            {
                _isCompleted = true;
                _storage.Set(_id, new ProcessState { IsCompleted = true });
            }
        }

        public void Dispose()
        {
            Complete();
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

        public async Task Send<T>(T message)
        {
            _onMessageReceived = new TaskCompletionSource<object>();
            _pendingMessage.SetResult(message);
            await _onMessageReceived.Task;
        }

        public async Task<T> Receive<T>()
        {
            var message = await _pendingMessage.Task;
            _pendingMessage = new TaskCompletionSource<object>();
            _onMessageReceived.TrySetResult(null);
            return (T)message;
        }
    }
}
