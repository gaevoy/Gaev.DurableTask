using System;
using System.Threading;
using System.Threading.Tasks;
using Gaev.DurableTask.Tests.Storage;
using NUnit.Framework;
// ReSharper disable AccessToModifiedClosure
// ReSharper disable AccessToDisposedClosure
#pragma warning disable 4014
#pragma warning disable 1998

namespace Gaev.DurableTask.Tests
{
    public class ProcessFlowTests
    {
        [Test]
        public async Task Host()
        {
            // Given
            var host = new ProcessHost(new InMemoryJsonProcessStorage());
            var companyId = Guid.NewGuid().ToString();
            var creditCard = "123";
            var creditCardFlow = new CreditCardFlow(host);
            creditCardFlow.RegisterProcess();
            await host.Start();
            var processId = creditCardFlow.Start(companyId, creditCard);
            var process = (CreditCardFlow.MyProcess)host.Get(processId);

            // When
            process.RaiseOnTransactionAppeared();
            //process.RaiseOnCreditCardDeleted();
            await Task.Delay(3000);
            Console.WriteLine("Pause");
            await Task.Delay(3000);
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
                using (var process = _host.Spawn(processId).As<MyProcess>())
                {
                    companyId = await process.Attach(companyId, "1");
                    creditCard = await process.Attach(creditCard, "2");
                    var email = await process.Do(() => GetEmail(companyId), "3");
                    await process.Do(() => SendEmail(email, $"{creditCard} was assigned to you"), "4");
                    var onCheckTime = process.Delay(TimeSpan.FromSeconds(5), "5");
                    var onFirstTransaction = process.Do(() => process.OnTransactionAppeared(), "6");
                    var onDeleted = process.Do(() => process.OnCreditCardDeleted(), "7");
                    // How to count 2nd or 100th transaction to send Congrats message?
                    Task.Run(async () =>
                    {
                        await onCheckTime;
                        if (onDeleted.IsCompleted) return;
                        if (!onFirstTransaction.IsCompleted)
                            await process.Do(() => SendEmail(email, $"{creditCard} is inactive long time"), "8");
                    });
                    Task.Run(async () =>
                    {
                        await onFirstTransaction;
                        if (onDeleted.IsCompleted) return;
                        await process.Do(() => SendEmail(email, $"{creditCard} received 1st transaction"), "9");
                    });

                    await onDeleted;
                    // Cancel all pending tasks
                    await process.Do(() => SendEmail(email, $"{creditCard} was deleted"), "10");
                }
            }

            public void RegisterProcess() => _host.Register(new ProcessRegistration
            {
                IdSelector = id => id.StartsWith(nameof(CreditCardFlow)),
                EntryPoint = id => Run(id),
                ProcessWrapper = p => new MyProcess(p)
            });

            private async Task<string> GetEmail(string companyId)
            {
                await EmulateAsync();
                return "companyId@test.com";
            }

            private async Task SendEmail(string email, string text)
            {
                await EmulateAsync();
                Console.WriteLine($"Email '{text}' was sent to {email}");
            }

            private static Task EmulateAsync() => Task.Delay(5);

            public class MyProcess : IProcess
            {
                private readonly IProcess _underlying;

                public MyProcess(IProcess underlying)
                {
                    _underlying = underlying;
                }

                public CancellationToken Cancellation => _underlying.Cancellation;
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
    }
}
