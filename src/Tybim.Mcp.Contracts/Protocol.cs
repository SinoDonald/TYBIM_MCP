using System;
using Newtonsoft.Json.Linq;

namespace Tybim.Mcp.Contracts
{
    // Internal bridge protocol; MCP itself terminates at the Node server.
    public sealed class BridgeRequest
    {
        public int Version { get; set; }
        public string Id { get; set; }
        public string Token { get; set; }
        public string Method { get; set; }
        public JObject Parameters { get; set; } = new JObject();
        public long ExpiresAt { get; set; }
    }

    public sealed class BridgeResponse
    {
        public int Version { get; set; } = 1;
        public string Id { get; set; }
        public bool Success { get; set; }
        public object Data { get; set; }
        public string Error { get; set; }

        public static BridgeResponse Fail(string id, string error) =>
            new BridgeResponse { Id = id, Error = error };
    }

    public sealed class BridgeSettings
    {
        public int Port { get; set; } = 17825;
        public string Token { get; set; }

        public void Validate()
        {
            if (Port < 1024 || Port > 65535 || string.IsNullOrWhiteSpace(Token) || Token.Length < 32)
                throw new InvalidOperationException("Bridge configuration needs port 1024..65535 and a token of at least 32 characters.");
        }
    }
}
