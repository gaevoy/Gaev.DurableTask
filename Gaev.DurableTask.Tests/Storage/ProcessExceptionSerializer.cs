using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gaev.DurableTask.Tests.Storage
{
    public class ProcessExceptionSerializer : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var exception = (ProcessException)value;
            writer.WriteStartObject();
            writer.WritePropertyName("Type");
            serializer.Serialize(writer, exception.Type);
            writer.WritePropertyName("Message");
            serializer.Serialize(writer, exception.Message);
            writer.WriteEndObject();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            dynamic json = JObject.Load(reader);
            return new ProcessException((string)json.Message, (string)json.Type);
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(ProcessException);
        }
    }
}