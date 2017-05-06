using System;
using System.Threading.Tasks;
using Gaev.DurableTask;
using Gaev.DurableTask.Storage;

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
        using (var host = new ProcessHost(new FileSystemProcessStorage()))
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