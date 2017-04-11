﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gaev.DurableTask.Storage;

namespace Gaev.DurableTask
{
    public class ProcessFactory : IProcessFactory
    {
        private readonly IProcessStorage _storage;
        private readonly List<Registeration> _registerations = new List<Registeration>();

        public ProcessFactory(IProcessStorage storage)
        {
            _storage = storage;
        }
        public Task<IProcess> Spawn(string id)
        {
            return Task.FromResult<IProcess>(new Process(id, _storage));
        }

        public void RestoreProcess(Func<string, bool> idSelector, Func<string, Task> entryPoint)
        {
            lock (_registerations)
                _registerations.Add(new Registeration(entryPoint, idSelector));
        }

        public async Task Initialize()
        {
            var tasks = (from id in await _storage.GetPendingProcessIds()
                         from registeration in _registerations
                         where registeration.IdSelector(id)
                         select registeration.EntryPoint(id)).ToArray();
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
