using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Gaev.DurableTask.Storage;
using Gaev.DurableTask.Tests.Storage;
using NUnit.Framework;
#pragma warning disable CS1998

namespace Gaev.DurableTask.Tests
{
    public class ProcessExtensionsTests
    {
        [Test]
        public async Task It_should_execute_void_task_once()
        {
            // Given
            var proc = NewProcess();
            var count = 0;
            Func<Task> act = async () => { count++; };

            // When
            await proc.Do(act, "op1");
            await proc.Do(act, "op1");

            // Then
            Assert.AreEqual(1, count);
        }

        [Test]
        public async Task It_should_get_value()
        {
            // Given
            var proc = NewProcess();

            // When
            var actual1 = await proc.Get(123, "op1");
            var actual2 = await proc.Get(0, "op1");

            // Then
            Assert.AreEqual(123, actual1);
            Assert.AreEqual(123, actual2);
        }

        [Test]
        public async Task It_should_override_value()
        {
            // Given
            var proc = NewProcess();

            // When
            var actual1 = await proc.Get(123, "op1");
            await proc.Set(456, "op1");
            var actual2 = await proc.Get(0, "op1");

            // Then
            Assert.AreEqual(123, actual1);
            Assert.AreEqual(456, actual2);
        }

        [Test]
        public async Task It_should_delay_execution()
        {
            // Given
            var proc = NewProcess();

            // When
            var duration = Stopwatch.StartNew();
            var actual = await proc.Delay(TimeSpan.FromMilliseconds(300), "op1");
            await proc.Delay(TimeSpan.FromMilliseconds(300), "op1");
            duration.Stop();

            // Then
            Assert.Less(duration.ElapsedMilliseconds, 500);
            Assert.AreEqual(true, actual.OnTime);
        }

        [Test]
        public async Task It_should_not_delay_2nd_time()
        {
            // Given
            var proc = NewProcess();
            await proc.Delay(TimeSpan.FromMilliseconds(500), "op1");

            // When
            var duration = Stopwatch.StartNew();
            await proc.Delay(TimeSpan.FromMilliseconds(500), "op1");
            duration.Stop();

            // Then
            Assert.Less(duration.ElapsedMilliseconds, 500);
        }

        [Test]
        public async Task It_should_resume_delay_execution()
        {
            // Given
            var cancellation = new CancellationTokenSource();
            var storage = new InMemoryJsonProcessStorage();
            var proc = NewProcess(storage: storage, cancellation: cancellation.Token);

            // When
            var _ = proc.Delay(TimeSpan.FromMilliseconds(300), "op1");
            cancellation.Cancel();
            await Task.Delay(300);
            proc = NewProcess(storage: storage);
            var actual = await proc.Delay(TimeSpan.FromMilliseconds(300), "op1");

            // Then
            Assert.AreEqual(false, actual.OnTime);
        }

        [Test]
        public async Task It_should_cancel_delay_execution()
        {
            // Given
            var cancellation = new CancellationTokenSource();
            var proc = NewProcess(cancellation: cancellation.Token);

            // When
            var delayTask = proc.Delay(TimeSpan.FromDays(1), "op1");
            Func<Task> act = () => delayTask;
            cancellation.Cancel();

            // Then
            await ShouldThrow<OperationCanceledException>(act);
        }

        private static Process NewProcess(string id = "", IProcessStorage storage = null, CancellationToken cancellation = default(CancellationToken), Action<string> onDisposed = null)
        {
            storage = storage ?? new InMemoryJsonProcessStorage();
            onDisposed = onDisposed ?? (_ => { });
            return new Process(id, storage, cancellation, onDisposed);
        }

        private static async Task<T> ShouldThrow<T>(Func<Task> act, Func<T, bool> condition = null) where T : Exception
        {
            try
            {
                await act();
            }
            catch (T ex)
            {
                if (condition == null || condition(ex))
                    return ex;
            }
            throw new AssertionException($"Exception {typeof(T).Name} was not thrown");
        }
    }
}
