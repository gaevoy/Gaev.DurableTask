using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Gaev.DurableTask.Storage;
using Gaev.DurableTask.Tests.Storage;
using NUnit.Framework;
// ReSharper disable AccessToModifiedClosure

namespace Gaev.DurableTask.Tests
{
    public class ProcessTests
    {
        [Test]
        public async Task It_should_execute_task_once_for_same_id()
        {
            // Given
            var process = NewProcess();
            var value = 1;
            var count = 0;

            // When
            Func<Task<int>> act = async () =>
            {
                await Task.Delay(1);
                count++;
                return value;
            };
            var actual1 = await process.Do(act, "op1");
            value = 2;
            var actual2 = await process.Do(act, "op1");

            // Then
            Assert.AreEqual(1, actual1);
            Assert.AreEqual(1, actual2);
            Assert.AreEqual(1, count);
        }

        [Test]
        public async Task It_should_execute_task_every_time()
        {
            // Given
            var process = NewProcess();
            var value = 1;
            var count = 0;

            // When
            Func<Task<int>> act = async () =>
            {
                await Task.Delay(1);
                count++;
                return value;
            };
            var actual1 = await process.Do(act, "op1", redo: true);
            value = 2;
            var actual2 = await process.Do(act, "op1", redo: true);

            // Then
            Assert.AreEqual(1, actual1);
            Assert.AreEqual(2, actual2);
            Assert.AreEqual(2, count);
        }

        [Test]
        public async Task It_should_execute_task_for_different_ids()
        {
            // Given
            var process = NewProcess();
            var value = 1;
            var count = 0;

            // When
            Func<Task<int>> act = async () =>
            {
                await Task.Delay(1);
                count++;
                return value;
            };
            var actual1 = await process.Do(act, "op1");
            value = 2;
            var actual2 = await process.Do(act, "op2");

            // Then
            Assert.AreEqual(1, actual1);
            Assert.AreEqual(2, actual2);
            Assert.AreEqual(2, count);
        }

        [Test]
        public async Task It_should_return_task_result_after_1st_run()
        {
            // Given
            var process = NewProcess();

            // When
            var actual1 = await process.Do(() => Task.FromResult(123), "op1");
            var actual2 = await process.Do(() => Task.FromResult(0), "op1");

            // Then
            Assert.AreEqual(123, actual1);
            Assert.AreEqual(123, actual2);
        }

        [Test]
        public async Task It_should_throw_ProcessException()
        {
            // Given
            var process = NewProcess();

            // When
            Func<Task> act1 = () => process.Do(() => Task.FromException<int>(new MyTestException123("456")), "op1");
            Func<Task> act2 = () => process.Do(() => Task.FromResult(0), "op1");

            // Then
            await ShouldThrow<ProcessException>(act1, ex => ex.Type == nameof(MyTestException123) && ex.Message == "456");
            await ShouldThrow<ProcessException>(act2, ex => ex.Type == nameof(MyTestException123) && ex.Message == "456");
        }

        [Test]
        public async Task It_should_override_result_of_throw_ProcessException()
        {
            // Given
            var process = NewProcess();

            // When
            Func<Task> act1 = () => process.Do(() => Task.FromException<int>(new MyTestException123("456")), "op1");
            Func<Task> act2 = () => process.Do(() => Task.FromException<int>(new MyTestException123("789")), "op1", redo: true);

            // Then
            await ShouldThrow<ProcessException>(act1, ex => ex.Type == nameof(MyTestException123) && ex.Message == "456");
            await ShouldThrow<ProcessException>(act2, ex => ex.Type == nameof(MyTestException123) && ex.Message == "789");
        }

        [Test]
        public async Task It_should_dispose_the_process()
        {
            // Given
            var storage = new InMemoryJsonProcessStorage();
            var disposedIds = new List<string>();
            var process1 = NewProcess("1", storage, onDisposed: disposedIds.Add);
            var process2 = NewProcess("2", storage, onDisposed: disposedIds.Add);
            await process1.Do(() => Task.FromResult(123), "op1");
            await process2.Do(() => Task.FromResult(123), "op1");

            // When
            process1.Dispose();
            var actual1 = await process1.Do(() => Task.FromResult(456), "op1");
            var actual2 = await process2.Do(() => Task.FromResult(456), "op1");

            // Then
            Assert.AreEqual(456, actual1);
            Assert.AreEqual(123, actual2);
            CollectionAssert.AreEqual(new[] { "1" }, disposedIds);
        }

        [Test]
        public async Task It_should_cancel_pending_tasks_and_ignore_to_dispose()
        {
            // Given
            var cancellation = new CancellationTokenSource();
            var process = NewProcess(cancellation: cancellation.Token);
            await process.Do(() => Task.FromResult(123), "op1");

            // When
            var job = new TaskCompletionSource<int>();
            process.Cancellation.Register(() => job.TrySetCanceled());
            var longRunningTask = process.Do(() => job.Task, "op2");
            cancellation.Cancel();
            try
            {
                await longRunningTask;
            }
            catch (OperationCanceledException) { }
            process.Dispose();
            var actual1 = await process.Do(() => Task.FromResult(456), "op1");
            var actual2 = await process.Do(() => Task.FromResult(456), "op2");

            // Then
            Assert.AreEqual(123, actual1);
            Assert.AreEqual(456, actual2);
        }

        private static Process NewProcess(string id = "", IProcessStorage storage = null, CancellationToken cancellation = default(CancellationToken), Action<string> onDisposed = null)
        {
            storage = storage ?? new InMemoryJsonProcessStorage();
            onDisposed = onDisposed ?? (_ => { });
            return new Process(id, storage, cancellation, onDisposed);
        }

        public class MyTestException123 : Exception
        {
            public MyTestException123(string message) : base(message) { }
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
