using System;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Threading.Tasks;
using Gaev.DurableTask.Storage;
using Gaev.DurableTask.Tests.Storage;
using NUnit.Framework;

namespace Gaev.DurableTask.Tests
{
    [TestFixture("File")]
    [TestFixture("MsSqlWithCache")]
    [TestFixture("MsSql")]
    [TestFixture("InMemory")]
    [TestFixture("InMemoryJson")]
    public class ProcessStorageTests
    {
        private readonly string _provider;

        public ProcessStorageTests(string provider)
        {
            _provider = provider;
        }

        [Test]
        public async Task It_should_save_guid()
        {
            // Given
            var storage = CreateProcessStorage();
            var processId = Guid.NewGuid().ToString();
            var operationId = Guid.NewGuid().ToString();
            var state = new OperationState<Guid> { Value = Guid.NewGuid() };

            // When
            await storage.Set(processId, operationId, state);

            // Then
            var actual = await storage.Get<Guid>(processId, operationId);
            Assert.IsNotNull(actual);
            Assert.AreEqual(state.Value, actual.Value);
        }

        [Test]
        public async Task It_should_save_process_exception()
        {
            // Given
            var storage = CreateProcessStorage();
            var processId = Guid.NewGuid().ToString();
            var operationId = Guid.NewGuid().ToString();
            var state = new OperationState<int> { Exception = new ProcessException(Guid.NewGuid().ToString(), Guid.NewGuid().ToString()) };

            // When
            await storage.Set(processId, operationId, state);

            // Then
            var actual = await storage.Get<int>(processId, operationId);
            Assert.IsNotNull(actual);
            Assert.AreEqual(state.Exception.Message, actual.Exception.Message);
            Assert.AreEqual(state.Exception.Type, actual.Exception.Type);
        }

        [Test]
        public async Task It_should_save_custom_type()
        {
            // Given
            var storage = CreateProcessStorage();
            var processId = Guid.NewGuid().ToString();
            var operationId = Guid.NewGuid().ToString();
            var state = new OperationState<TestType1> { Value = new TestType1 { Val1 = "1", Val3 = 3 } };

            // When
            await storage.Set(processId, operationId, state);

            // Then
            var actual = await storage.Get<TestType1>(processId, operationId);
            Assert.IsNotNull(actual);
            Assert.AreEqual(state.Value.Val1, actual.Value.Val1);
            Assert.IsNull(actual.Value.Val2);
            Assert.AreEqual(state.Value.Val3, actual.Value.Val3);
        }

        [Test]
        public async Task It_should_save_void()
        {
            // Given
            var storage = CreateProcessStorage();
            var processId = Guid.NewGuid().ToString();
            var operationId = Guid.NewGuid().ToString();
            var state = new OperationState<Void> { Value = Void.Nothing };

            // When
            await storage.Set(processId, operationId, state);

            // Then
            var actual = await storage.Get<Void>(processId, operationId);
            Assert.IsNotNull(actual);
            Assert.AreSame(state.Value, actual.Value);
        }

        [Test]
        public async Task It_should_override()
        {
            // Given
            var storage = CreateProcessStorage();
            var processId = Guid.NewGuid().ToString();
            var operationId = Guid.NewGuid().ToString();
            var state = new OperationState<Guid> { Value = Guid.NewGuid() };

            // When
            await storage.Set(processId, operationId, new OperationState<Guid> { Value = Guid.Empty });
            await storage.Set(processId, operationId, state);

            // Then
            var actual = await storage.Get<Guid>(processId, operationId);
            Assert.IsNotNull(actual);
            Assert.AreEqual(state.Value, actual.Value);
        }

        [Test]
        public async Task It_should_get_null()
        {
            // Given
            var storage = CreateProcessStorage();
            var processId = Guid.NewGuid().ToString();
            var operationId = Guid.NewGuid().ToString();

            // When
            var actual = await storage.Get<string>(processId, operationId);

            // Then
            Assert.IsNull(actual);
        }

        [Test]
        public async Task It_should_get_pending_process_ids()
        {
            // Given
            var storage = CreateProcessStorage();
            var processIds = new[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString() };
            foreach (var processId in processIds)
                await storage.Set(processId, "_", new OperationState<string> { Value = "123" });

            // When
            var actual = storage.GetPendingProcessIds();

            // Then
            CollectionAssert.AreEquivalent(processIds, actual);
        }

        [Test]
        public async Task It_should_clean_process()
        {
            // Given
            var storage = CreateProcessStorage();
            var processId1 = Guid.NewGuid().ToString();
            var processId2 = Guid.NewGuid().ToString();
            await storage.Set(processId1, "_", new OperationState<string> { Value = "123" });
            await storage.Set(processId2, "_", new OperationState<string> { Value = "123" });

            // When
            storage.CleanProcess(processId1);

            // Then
            var actual1 = await storage.Get<string>(processId1, "_");
            var actual2 = await storage.Get<string>(processId2, "_");
            Assert.IsNull(actual1);
            Assert.IsNotNull(actual2);
            CollectionAssert.AreEquivalent(new [] { processId2 }, storage.GetPendingProcessIds());
        }

        private class TestType1
        {
            public string Val1 { get; set; }
            public string Val2 { get; set; }
            public int Val3 { get; set; }
        }

        private IProcessStorage CreateProcessStorage()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["Sql"].ConnectionString;
            switch (_provider)
            {
                case "InMemory": return new InMemoryProcessStorage();
                case "InMemoryJson": return new InMemoryJsonProcessStorage();
                case "File": return new FileSystemProcessStorage(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
                case "MsSqlWithCache":
                    CleanDb(connectionString);
                    return new MsSqlProcessStorageWithCache(connectionString);
                case "MsSql":
                    CleanDb(connectionString);
                    return new MsSqlProcessStorage(connectionString);
                default: throw new NotImplementedException();
            }
        }

        private void CleanDb(string connectionString)
        {
            using (var con = new SqlConnection(connectionString))
            {
                con.Open();
                var cmd = con.CreateCommand();
                cmd.CommandText = "DELETE FROM [dbo].[DurableTasks]";
                cmd.ExecuteNonQuery();
            }
        }
    }
}
