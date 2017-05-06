using System;
using Newtonsoft.Json;

namespace Gaev.DurableTask.Storage
{
    public class VoidSerializer : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, null);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return Void.Nothing;
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Void);
        }
    }
}