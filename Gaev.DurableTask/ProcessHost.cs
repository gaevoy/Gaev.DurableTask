using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gaev.DurableTask.Storage;

namespace Gaev.DurableTask
{
    public class ProcessHost : IProcessHost, IDisposable
    {
        private readonly IProcessStorage _storage;
        private readonly List<ProcessRegistration> _registrations = new List<ProcessRegistration>();
        private readonly ConcurrentDictionary<string, IProcess> _process = new ConcurrentDictionary<string, IProcess>();
        private CancellationTokenSource _cancellation;
        private readonly object _syncLock = new object();
        private List<Task> _watchList;

        public ProcessHost(IProcessStorage storage)
        {
            _storage = storage;
        }

        public IProcess Spawn(string processId)
        {
            if (_cancellation == null) throw new ApplicationException("Process host has not been started");
            if (_process.ContainsKey(processId)) throw new ApplicationException($"Process {processId} already exist");
            return _process.GetOrAdd(processId, id =>
            {
                ProcessRegistration registration;
                lock (_registrations)
                    registration = _registrations.FirstOrDefault(e => e.IdSelector(id));
                if (registration == null)
                    throw new ApplicationException($"Registration for {id} is not found");
                return registration.ProcessWrapper(new Process(id, _storage, _cancellation.Token, Remove));
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
            if (_cancellation != null) throw new ApplicationException("You can not register after process host start");
            lock (_registrations)
                _registrations.Add(registration);
        }

        public void Resume()
        {
            if (_cancellation != null) throw new ApplicationException("Process host already has been started");
            _cancellation = new CancellationTokenSource();
            _watchList = (from id in _storage.GetPendingProcessIds().AsParallel()
                          from registeration in _registrations
                          where registeration.IdSelector(id)
                          select registeration.EntryPoint(id)).ToList();
        }

        public void Watch(Task longRunningTask)
        {
            if (_cancellation == null) throw new ApplicationException("You can not host after process host start");
            lock (_syncLock)
                _watchList.Add(longRunningTask);
        }

        public void Dispose()
        {
            _cancellation.Cancel();
            try
            {
                Task.WhenAll(_watchList).Wait();
            }
            catch (Exception)
            {
                // Ignore
            }
        }

        private void Remove(string processId)
        {
            IProcess _;
            _process.TryRemove(processId, out _);
        }
    }
}
