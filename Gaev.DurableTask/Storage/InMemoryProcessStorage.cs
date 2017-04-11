using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Gaev.DurableTask.Storage
{
    public class InMemoryProcessStorage : IProcessStorage
    {
        private readonly ConcurrentDictionary<string, object> _value = new ConcurrentDictionary<string, object>();
        private readonly ConcurrentDictionary<string, object> _process = new ConcurrentDictionary<string, object>();
        public Task Set<T>(string processId, string operationId, OperationState<T> state)
        {
            _value[processId + operationId] = state;
            return Task.CompletedTask;
        }

        public void Set(string processId, ProcessState state)
        {
            object _;
            if (state.IsCompleted)
                _process.TryRemove(processId, out _);
            else
                _process[processId] = state;
        }

        public Task<IEnumerable<string>> GetPendingProcessIds()
        {
            return Task.FromResult<IEnumerable<string>>(_process.Keys);
        }

        public Task<OperationState<T>> Get<T>(string processId, string operationId)
        {
            object value;
            OperationState<T> result = null;
            if (_value.TryGetValue(processId + operationId, out value))
                result = (OperationState<T>)value;
            return Task.FromResult(result);
        }

        public ProcessState Get(string key)
        {
            object value;
            if (_process.TryGetValue(key, out value))
                return (ProcessState)value;
            return null;
        }
    }
}
