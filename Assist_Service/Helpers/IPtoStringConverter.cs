using System;
using System.Net;
using Newtonsoft.Json;

namespace Assist_Service.Helpers
{
    public class IPtoStringConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(IPEndPoint);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            if (reader.TokenType == JsonToken.String)
            {
                string value = (string)reader.Value;

                // Handle empty or null strings
                if (string.IsNullOrEmpty(value))
                    return null;

                try
                {
                    // Parse IP:Port format
                    var parts = value.Split(':');
                    if (parts.Length == 2)
                    {
                        IPAddress address = IPAddress.Parse(parts[0]);
                        int port = int.Parse(parts[1]);
                        return new IPEndPoint(address, port);
                    }
                }
                catch
                {
                    // Return null if parsing fails
                    return null;
                }
            }

            return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            IPEndPoint ep = (IPEndPoint)value;
            writer.WriteValue($"{ep.Address}:{ep.Port}");
        }
    }
}
