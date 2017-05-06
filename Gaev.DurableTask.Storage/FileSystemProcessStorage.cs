using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Gaev.DurableTask.Storage
{
    public class FileSystemProcessStorage : IProcessStorage
    {
        private readonly string _basePath;
        private readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter> { new ProcessExceptionSerializer(), new VoidSerializer() }
        };

        public FileSystemProcessStorage(string basePath = null)
        {
            _basePath = basePath ?? GetDefaultBasePath();
            Directory.CreateDirectory(_basePath);
        }

        public async Task Set<T>(string processId, string operationId, OperationState<T> state)
        {
            var isException = state.Exception != null;
            var stateJson = JsonConvert.SerializeObject(isException ? (object)state.Exception : state.Value, _jsonSettings);
            var processFolder = Path.Combine(_basePath, processId);
            Directory.CreateDirectory(processFolder);
            string path = Path.Combine(processFolder, operationId);
            using (var stream = File.OpenWrite(path))
            {
                var payload = new FilePayload { IsException = isException, State = stateJson };
                var buffer = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload, _jsonSettings));
                await stream.WriteAsync(buffer, 0, buffer.Length);
            }
        }

        public void CleanProcess(string processId)
        {
            string dir = Path.Combine(_basePath, processId);
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }

        public IEnumerable<string> GetPendingProcessIds()
        {
            return new DirectoryInfo(_basePath).GetDirectories().Select(e => e.Name);
        }

        public async Task<OperationState<T>> Get<T>(string processId, string operationId)
        {
            string path = Path.Combine(_basePath, processId, operationId);
            if (!File.Exists(path)) return null;
            using (var stream = File.OpenText(path))
            {
                var payload = JsonConvert.DeserializeObject<FilePayload>(await stream.ReadToEndAsync(), _jsonSettings);
                if (payload.IsException)
                    return new OperationState<T>
                    {
                        Exception = JsonConvert.DeserializeObject<ProcessException>(payload.State, _jsonSettings)
                    };
                return new OperationState<T>
                {
                    Value = JsonConvert.DeserializeObject<T>(payload.State, _jsonSettings)
                };
            }
        }

        private string GetDefaultBasePath()
        {
            return Path.Combine(Path.GetDirectoryName(new Uri(GetType().Assembly.CodeBase).AbsolutePath), "Processes");
        }

        private class FilePayload
        {
            public bool IsException { get; set; }
            public string State { get; set; }
        }
    }
}
