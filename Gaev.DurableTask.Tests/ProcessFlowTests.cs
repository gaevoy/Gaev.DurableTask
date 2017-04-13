using System;
using System.Collections.Generic;
using System.Threading;
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
                    companyId = await process.Attach(companyId, "1");
                    creditCard = await process.Attach(creditCard, "2");
                    var email = await process.Do(() => GetEmail(companyId), "3");
                    await process.Do(() => SendEmail(email, $"{creditCard} was assigned to you"), "4");
                    var onCheckTime = process.Delay(TimeSpan.FromMinutes(5), "5");
                    var subscription = process.Subscribe<string>();
                    var onFirstTransaction = process.Do(() => subscription.On(msg => msg == "onFirstTransaction|" + creditCard), "6");
                    var onDeleted = process.Do(() => subscription.On(msg => msg == "onDeleted|" + creditCard), "7");
                    // How count 2nd or 100th transaction to send Congrats message?
                    var _ = Task.Run(async () =>
                    {
                        await onCheckTime;
                        if (!onFirstTransaction.IsCompleted)
                            await process.Do(() => SendEmail(email, $"{creditCard} is inactive long time"), "8");
                    });
                    var __ = Task.Run(async () =>
                    {
                        await onFirstTransaction;
                        await process.Do(() => SendEmail(email, $"{creditCard} received 1st transaction"), "9");
                    });
                    var ___ = subscription.StartReceiving();

                    await onDeleted;
                    await process.Do(() => SendEmail(email, $"{creditCard} was deleted"), "10");
                }
            }

            public void RegisterProcess() => _host.SetEntryPoint(id => id.StartsWith(nameof(CreditCardFlow)), id => Run(id));

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
        }
    }

    public static class SubscriptionExt
    {
        public static Subscription<TMessage> Subscribe<TMessage>(this IProcess process)
        {
            return new Subscription<TMessage>(process);
        }
    }

    public class Subscription<TMessage>
    {
        private readonly IProcess _process;
        private readonly List<Action<TMessage>> _handlers = new List<Action<TMessage>>();

        public Subscription(IProcess process)
        {
            _process = process;
        }

        public Task On(Func<TMessage, bool> handler)
        {
            var source = new TaskCompletionSource<TMessage>();
            _handlers.Add(msg => { if (handler(msg)) source.SetResult(msg); });
            return source.Task;
        }

        public async Task StartReceiving()
        {
            while (true)
            {
                var message = await _process.Receive<TMessage>();
                foreach (var handler in _handlers)
                    handler(message);
            }
        }
    }
}
