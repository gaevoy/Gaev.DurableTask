using System;
using System.Threading.Tasks;
using Gaev.DurableTask.Tests.Storage;
using NUnit.Framework;
// ReSharper disable AccessToModifiedClosure
// ReSharper disable AccessToDisposedClosure
#pragma warning disable 1998

namespace Gaev.DurableTask.Tests
{
    public class ProcessFlowTests
    {
        [Test]
        public async Task ShouldRestore()
        {
            // Given
            var host = new ProcessHost(new InMemoryJsonProcessStorage());
            var processId = Guid.NewGuid().ToString();
            var actualInput = Guid.NewGuid().ToString();

        }


        public class CreditCardFlow
        {
            private readonly IProcessHost _host;

            public CreditCardFlow(IProcessHost host)
            {
                _host = host;
            }

            public string Start(string companyId, string creditCard)
            {
                var processId = nameof(CreditCardFlow) + Guid.NewGuid();
                var _ = Run(processId, companyId, creditCard);
                return processId;
            }

            private async Task Run(string processId, string companyId = null, string creditCard = null)
            {
                using (var process = _host.Spawn(processId))
                {
                    // on restart we need to reread OnCreditCardTransaction, OnCreditCardDeleted if that event raised while saga is down
                    companyId = await process.Attach(companyId, "1");
                    creditCard = await process.Attach(creditCard, "2");
                    var email = await process.Do(() => GetEmail(companyId), "3");
                    await process.Do(() => SendEmail(email, $"{creditCard} was assigned to you"), "4");

                    var onCheckTime = process.Delay(TimeSpan.FromDays(30), "5");
                    var onFirstTransaction = process.Do(() => OnCreditCardTransaction(creditCard), "6");
                    var onDeleted = process.Do(() => OnCreditCardDeleted(creditCard), "7");
                    while (true)
                    {
                        await Task.WhenAny(onCheckTime, onFirstTransaction, onDeleted);
                        if (onDeleted.IsCompleted)
                        {
                            await process.Do(() => SendEmail(email, $"{creditCard} was deleted"), "8");
                            return;
                        }
                        else if (onFirstTransaction.IsCompleted)
                        {
                            await process.Do(() => SendEmail(email, $"{creditCard} received 1st transaction"), "9");
                        }
                        else if (onCheckTime.IsCompleted && !onFirstTransaction.IsCompleted)
                        {
                            await process.Do(() => SendEmail(email, $"{creditCard} is inactive long time"), "10");
                        }
                    }
                }
            }

            public void RegisterProcess() => _host.SetEntryPoint(id => id.StartsWith(nameof(CreditCardFlow)), id => Run(id));

            private async Task<string> GetEmail(string companyId)
            {
                await EmulateAsync();
                return "test@text.com";
            }

            private async Task SendEmail(string email, string text)
            {
                await EmulateAsync();
                Console.WriteLine($"Email '{text}' was sent to {email}");
            }

            private async Task OnCreditCardTransaction(string creditCard)
            {
                await EmulateAsync();
                await Task.Delay(TimeSpan.FromHours(1));
                Console.WriteLine($"'{creditCard}' has transaction");
            }

            private async Task OnCreditCardDeleted(string creditCard)
            {
                await EmulateAsync();
                await Task.Delay(TimeSpan.FromHours(2));
                Console.WriteLine($"'{creditCard}' has been deleted");
            }

            private static Task EmulateAsync() => Task.Delay(5);
        }
    }
}
