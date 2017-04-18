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
        private readonly List<ProcessRegistration> _registrations = new List<ProcessRegistration>();
        private readonly ConcurrentDictionary<string, IProcess> _process = new ConcurrentDictionary<string, IProcess>();

        public ProcessHost(IProcessStorage storage)
        {
            _storage = storage;
        }

        public IProcess Spawn(string processId)
        {
            return _process.GetOrAdd(processId, id =>
            {
                ProcessRegistration registration;
                lock (_registrations)
                    registration = _registrations.FirstOrDefault(e => e.IdSelector(id));
                if (registration == null)
                    throw new ApplicationException($"Registration for {id} is not found");
                return registration.ProcessWrapper(new Process(id, _storage));
            });
        }

        public IProcess Get(string id)
        {
            IProcess result;
            _process.TryGetValue(id, out result);
            return result;
        }

        public void Register(ProcessRegistration registration)
        {
            lock (_registrations)
                _registrations.Add(registration);
        }

        public async Task Start()
        {
            var tasks = (from id in await _storage.GetPendingProcessIds()
                         from registeration in _registrations
                         where registeration.IdSelector(id)
                         select registeration.EntryPoint(id)).ToArray();
            var _ = Task.WhenAll(tasks);
        }
    }
}
