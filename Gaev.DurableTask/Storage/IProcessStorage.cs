using System.Collections.Generic;
using System.Threading.Tasks;

namespace Gaev.DurableTask.Storage
{
    public interface IProcessStorage
    {
        Task Set<T>(string processId, string operationId, OperationState<T> state);
        void Set(string processId, ProcessState state);
        Task<IEnumerable<string>> GetPendingProcessIds();
        Task<OperationState<T>> Get<T>(string processId, string operationId);
        ProcessState Get(string processId);
    }
}