using System;
using System.Threading.Tasks;

namespace Gaev.DurableTask.ConsolePlayground
{
    public class CreditCardProcess : IProcess
    {
        private readonly IProcess _underlying;

        public CreditCardProcess(IProcess underlying)
        {
            _underlying = underlying;
        }

        public void Dispose() => _underlying.Dispose();
        public Task<T> Do<T>(Func<Task<T>> act, string id) => _underlying.Do(act, id);

        private readonly TaskCompletionSource<object> _onTransactionAppeared = new TaskCompletionSource<object>();
        public void RaiseOnTransactionAppeared() => _onTransactionAppeared.TrySetResult(null);
        public Task OnTransactionAppeared() => _onTransactionAppeared.Task;

        private readonly TaskCompletionSource<object> _onCreditCardDeleted = new TaskCompletionSource<object>();
        public void RaiseOnCreditCardDeleted() => _onCreditCardDeleted.TrySetResult(null);
        public Task OnCreditCardDeleted() => _onCreditCardDeleted.Task;
    }
}