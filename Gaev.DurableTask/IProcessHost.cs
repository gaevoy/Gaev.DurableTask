using System.Threading.Tasks;

namespace Gaev.DurableTask
{
    public interface IProcessHost
    {
        IProcess Spawn(string id);
        IProcess Get(string id);
        void Register(ProcessRegistration registration);
        void Resume();
        void Watch(Task longRunningTask);
        // TODO: Consider to add Host(string processId, Func<Task> entryPoint) method and remove Spawn because it will be executed inside Host method
    }
}