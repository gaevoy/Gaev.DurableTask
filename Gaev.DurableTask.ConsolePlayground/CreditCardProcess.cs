using System.Threading.Tasks;

namespace Gaev.DurableTask.ConsolePlayground
{
    public class CreditCardProcess : ProcessWrapper
    {
        public CreditCardProcess(IProcess underlying) : base(underlying)
        {
            Underlying.Cancellation.Register(() =>
            {
                _onTransactionAppeared.TrySetCanceled();
                _onCreditCardDeleted.TrySetCanceled();
            });
        }
        
        private readonly TaskCompletionSource<object> _onTransactionAppeared = new TaskCompletionSource<object>();
        public void RaiseOnTransactionAppeared() => _onTransactionAppeared.TrySetResult(null);
        public Task OnTransactionAppeared() => _onTransactionAppeared.Task;

        private readonly TaskCompletionSource<object> _onCreditCardDeleted = new TaskCompletionSource<object>();
        public void RaiseOnCreditCardDeleted() => _onCreditCardDeleted.TrySetResult(null);
        public Task OnCreditCardDeleted() => _onCreditCardDeleted.Task;
    }
}