﻿using System;
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
        private Task _running;

        public ProcessHost(IProcessStorage storage)
        {
            _storage = storage;
        }

        public IProcess Spawn(string processId)
        {
            if (_cancellation == null) throw new ApplicationException("Process host has not been started");
            return _process.GetOrAdd(processId, id =>
            {
                ProcessRegistration registration;
                lock (_registrations)
                    registration = _registrations.FirstOrDefault(e => e.IdSelector(id));
                if (registration == null)
                    throw new ApplicationException($"Registration for {id} is not found");
                return registration.ProcessWrapper(new Process(id, _storage, _cancellation.Token));
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

        public async Task Start()
        {
            if (_cancellation != null) throw new ApplicationException("Process host already has been started");
            _cancellation = new CancellationTokenSource();
            var tasks = (from id in await _storage.GetPendingProcessIds()
                         from registeration in _registrations
                         where registeration.IdSelector(id)
                         select registeration.EntryPoint(id)).ToArray();
            _running = Task.WhenAll(tasks);
        }

        public void Dispose()
        {
            _cancellation.Cancel();
            try
            {
                _running.Wait();
            }
            catch (Exception)
            {
                // Ignore
            }
        }
    }
}
