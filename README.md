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
