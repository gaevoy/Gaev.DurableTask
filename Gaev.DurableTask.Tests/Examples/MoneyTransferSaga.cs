using System;
using System.Threading.Tasks;
using Gaev.DurableTask;
using Gaev.DurableTask.MsSql;

public class MoneyTransferSaga
{
    private readonly IProcessHost _host;
    private readonly TransferService _service;

    public MoneyTransferSaga(IProcessHost host, TransferService service)
    {
        _host = host;
        _service = service;
    }

    public void RegisterForResuming()
    {
        _host.Register(id => id.StartsWith(nameof(MoneyTransferSaga)), id => DurableTask(id));
    }

    public void StartTransfer(string srcAccount, string destAccount, decimal amount)
    {
        _host.Watch(DurableTask(nameof(MoneyTransferSaga) + Guid.NewGuid(), srcAccount, destAccount, amount));
    }

    private async Task DurableTask(string id, string srcAccount = null, string destAccount = null, decimal amount = 0)
    {
        using (var proc = _host.Spawn(id))
        {
            // Save values not to lose it if durable task resumes
            srcAccount = await proc.Get(srcAccount, "SaveSrcAccount");
            destAccount = await proc.Get(destAccount, "SaveDestAccount");
            amount = await proc.Get(amount, "SaveAmount");
            var srcTranId = Guid.Empty;
            var destTranId = Guid.Empty;
            try
            {
                // Start transferring the money
                srcTranId = await proc.Do(() => _service.StartTransfer(srcAccount, -amount), "StartTransfer1");
                destTranId = await proc.Do(() => _service.StartTransfer(destAccount, +amount), "StartTransfer2");
                // Complete transferring the money
                await proc.Do(() => _service.CompleteTransfer(srcAccount, srcTranId), "CompleteTransfer1");
                await proc.Do(() => _service.CompleteTransfer(destAccount, destTranId), "CompleteTransfer2");
            }
            catch (ProcessException ex) when (ex.Type == nameof(TransferFailedException))
            {
                // Rollback logic
                if (srcTranId != Guid.Empty)
                    await proc.Do(() => _service.RollbackTransfer(srcAccount, srcTranId), "RollbackTransfer1");
                if (destTranId != Guid.Empty)
                    await proc.Do(() => _service.RollbackTransfer(destAccount, destTranId), "RollbackTransfer2");
                throw;
            }
        }
    }

    public class TransferService
    {
        public Task<Guid> StartTransfer(string account, decimal amount) => Task.FromResult(Guid.NewGuid());
        public Task CompleteTransfer(string account, Guid tranId) => Task.CompletedTask;
        public Task RollbackTransfer(string account, Guid tranId) => Task.CompletedTask;
    }

    public class TransferFailedException : Exception { }

    static void Main(string[] args)
    {
        using (var host = new ProcessHost(new MsSqlProcessStorage("...")))
        {
            var saga = new MoneyTransferSaga(host, new TransferService());
            saga.RegisterForResuming();
            host.Resume();
            saga.StartTransfer("user1", "user2", 1000);
            saga.StartTransfer("user1", "user3", 5000);
            Console.Read();
        }
    }
}