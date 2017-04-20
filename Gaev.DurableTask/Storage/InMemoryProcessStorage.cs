using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gaev.DurableTask.Storage
{
    public class InMemoryProcessStorage : IProcessStorage
    {
        private readonly ConcurrentDictionary<string, object> _value = new ConcurrentDictionary<string, object>();
        private readonly ConcurrentDictionary<string, object> _process = new ConcurrentDictionary<string, object>();
        public Task Set<T>(string processId, string operationId, OperationState<T> state)
        {
            _process[processId] = processId;
            _value[processId + operationId] = state;
            return Task.CompletedTask;
        }

        public void CleanProcess(string processId)
        {
            object _;
            _process.TryRemove(processId, out _);
            var keys = _value.Keys.Where(e => e.StartsWith(processId)).ToList();
            foreach (var key in keys)
                _value.TryRemove(key, out _);
        }

        public IEnumerable<string> GetPendingProcessIds()
        {
            return _process.Keys.ToList();
        }

        public Task<OperationState<T>> Get<T>(string processId, string operationId)
        {
            object value;
            OperationState<T> result = null;
            if (_value.TryGetValue(processId + operationId, out value))
                result = (OperationState<T>)value;
            return Task.FromResult(result);
        }
    }
}
