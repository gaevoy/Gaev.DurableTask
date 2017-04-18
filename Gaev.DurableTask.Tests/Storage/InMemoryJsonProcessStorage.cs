using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gaev.DurableTask.Json;
using Gaev.DurableTask.Storage;
using Newtonsoft.Json;

namespace Gaev.DurableTask.Tests.Storage
{
    public class InMemoryJsonProcessStorage : IProcessStorage
    {
        private readonly ConcurrentDictionary<string, string> _process = new ConcurrentDictionary<string, string>();
        private readonly ConcurrentDictionary<string, string> _value = new ConcurrentDictionary<string, string>();
        private readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter> { new ProcessExceptionSerializer(), new VoidSerializer() }
        };

        public async Task Set<T>(string processId, string operationId, OperationState<T> state)
        {
            await EmulateAsync();
            _value[processId + operationId] = JsonConvert.SerializeObject(state, _jsonSettings);
        }

        public void Set(string processId, ProcessState state)
        {
            string _;
            if (state.IsCompleted)
                _process.TryRemove(processId, out _);
            else
                _process[processId] = "";
        }

        public async Task<IEnumerable<string>> GetPendingProcessIds()
        {
            await EmulateAsync();
            return _process.Keys;
        }

        public async Task<OperationState<T>> Get<T>(string processId, string operationId)
        {
            await EmulateAsync();
            string value;
            OperationState<T> result = null;
            if (_value.TryGetValue(processId + operationId, out value))
                result = JsonConvert.DeserializeObject<OperationState<T>>(value, _jsonSettings);
            return result;
        }

        public ProcessState Get(string key)
        {
            string value;
            if (_process.TryGetValue(key, out value))
                return JsonConvert.DeserializeObject<ProcessState>(value, _jsonSettings);
            return null;
        }

        private static Task EmulateAsync() => Task.Delay(5);
    }
}