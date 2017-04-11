using System.Threading.Tasks;

namespace Gaev.DurableTask
{
    public sealed class Void
    {
        private Void() { }
        public static readonly Void Nothing = new Void();
        public static readonly Task<Void> CompletedTask = Task.FromResult(Nothing);
    }
}