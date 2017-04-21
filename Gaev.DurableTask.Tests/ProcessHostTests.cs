using System;
using System.Threading.Tasks;
using Gaev.DurableTask.Tests.Storage;
using NUnit.Framework;

namespace Gaev.DurableTask.Tests
{
    public class ProcessHostTests
    {
        [Test]
        public async Task It_should_spawn_a_process()
        {
            // Given
            var host = NewProcessHost().WithoutRegistration();
            host.Resume();
            var processId = Guid.NewGuid().ToString();

            // When
            var proc = host.Spawn(processId);
            await proc.Set(123, "op1");
            var actual = await host.Get(processId).Get(0, "op1");

            // Then
            Assert.AreEqual(123, actual);
        }

        [Test]
        public void It_should_spawn_wrapper_of_process()
        {
            // Given
            var host = NewProcessHost();
            host.Register(new ProcessRegistration
            {
                IdSelector = id => true,
                EntryPoint = id => Task.CompletedTask,
                ProcessWrapper = p => new TestProcess(p)
            });
            host.Resume();

            // When
            var proc = host.Spawn("1");

            // Then
            Assert.IsInstanceOf<TestProcess>(proc);
            Assert.IsNotNull(proc.As<TestProcess>().Original);
        }

        [Test]
        public void It_should_remove_disposed_process()
        {
            // Given
            var host = NewProcessHost().WithoutRegistration();
            host.Resume();
            var processId = Guid.NewGuid().ToString();
            var proc = host.Spawn(processId);

            // When
            proc.Dispose();
            var actual = host.Get(processId);

            // Then
            Assert.IsNull(actual);
        }

        [Test]
        public async Task It_should_resume_pending_task()
        {
            // Given
            var storage = new InMemoryJsonProcessStorage();
            var processId = Guid.NewGuid().ToString();
            var host = new ProcessHost(storage).WithoutRegistration();
            host.Resume();
            await host.Spawn(processId).Set(123, "op1");

            // When
            var actual = 0;
            var isStarted = false;
            var onDone = new TaskCompletionSource<int>();
            host = new ProcessHost(storage);
            host.Register(new ProcessRegistration
            {
                IdSelector = id => id == processId,
                EntryPoint = async id =>
                {
                    var proc = host.Spawn(id);
                    isStarted = true;
                    actual = await proc.Get(0, "op1");
                    onDone.SetResult(0);
                }
            });
            host.Resume();

            // Then
            Assert.IsTrue(isStarted);
            await onDone.Task;
            Assert.AreEqual(123, actual);
        }

        [Test]
        public async Task It_should_not_resume_disposed_task()
        {
            // Given
            var storage = new InMemoryJsonProcessStorage();
            var processId = Guid.NewGuid().ToString();
            var host = new ProcessHost(storage).WithoutRegistration();
            host.Resume();
            var proc = host.Spawn(processId);
            await proc.Set(123, "op1");
            proc.Dispose();

            // When
            var isStarted = false;
            host = new ProcessHost(storage);
            host.Register(new ProcessRegistration
            {
                IdSelector = id => id == processId,
                EntryPoint = async id =>
                {
                    var process = host.Spawn(id);
                    isStarted = true;
                    await Task.Delay(100);
                }
            });
            host.Resume();

            // Then
            Assert.IsFalse(isStarted);
        }

        [Test]
        public async Task It_should_cancel_resumed_processes_if_disposed()
        {
            // Given
            var storage = new InMemoryJsonProcessStorage();
            var processId = Guid.NewGuid().ToString();
            var host = new ProcessHost(storage).WithoutRegistration();
            host.Resume();
            await host.Spawn(processId).Set(123, "op1");
            var onDone = new TaskCompletionSource<int>();
            host = new ProcessHost(storage);
            host.Register(new ProcessRegistration
            {
                IdSelector = id => id == processId,
                EntryPoint = async id =>
                {
                    try
                    {
                        var proc = host.Spawn(id);
                        await Task.Delay(TimeSpan.FromDays(1), proc.Cancellation);
                    }
                    catch (OperationCanceledException)
                    {
                        onDone.SetResult(0);
                    }
                }
            });
            host.Resume();

            // When
            host.Dispose();

            // Then
            Assert.IsTrue(onDone.Task.IsCompleted);
        }

        [Test]
        public void It_should_cancel_new_processes_if_disposed()
        {
            // Given
            var host = NewProcessHost().WithoutRegistration();
            host.Resume();
            var onDone = new TaskCompletionSource<int>();
            host.Watch(new Func<Task>(async () =>
            {
                try
                {
                    var proc = host.Spawn("1");
                    await Task.Delay(TimeSpan.FromDays(1), proc.Cancellation);
                }
                catch (OperationCanceledException)
                {
                    onDone.SetResult(0);
                }
            })());

            // When
            host.Dispose();

            // Then
            Assert.IsTrue(onDone.Task.IsCompleted);
        }

        public ProcessHost NewProcessHost()
        {
            return new ProcessHost(new InMemoryJsonProcessStorage());
        }

        public class TestProcess : ProcessWrapper
        {
            public IProcess Original { get; }
            public TestProcess(IProcess underlying) : base(underlying)
            {
                Original = underlying;
            }
        }
    }
}
