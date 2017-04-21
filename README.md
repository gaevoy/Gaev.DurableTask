**Gaev.DurableTask** is tiny library to build durable task, saga, process manager using the async/await capabilities. Inspired by [Azure Durable Task Framework](https://github.com/Azure/durabletask). 

Just imagine you can write regular code using the async/await capabilities which can last for long time, say 1 week or year. Moreover, if an application crashes the durable task will resume execution exactly from where it left. 

A durable task must have some storage for storing current state in order to resume execution after restart/crash. There is MS SQL storage provider. However, you can implement your own provider, just implement *IProcessStorage*.

To estimate amount of used memory, a simple durable task was hosted in console application. As a result one instance of the durable task will use 4.3Kb in 32bit or 9KB in 64bit, so 250 000 instances will occupy 1Gb of 32bit console app.

Let's look closer to the durable task. It is easier to show an example:

**Saga, process manager**
```csharp
using System;
using System.Threading.Tasks;
using Gaev.DurableTask;
using Gaev.DurableTask.MsSql;

public class UserRegistrationSaga
{
    private readonly IProcessHost _host;
    private readonly UserService _service;

    public UserRegistrationSaga(IProcessHost host, UserService service)
    {
        _host = host;
        _service = service;
    }

    public void RegisterForResuming()
    {
        _host.Register(id => id.StartsWith(nameof(UserRegistrationSaga)), id => DurableTask(id));
    }

    public void StartUserRegistration(string email)
    {
        _host.Watch(DurableTask(nameof(UserRegistrationSaga) + Guid.NewGuid(), email));
    }

    private async Task DurableTask(string id, string email = null)
    {
        using (var proc = _host.Spawn(id))
        {
            // Save email not to lose it if durable task resumes
            email = await proc.Get(email, "SaveEmail");
            // Register the user
            var userId = await proc.Do(() => _service.RegisterUser(email), "RegisterUser");
            // Generate a secret for email verification
            var secret = await proc.Get(Guid.NewGuid(), "secret");
            // Send email to the user with the secret to verify
            await proc.Do(() => _service.VerifyEmail(email, secret), "VerifyEmail");
            // Wait when user receive verification email and send the secret here, it can take couple of days
            await proc.Do(() => _service.WaitForEmailVerification(secret), "WaitForEmailVerification");
            // Activate the user in the system
            await proc.Do(() => _service.ActivateUser(userId), "ActivateUser");
        }
    }

    public class UserService
    {
        public Task<Guid> RegisterUser(string email) => Task.FromResult(Guid.NewGuid());
        public Task VerifyEmail(string email, Guid secret) => Task.CompletedTask;
        public Task WaitForEmailVerification(Guid secret) => Task.CompletedTask;
        public Task ActivateUser(Guid userId) => Task.CompletedTask;
    }

    static void Main(string[] args)
    {
        using (var host = new ProcessHost(new MsSqlProcessStorage("...")))
        {
            var saga = new UserRegistrationSaga(host, new UserService());
            saga.RegisterForResuming();
            host.Resume();
            saga.StartUserRegistration("user1@gmail.com");
            saga.StartUserRegistration("user2@gmail.com");
            Console.Read();
        }
    }
}
```

**Schedule**
```csharp
using System;
using System.Threading.Tasks;
using Gaev.DurableTask;
using Gaev.DurableTask.MsSql;

public class Schedule
{
    private readonly IProcessHost _host;
    private readonly SmtpClient _smtp;

    public Schedule(IProcessHost host, SmtpClient smtp)
    {
        _host = host;
        _smtp = smtp;
    }

    public void RegisterForResuming()
    {
        _host.Register(id => id.StartsWith(nameof(Schedule)), id => DurableTask(id));
    }

    public void Start(string email)
    {
        _host.Watch(DurableTask(nameof(Schedule) + Guid.NewGuid(), email));
    }

    private async Task DurableTask(string id, string email = null)
    {
        using (var proc = _host.Spawn(id))
        {
            // Save email not to lose it if durable task resumes
            email = await proc.Get(email, "SaveEmail");
            await proc.Do(() => _smtp.Send(email, "Welcome!"), "Welcome");
            // Wait 1 month
            await proc.Delay(TimeSpan.FromDays(30), "Wait1m");
            await proc.Do(() => _smtp.Send(email, "Your 1st month with us. Congrats!"), "CongratsMonth");
            // Wait 11 months
            await proc.Delay(TimeSpan.FromDays(365 - 30), "Wait1y");
            await proc.Do(() => _smtp.Send(email, "Your 1st year with us. Congrats!"), "CongratsYear");
        }
    }

    public class SmtpClient
    {
        public Task Send(string email, string text) => Task.CompletedTask;
    }

    static void Main(string[] args)
    {
        using (var host = new ProcessHost(new MsSqlProcessStorage("...")))
        {
            var schedule = new Schedule(host, new SmtpClient());
            schedule.RegisterForResuming();
            host.Resume();
            schedule.Start("user1@gmail.com");
            schedule.Start("user2@gmail.com");
            Console.Read();
        }
    }
}
```
**Rollback logic**
```csharp
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
```
