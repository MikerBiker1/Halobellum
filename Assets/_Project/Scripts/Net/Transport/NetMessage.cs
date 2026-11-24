// NetMessage.cs
// One message between the two games: a JSON object whose string field "t" names its type ("Hello", "Ping", "Ready"...).
// The transport's own types are listed in NetMessageTypes; the layers above add their own (MatchStart, Ready, StateHash...).
// Plain C# 9 without the engine.
// Depends on: NetJson.

using System;

namespace Halobellum.Net
{
    /// <summary>The message types the transport itself sends and reads.</summary>
    public static class NetMessageTypes
    {
        /// <summary>Heartbeat, sent every second by both sides; handled inside LanConnection, never handed to the game.</summary>
        public const string Ping = "Ping";
        /// <summary>The answer to a Ping (gives the round trip time); handled inside LanConnection.</summary>
        public const string Pong = "Pong";
        /// <summary>Guest to host, first message: who I am (protocol, version, code and content fingerprints, name).</summary>
        public const string Hello = "Hello";
        /// <summary>Host to guest: the match can go ahead.</summary>
        public const string Welcome = "Welcome";
        /// <summary>Host to guest: refused, with a reason; the host closes the connection after it.</summary>
        public const string Reject = "Reject";
    }

    /// <summary>A JSON object with a string type in "t".</summary>
    public sealed class NetMessage : JsonObject
    {
        public const string TypeKey = "t";

        public NetMessage(string type)
        {
            if (string.IsNullOrEmpty(type)) throw new ArgumentException("A message needs a type.");
            Set(TypeKey, type);
        }

        private NetMessage() { }

        public string Type => GetString(TypeKey, "");

        /// <summary>Reads a message from its JSON text. Throws FormatException when it is not an object with a string "t".</summary>
        public static NetMessage Parse(string json)
        {
            JsonObject parsed = NetJson.ParseObject(json);
            if (!(parsed.Get(TypeKey) is string type) || type.Length == 0) throw new FormatException("A message needs a string field \"t\".");
            var message = new NetMessage();
            foreach (string key in parsed.Keys) message.SetRaw(key, parsed.Get(key));
            return message;
        }
    }
}
