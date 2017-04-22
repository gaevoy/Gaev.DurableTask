using System;
using System.Threading.Tasks;
// ReSharper disable AccessToModifiedClosure
// ReSharper disable AccessToDisposedClosure
#pragma warning disable 4014
#pragma warning disable 1998

namespace Gaev.DurableTask.ConsolePlayground
{
    public class CreditCardFlow
    {
        private readonly IProcessHost _host;

        public CreditCardFlow(IProcessHost host)
        {
            _host = host;
        }

        public string Start(string creditCard, string companyId)
        {
            var processId = nameof(CreditCardFlow) + creditCard;
            _host.Watch(DurableTask(processId, companyId, creditCard));
            return processId;
        }

        private async Task DurableTask(string processId, string companyId = null, string creditCard = null)
        {
            using (var proc = _host.Spawn(processId).As<CreditCardProcess>())
            {
                companyId = await proc.Get(companyId, "1");
                creditCard = await proc.Get(creditCard, "2");
                Console.WriteLine($"CreditCardFlow is up for companyId={companyId} creditCard={creditCard}");
                var email = await proc.Do(() => GetEmail(companyId), "3");
                await proc.Do(() => SendEmail(email, $"{creditCard} was assigned to you"), "4");
                var onCheckTime = proc.Delay(TimeSpan.FromMinutes(5), "5");
                var onFirstTransaction = proc.Do(() => proc.OnTransactionAppeared(), "6");
                var onDeleted = proc.Do(() => proc.OnCreditCardDeleted(), "7");
                Task.Run(async () =>
                {
                    await onCheckTime;
                    if (onDeleted.IsCompleted) return;
                    if (!onFirstTransaction.IsCompleted)
                        await proc.Do(() => SendEmail(email, $"{creditCard} is inactive long time"), "8");
                });
                Task.Run(async () =>
                {
                    await onFirstTransaction;
                    if (onDeleted.IsCompleted) return;
                    await proc.Do(() => SendEmail(email, $"{creditCard} received 1st transaction"), "9");
                });

                await onDeleted;
                // Cancel all pending tasks
                await proc.Do(() => SendEmail(email, $"{creditCard} was deleted"), "10");
            }
        }

        public void RegisterProcess() => _host.Register(new ProcessRegistration
        {
            IdSelector = id => id.StartsWith(nameof(CreditCardFlow)),
            EntryPoint = id => DurableTask(id),
            ProcessWrapper = p => new CreditCardProcess(p)
        });

        private async Task<string> GetEmail(string companyId)
        {
            await EmulateAsync();
            return companyId + "@test.com";
        }

        private async Task SendEmail(string email, string text)
        {
            await EmulateAsync();
            Console.WriteLine($"Email '{text}' was sent to {email}");
        }

        private static Task EmulateAsync() => Task.Delay(5);
    }
}
