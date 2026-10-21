using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Tybim.Mcp.Contracts;

namespace Tybim.Revit.Addin
{
    internal sealed class BridgeListener : IDisposable
    {
        private readonly TcpListener listener;
        private readonly Dispatcher dispatcher;
        private readonly BridgeSettings settings;
        private readonly CancellationTokenSource stop = new CancellationTokenSource();
        private readonly ConcurrentDictionary<TcpClient, byte> clients = new ConcurrentDictionary<TcpClient, byte>();
        private readonly JsonSerializerSettings json = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver(), MaxDepth = 32 };
        public BridgeListener(BridgeSettings settings, Dispatcher dispatcher)
        { this.settings = settings; this.dispatcher = dispatcher; listener = new TcpListener(IPAddress.Loopback, settings.Port); }
        public void Start() { listener.Start(8); _ = Accept(); }
        private async Task Accept()
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    if (clients.Count >= 8) { client.Dispose(); continue; }
                    clients.TryAdd(client, 0);
                    _ = Serve(client);
                }
            }
            catch (Exception ex) { if (!stop.IsCancellationRequested) System.Diagnostics.Trace.WriteLine("TYBIM MCP accept: " + ex.Message); }
        }
        private async Task Serve(TcpClient client)
        {
            try
            {
                // One bounded UTF-8 JSON line per connection; a fresh connection per command.
                var stream = client.GetStream();
                var bytes = new MemoryStream();
                var buffer = new byte[4096];
                using (var readStop = CancellationTokenSource.CreateLinkedTokenSource(stop.Token))
                {
                    readStop.CancelAfter(TimeSpan.FromSeconds(5));
                    using (readStop.Token.Register(() => client.Close()))
                    {
                        bool complete = false;
                        while (!complete)
                        {
                            int count = await stream.ReadAsync(buffer, 0, buffer.Length, readStop.Token).ConfigureAwait(false);
                            if (count == 0) return;
                            for (int i = 0; i < count; i++)
                            {
                                if (buffer[i] == 10) { complete = true; break; }
                                bytes.WriteByte(buffer[i]);
                                if (bytes.Length > 65536) throw new InvalidDataException("FRAME_TOO_LARGE");
                            }
                        }
                    }
                }
                BridgeResponse response;
                var request = JsonConvert.DeserializeObject<BridgeRequest>(new UTF8Encoding(false, true).GetString(bytes.ToArray()), json);
                if (request == null || request.Version != 1 || string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Method) || request.Parameters == null)
                    response = BridgeResponse.Fail(request?.Id, "INVALID_REQUEST");
                else if (!string.Equals(request.Token, settings.Token, StringComparison.Ordinal))
                    response = BridgeResponse.Fail(request.Id, "UNAUTHORIZED");
                else if (request.ExpiresAt <= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() || request.ExpiresAt > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 180000)
                    response = BridgeResponse.Fail(request.Id, "INVALID_DEADLINE");
                else
                {
                    var pending = dispatcher.Enqueue(request);
                    var remaining = Math.Max(1, request.ExpiresAt - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                    response = await Task.WhenAny(pending, Task.Delay((int)remaining, stop.Token)).ConfigureAwait(false) == pending
                        ? await pending.ConfigureAwait(false) : BridgeResponse.Fail(request.Id, "REQUEST_EXPIRED");
                }
                var output = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response, json) + "\n");
                using (var writeStop = CancellationTokenSource.CreateLinkedTokenSource(stop.Token))
                {
                    writeStop.CancelAfter(TimeSpan.FromSeconds(5));
                    using (writeStop.Token.Register(() => client.Close()))
                        await stream.WriteAsync(output, 0, output.Length, writeStop.Token).ConfigureAwait(false);
                }
            }
            catch (Exception ex) { if (!stop.IsCancellationRequested) System.Diagnostics.Trace.WriteLine("TYBIM MCP connection: " + ex.Message); }
            finally { clients.TryRemove(client, out _); client.Dispose(); }
        }
        public void Dispose()
        {
            stop.Cancel(); listener.Stop();
            foreach (var client in clients.Keys) client.Close();
        }
    }
}
