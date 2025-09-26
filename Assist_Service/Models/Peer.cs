using System;
using System.Net;
using Newtonsoft.Json;

namespace Assist_Service.Models
{
    /// <summary>
    /// Represents a discovered peer node
    /// </summary>
    public class Peer
    {
        public string NodeName { get; set; }

        [JsonConverter(typeof(IPEndPointConverter))]
        public IPEndPoint EndPoint { get; set; }

        public string MacAddress { get; set; }
        public string IPAddress { get; set; }
        public string Status { get; set; }
        public DateTime LastSeen { get; set; }
        public bool LeftGracefully { get; set; } = false;
        public int MissedHeartbeats { get; set; } = 0;
    }

    /// <summary>
    /// Custom converter for IPEndPoint (serialize as "IP:Port")
    /// </summary>
    public class IPEndPointConverter : JsonConverter<IPEndPoint>
    {
        public override void WriteJson(JsonWriter writer, IPEndPoint value, JsonSerializer serializer)
        {
            writer.WriteValue(value?.ToString());
        }

        public override IPEndPoint ReadJson(JsonReader reader, Type objectType, IPEndPoint existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var s = reader.Value as string;
            if (string.IsNullOrEmpty(s)) return null;

            var parts = s.Split(':');
            if (parts.Length != 2) return null;

            if (IPAddress.TryParse(parts[0], out var ip) && int.TryParse(parts[1], out var port))
            {
                return new IPEndPoint(ip, port);
            }

            return null;
        }
    }
}
