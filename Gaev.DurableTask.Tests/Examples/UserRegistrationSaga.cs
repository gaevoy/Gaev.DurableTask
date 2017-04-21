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
            email = await proc.Get(email, "SaveEmail");
            var userId = await proc.Do(() => _service.RegisterUser(email), "RegisterUser");
            var secret = await proc.Get(Guid.NewGuid(), "secret");
            await proc.Do(() => _service.VerifyEmail(email, secret), "VerifyEmail");
            await proc.Do(() => _service.WaitForEmailVerification(secret), "WaitForEmailVerification");
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