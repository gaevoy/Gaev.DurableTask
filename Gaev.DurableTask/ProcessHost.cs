using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gaev.DurableTask.Storage;

namespace Gaev.DurableTask
{
    public class ProcessHost : IProcessHost
    {
        private readonly IProcessStorage _storage;
        private readonly List<Registeration> _registerations = new List<Registeration>();
        private readonly ConcurrentDictionary<string, IProcess> _process = new ConcurrentDictionary<string, IProcess>();

        public ProcessHost(IProcessStorage storage)
        {
            _storage = storage;
        }
        public IProcess Spawn(string processId)
        {
            return _process.GetOrAdd(processId, id => new Process(id, _storage));
        }

        public void SetEntryPoint(Func<string, bool> idSelector, Func<string, Task> entryPoint)
        {
            lock (_registerations)
                _registerations.Add(new Registeration(entryPoint, idSelector));
        }

        public async Task Run()
        {
            var tasks = (from id in await _storage.GetPendingProcessIds()
                         from registeration in _registerations
                         where registeration.IdSelector(id)
                         select registeration.EntryPoint(id)).ToArray();
            try
            {
                await Task.WhenAll(tasks);
            }
            catch
            {
                // Ignore 
            }
        }

        private class Registeration
        {
            public Registeration(Func<string, Task> entryPoint, Func<string, bool> idSelector)
            {
                EntryPoint = entryPoint;
                IdSelector = idSelector;
            }
            public Func<string, Task> EntryPoint { get; }
            public Func<string, bool> IdSelector { get; }
        }
    }
}
