using System.Net.WebSockets;
using System.Text;
namespace Mo2.Frontend;
internal static class Mo2NexusSsoCheck
{
    public static async Task CheckConnection()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ready = false;
        try {
            await Mo2NexusSso.Authorize(_ => { ready = true; cancellation.Cancel(); }, cancellation.Token);
            throw new Exception("Unexpected authorization without browser interaction");
        } catch (OperationCanceledException) when (ready) {
            Console.WriteLine("PASS live Nexus SSO initialization and cancellation before browser authorization");
        }
    }
    public static async Task Run()
    {
        const string ack = "{\"success\":true,\"data\":{\"connection_token\":\"test-token\"}}";
        const string reply = "{\"success\":true,\"data\":{\"api_key\":\"test-key\"}}";
        var browser = 0;
        using var socket = new FakeSocket([(ack[..20],false),(ack[20..],true),(reply,true)]);
        var id = Guid.NewGuid();
        var key = await Mo2NexusSso.Exchange(socket,id,uri => {
            if (uri.Scheme != "https" || uri.Host != "www.nexusmods.com" || !uri.Query.Contains("application=modorganizer2") || !uri.Query.Contains(id.ToString())) throw new Exception("Incorrect browser authorization URL");
            browser++;
        },default);
        if (key != "test-key" || browser != 1 || !socket.Sent.Contains("\"protocol\":2")) throw new Exception("SSO handshake failed");
        foreach (var invalid in new[] { "{}", "not-json", "{\"success\":false,\"error\":\"private-test-value\"}", "{\"success\":true,\"data\":{\"api_key\":\"private-test-value\\n\"}}" }) {
            try { Mo2NexusSso.ParseMessage(Encoding.UTF8.GetBytes(invalid),true); throw new Exception("Invalid response accepted"); }
            catch (InvalidOperationException error) { if (error.ToString().Contains("private-test-value")) throw new Exception("Response body exposed in an exception"); }
        }
        try { Mo2NexusSso.ParseMessage(Encoding.UTF8.GetBytes(reply),false); throw new Exception("Key accepted before initialization"); } catch (InvalidOperationException) { }
        using var oversized = new FakeSocket([(new string('x',Mo2NexusSso.MaximumMessageBytes),false)]);
        try { await Mo2NexusSso.Exchange(oversized,id,_ => throw new Exception("Unexpected browser launch"),default); throw new Exception("Oversized response accepted"); } catch (InvalidOperationException) { }
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { await Mo2NexusSso.Exchange(new FakeSocket([]),id,_ => throw new Exception("Unexpected browser launch"),cancelled.Token); throw new Exception("Cancellation ignored"); } catch (OperationCanceledException) { }
        Console.WriteLine("PASS SSO protocol 2, fragmented messages, browser URL, secret-safe errors, size limit, ordering and cancellation");
    }
    private sealed class FakeSocket(IEnumerable<(string Text,bool End)> messages) : WebSocket
    {
        private readonly Queue<(string Text,bool End)> _messages = new(messages);
        public string Sent { get; private set; } = "";
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => WebSocketState.Open;
        public override string? SubProtocol => null;
        public override void Abort() { }
        public override void Dispose() { }
        public override Task CloseAsync(WebSocketCloseStatus status,string? description,CancellationToken token) => Task.CompletedTask;
        public override Task CloseOutputAsync(WebSocketCloseStatus status,string? description,CancellationToken token) => Task.CompletedTask;
        public override Task SendAsync(ArraySegment<byte> buffer,WebSocketMessageType type,bool end,CancellationToken token) {
            token.ThrowIfCancellationRequested(); Sent = Encoding.UTF8.GetString(buffer); return Task.CompletedTask;
        }
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer,CancellationToken token) {
            token.ThrowIfCancellationRequested(); var message = _messages.Dequeue(); var bytes = Encoding.UTF8.GetBytes(message.Text);
            bytes.CopyTo(buffer.AsSpan()); return Task.FromResult(new WebSocketReceiveResult(bytes.Length,WebSocketMessageType.Text,message.End));
        }
    }
}
