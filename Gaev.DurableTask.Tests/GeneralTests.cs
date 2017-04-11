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
            var factory = new ProcessFactory(new InMemoryJsonProcessStorage());
            var duration = Stopwatch.StartNew();
            var now = DateTime.UtcNow;
            var delay = TimeSpan.FromMilliseconds(300);

            // When
            var date = await new DelayedJobHandler(factory).StartJob(delay);
            duration.Stop();

            // Then
            Assert.Greater(date - now, delay);
            Assert.Greater(duration.Elapsed, delay);
        }

        [Test]
        public async Task ShouldRestore()
        {
            // Given
            var factory = new ProcessFactory(new InMemoryJsonProcessStorage());
            var processId = Guid.NewGuid().ToString();
            var actualInput = Guid.NewGuid().ToString();

            // When
            var onFirstCalled = new TaskCompletionSource<object>();
            var _ = TestProcess(factory, actualInput, __ =>
            {
                onFirstCalled.SetResult(null);
                return Task.Delay(50000); // exit emulation
            }, processId);
            await onFirstCalled.Task;
            var onRestored = new TaskCompletionSource<string>();
            factory.SetEntryPoint(id => id == processId, id => TestProcess(factory, null, input =>
            {
                onRestored.SetResult(input);
                return Task.CompletedTask;
            }, id));
            await factory.RunSuspended();
            var expectedInput = await onRestored.Task;

            // Then
            Assert.AreEqual(expectedInput, actualInput);
        }

        [Test]
        public async Task ShouldRestoreException()
        {
            // Given
            var factory = new ProcessFactory(new InMemoryJsonProcessStorage());
            var processId = Guid.NewGuid().ToString();

            // When
            var onFirstCalled = new TaskCompletionSource<ProcessException>();
            var _ = TestProcessWithError(factory, async exception =>
            {
                onFirstCalled.SetResult(exception);
                await Task.Delay(50000); // exit emulation
            }, processId);
            await onFirstCalled.Task;
            var onRestored = new TaskCompletionSource<ProcessException>();
            factory.SetEntryPoint(id => id == processId, id => TestProcessWithError(factory, exception =>
            {
                onRestored.SetResult(exception);
                return Task.CompletedTask;
            }, id));
            await factory.RunSuspended();
            await onRestored.Task;

            // Then
            Assert.AreEqual(onFirstCalled.Task.Result.Type, onRestored.Task.Result.Type);
            Assert.AreEqual(onFirstCalled.Task.Result.Message, onRestored.Task.Result.Message);
        }

        [Test]
        public async Task ShouldComplete()
        {
            // Given
            var factory = new ProcessFactory(new InMemoryJsonProcessStorage());
            var processId = Guid.NewGuid().ToString();

            // When
            await TestProcessCompletion(factory, () => { }, processId);
            var isRestored = false;
            factory.SetEntryPoint(id => id == processId, id => TestProcessCompletion(factory, () =>
            {
                isRestored = true;
            }, id));
            await factory.RunSuspended();
            await Task.Delay(1000);

            // Then
            Assert.IsFalse(isRestored);
        }

        private async Task TestProcess(IProcessFactory factory, string input, Func<string, Task> callback, string id)
        {
            using (var process = factory.Spawn(id))
            {
                input = await process.Attach(input, "1");
                await callback(input);
            }
        }

        private async Task TestProcessWithError(IProcessFactory factory, Func<ProcessException, Task> callback, string id)
        {
            using (var process = factory.Spawn(id))
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

        private async Task TestProcessCompletion(IProcessFactory factory, Action callback, string id)
        {
            using (var process = factory.Spawn(id))
            {
                await process.Do(() => Task.Delay(100), "1");
                callback();
            }
        }
    }
}
