using System;
using System.Threading.Tasks;

namespace Gaev.DurableTask.Tests.Examples
{
    public class MoneyTransferHandler
    {
        private readonly IProcessFactory _factory;
        private readonly IAccountBusinessLogic _accounts;

        private class State
        {
            public string FromAccountId { get; set; }
            public string ToAccountId { get; set; }
            public decimal Amount { get; set; }
        }

        public MoneyTransferHandler(IProcessFactory factory, IAccountBusinessLogic accounts)
        {
            _factory = factory;
            _accounts = accounts;
        }

        public Task StartTransfer(string fromAccountId, string toAccountId, decimal amount)
        {
            var input = new State
            {
                FromAccountId = fromAccountId,
                ToAccountId = toAccountId,
                Amount = amount
            };
            return Transfer(input, nameof(MoneyTransferHandler) + Guid.NewGuid());
        }

        private async Task Transfer(State input, string id)
        {
            using (var process = _factory.Spawn(id))
            {
                var state = await process.Attach(input, "StateSaved");
                string fromTranId = null;
                string toTranId = null;
                try
                {
                    fromTranId = await process.Do(() => _accounts.Withdraw(state.FromAccountId, -state.Amount), "Withdraw1");
                    toTranId = await process.Do(() => _accounts.Withdraw(state.ToAccountId, +state.Amount), "Withdraw2");
                    await process.Do(() => _accounts.Capture(state.FromAccountId, fromTranId), "Capture1");
                    await process.Do(() => _accounts.Capture(state.ToAccountId, toTranId), "Capture2");
                }
                catch (ProcessException ex) when (ex.Type == nameof(ApplicationException))
                {
                    if (fromTranId != null)
                        await process.Do(() => _accounts.DeleteTransaction(state.FromAccountId, fromTranId), "Delete1");
                    if (toTranId != null)
                        await process.Do(() => _accounts.DeleteTransaction(state.ToAccountId, toTranId), "Delete2");
                    throw;
                }
            }
        }

        public void RegisterProcess()
        {
            _factory.SetEntryPoint(id => id.StartsWith(nameof(MoneyTransferHandler)), id => Transfer(null, id));
        }
    }
}
