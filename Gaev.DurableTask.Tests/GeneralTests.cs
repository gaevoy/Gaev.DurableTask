using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Gaev.DurableTask.Tests.Examples;
using Gaev.DurableTask.Tests.Storage;
using NUnit.Framework;
#pragma warning disable 1998

namespace Gaev.DurableTask.Tests
{
    public class GeneralTests
    {
        [Test]
        public async Task ShouldRun()
        {
            // Given
            var host = new ProcessHost(new InMemoryJsonProcessStorage()).WithoutRegistration();
            await host.Start();
            var duration = Stopwatch.StartNew();
            var now = DateTime.UtcNow;
            var delay = TimeSpan.FromMilliseconds(300);

            // When
            var date = await new DelayedJobHandler(host).StartJob(delay);
            duration.Stop();

            // Then
            Assert.Greater(date - now, delay);
            Assert.Greater(duration.Elapsed, delay);
        }

        [Test]
        public async Task ShouldRestore()
        {
            // Given
            var storage = new InMemoryJsonProcessStorage();
            var host = new ProcessHost(storage).WithoutRegistration();
            await host.Start();
            var processId = Guid.NewGuid().ToString();
            var actualInput = Guid.NewGuid().ToString();

            // When
            var onFirstCalled = new TaskCompletionSource<object>();
            var _ = TestProcess(host, actualInput, __ =>
            {
                onFirstCalled.SetResult(null);
                return Task.Delay(50000); // exit emulation
            }, processId);
            await onFirstCalled.Task;
            host = new ProcessHost(storage);
            var onRestored = new TaskCompletionSource<string>();
            host.Register(id => id == processId, id => TestProcess(host, null, input =>
            {
                onRestored.SetResult(input);
                return Task.CompletedTask;
            }, id));
            await host.Start();
            var expectedInput = await onRestored.Task;

            // Then
            Assert.AreEqual(expectedInput, actualInput);
        }

        [Test]
        public async Task ShouldRestoreException()
        {
            // Given
            var storage = new InMemoryJsonProcessStorage();
            var host = new ProcessHost(storage).WithoutRegistration();
            await host.Start();
            var processId = Guid.NewGuid().ToString();

            // When
            var onFirstCalled = new TaskCompletionSource<ProcessException>();
            var _ = TestProcessWithError(host, async exception =>
            {
                onFirstCalled.SetResult(exception);
                await Task.Delay(50000); // exit emulation
            }, processId);
            await onFirstCalled.Task;
            host = new ProcessHost(storage);
            var onRestored = new TaskCompletionSource<ProcessException>();
            host.Register(id => id == processId, id => TestProcessWithError(host, exception =>
            {
                onRestored.SetResult(exception);
                return Task.CompletedTask;
            }, id));
            await host.Start();
            await onRestored.Task;

            // Then
            Assert.AreEqual(onFirstCalled.Task.Result.Type, onRestored.Task.Result.Type);
            Assert.AreEqual(onFirstCalled.Task.Result.Message, onRestored.Task.Result.Message);
        }

        [Test]
        public async Task ShouldComplete()
        {
            // Given
            var storage = new InMemoryJsonProcessStorage();
            var host = new ProcessHost(storage).WithoutRegistration();
            await host.Start();
            var processId = Guid.NewGuid().ToString();

            // When
            await TestProcessCompletion(host, () => { }, processId);
            host = new ProcessHost(storage);
            var isRestored = false;
            host.Register(id => id == processId, id => TestProcessCompletion(host, () =>
            {
                isRestored = true;
            }, id));
            await host.Start();
            await Task.Delay(1000);

            // Then
            Assert.IsFalse(isRestored);
        }

        private async Task TestProcess(IProcessHost host, string input, Func<string, Task> callback, string id)
        {
            using (var process = host.Spawn(id))
            {
                input = await process.Attach(input, "1");
                await callback(input);
            }
        }

        private async Task TestProcessWithError(IProcessHost host, Func<ProcessException, Task> callback, string id)
        {
            using (var process = host.Spawn(id))
            {
                try
                {
                    await process.Do(() => Task.Delay(100), "1");
                    await process.Do(async () => { throw new ApplicationException(Guid.NewGuid().ToString()); }, "2");
                }
                catch (ProcessException e)
                {
                    await callback(e);
                    throw;
                }
            }
        }

        private async Task TestProcessCompletion(IProcessHost host, Action callback, string id)
        {
            using (var process = host.Spawn(id))
            {
                await process.Do(() => Task.Delay(100), "1");
                callback();
            }
        }
    }

    public static class ProcessHostExt
    {
        public static ProcessHost WithoutRegistration(this ProcessHost processHost)
        {
            processHost.Register(new ProcessRegistration
            {
                IdSelector = id => true,
                EntryPoint = id => Task.CompletedTask
            });

            return processHost;
        }
    }
}
