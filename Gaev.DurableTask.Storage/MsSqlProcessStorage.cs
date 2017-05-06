using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Gaev.DurableTask.Storage
{
    public class MsSqlProcessStorage : IProcessStorage
    {
        private readonly string _connectionString;
        private static readonly string Ns = "Gaev.DurableTask.Storage.Sql.";
        private static readonly string EnsureTableCreatedQuery = ReadEmbeddedFile(Ns + "EnsureTableCreated.sql");
        private static readonly string SetQuery = ReadEmbeddedFile(Ns + "Set.sql");
        private static readonly string GetQuery = ReadEmbeddedFile(Ns + "Get.sql");
        private static readonly string GetPendingProcessIdsQuery = ReadEmbeddedFile(Ns + "GetPendingProcessIds.sql");
        private static readonly string DeleteProcessQuery = ReadEmbeddedFile(Ns + "DeleteProcess.sql");
        private readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter> { new ProcessExceptionSerializer(), new VoidSerializer() }
        };

        public MsSqlProcessStorage(string connectionString)
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
            var isException = state.Exception != null;
            var stateJson = JsonConvert.SerializeObject(isException ? (object)state.Exception : state.Value, _jsonSettings);
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                var cmd = con.CreateCommand();
                cmd.CommandText = SetQuery;
                AddParameter(cmd, "ProcessId", processId);
                AddParameter(cmd, "OperationId", operationId);
                AddParameter(cmd, "IsException", isException);
                AddParameter(cmd, "State", stateJson);
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

        public async Task<OperationState<T>> Get<T>(string processId, string operationId)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                SqlCommand cmd = con.CreateCommand();
                cmd.CommandText = GetQuery;
                AddParameter(cmd, "ProcessId", processId);
                AddParameter(cmd, "OperationId", operationId);
                var reader = await cmd.ExecuteReaderAsync();
                using (reader)
                {
                    OperationState<T> result = null;
                    while (reader.Read())
                    {
                        var isException = (bool)reader["IsException"];
                        var state = (string)reader["State"];
                        if (isException)
                            return new OperationState<T>
                            {
                                Exception = JsonConvert.DeserializeObject<ProcessException>(state, _jsonSettings)
                            };
                        return new OperationState<T>
                        {
                            Value = JsonConvert.DeserializeObject<T>(state, _jsonSettings)
                        };
                    }
                    return null;
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
