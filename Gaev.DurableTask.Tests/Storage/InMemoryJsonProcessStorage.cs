﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
            _process[processId] = processId;
            _value[processId + operationId] = JsonConvert.SerializeObject(state, _jsonSettings);
        }

        public void CleanProcess(string processId)
        {
            string _;
            _process.TryRemove(processId, out _);
            var keys = _value.Keys.Where(e => e.StartsWith(processId)).ToList();
            foreach (var key in keys)
                _value.TryRemove(key, out _);
        }

        public IEnumerable<string> GetPendingProcessIds()
        {
            return _process.Keys.ToList();
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

        private static Task EmulateAsync() => Task.Delay(1);
    }
}