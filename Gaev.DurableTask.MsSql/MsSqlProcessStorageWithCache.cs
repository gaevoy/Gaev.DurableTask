using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Threading.Tasks;
using Gaev.DurableTask.Json;
using Gaev.DurableTask.Storage;
using Newtonsoft.Json;

namespace Gaev.DurableTask.MsSql
{
    public class MsSqlProcessStorageWithCache : IProcessStorage
    {
        private readonly string _connectionString;
        private static readonly string Ns = "Gaev.DurableTask.MsSql.Sql.";
        private static readonly string EnsureTableCreatedQuery = ReadEmbeddedFile(Ns + "EnsureTableCreated.sql");
        private static readonly string SetQuery = ReadEmbeddedFile(Ns + "Set.sql");
        private static readonly string GetQuery = ReadEmbeddedFile(Ns + "Get.sql");
        private static readonly string GetPendingProcessIdsQuery = ReadEmbeddedFile(Ns + "GetPendingProcessIds.sql");
        private static readonly string DeleteProcessQuery = ReadEmbeddedFile(Ns + "DeleteProcess.sql");
        private readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter> { new ProcessExceptionSerializer(), new VoidSerializer() }
        };

        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, OperationState>> _cache = new ConcurrentDictionary<string, ConcurrentDictionary<string, OperationState>>();
        public class OperationState
        {
            public bool IsException { get; set; }
            public string State { get; set; }
        }

        public MsSqlProcessStorageWithCache(string connectionString)
        {
            _connectionString = connectionString;
            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                var cmd = con.CreateCommand();
                cmd.CommandText = EnsureTableCreatedQuery;
                cmd.ExecuteNonQuery();
            }
        }

        public async Task Set<T>(string processId, string operationId, OperationState<T> state)
        {
            var op = _cache.GetOrAdd(processId, _ => GetOperations(processId)).GetOrAdd(operationId, new OperationState());
            op.IsException = state.Exception != null;
            op.State = JsonConvert.SerializeObject(op.IsException ? (object)state.Exception : state.Value, _jsonSettings);
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                var cmd = con.CreateCommand();
                cmd.CommandText = SetQuery;
                AddParameter(cmd, "ProcessId", processId);
                AddParameter(cmd, "OperationId", operationId);
                AddParameter(cmd, "IsException", op.IsException);
                AddParameter(cmd, "State", op.State);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public void CleanProcess(string processId)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                var cmd = con.CreateCommand();
                AddParameter(cmd, "ProcessId", processId);
                cmd.CommandText = DeleteProcessQuery;
                cmd.ExecuteNonQuery();
            }
        }

        public IEnumerable<string> GetPendingProcessIds()
        {
            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                SqlCommand cmd = con.CreateCommand();
                cmd.CommandText = GetPendingProcessIdsQuery;
                var reader = cmd.ExecuteReader();
                var processIds = new List<string>();
                using (reader)
                    while (reader.Read())
                        processIds.Add((string)reader["ProcessId"]);
                return processIds;
            }
        }

        public Task<OperationState<T>> Get<T>(string processId, string operationId)
        {
            OperationState op;
            OperationState<T> result = null;
            if (_cache.GetOrAdd(processId, _ => GetOperations(processId)).TryGetValue(operationId, out op))
            {
                if (op.IsException)
                    result = new OperationState<T>
                    {
                        Exception = JsonConvert.DeserializeObject<ProcessException>(op.State, _jsonSettings)
                    };
                else
                    result = new OperationState<T>
                    {
                        Value = JsonConvert.DeserializeObject<T>(op.State, _jsonSettings)
                    };
            }
            return Task.FromResult(result);
        }

        public ConcurrentDictionary<string, OperationState> GetOperations(string processId)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                SqlCommand cmd = con.CreateCommand();
                cmd.CommandText = @"
SELECT IsException, [State], OperationId
FROM DurableTasks
WHERE ProcessId = @ProcessId";
                AddParameter(cmd, "ProcessId", processId);
                var reader = cmd.ExecuteReader();
                using (reader)
                {
                    var result = new ConcurrentDictionary<string, OperationState>();
                    while (reader.Read())
                    {
                        var operationId = (string)reader["OperationId"];
                        result[operationId] = new OperationState
                        {
                            IsException = (bool)reader["IsException"],
                            State = (string)reader["State"]
                        };
                    }
                    return result;
                }
            }
        }

        private static string ReadEmbeddedFile(string fileName)
        {
            using (var stream = typeof(MsSqlProcessStorage).Assembly.GetManifestResourceStream(fileName))
            {
                stream.Position = 0;
                return new StreamReader(stream).ReadToEnd();
            }
        }

        private static void AddParameter(SqlCommand cmd, string parameterName, object value)
        {
            var parameter = cmd.CreateParameter();
            parameter.ParameterName = parameterName;
            if (value == null)
            {

            }
            else if (value is string)
                parameter.DbType = DbType.String;
            else if (value is bool)
                parameter.DbType = DbType.Boolean;
            else
                throw new NotImplementedException();
            parameter.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(parameter);
        }
    }
}
