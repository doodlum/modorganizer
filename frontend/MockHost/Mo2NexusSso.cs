using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Mo2.Frontend;

// Uses the protocol of the deployed MO2 SSO client, rather than treating NMA
// OAuth tokens as API keys. Message bodies and returned credentials stay private.
internal static class Mo2NexusSso
{
    internal const int MaximumMessageBytes = 16384;
    public static async Task<string> Authorize(Action<Uri> openBrowser, CancellationToken cancellationToken)
    {
        using var socket = new ClientWebSocket();
        using var connect = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connect.CancelAfter(TimeSpan.FromSeconds(10));
        try {
            await socket.ConnectAsync(new Uri("wss://sso.nexusmods.com"), connect.Token);
            return await Exchange(socket, Guid.NewGuid(), openBrowser, cancellationToken);
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            throw new InvalidOperationException("Nexus login timed out. Please try again.");
        } catch (WebSocketException) {
            throw new InvalidOperationException("The Nexus login connection closed. Please try again.");
        } finally { socket.Abort(); }
    }

    internal static async Task<string> Exchange(WebSocket socket, Guid id, Action<Uri> openBrowser, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.SerializeToUtf8Bytes(new { id = id.ToString(), protocol = 2 });
        await socket.SendAsync(request.AsMemory(), WebSocketMessageType.Text, true, cancellationToken);
        var ready = false;
        while (true) {
            using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            wait.CancelAfter(ready ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(10));
            var buffer = new byte[MaximumMessageBytes];
            var length = 0;
            while (true) {
                var response = await socket.ReceiveAsync(buffer.AsMemory(length), wait.Token);
                if (response.MessageType != WebSocketMessageType.Text)
                    throw new InvalidOperationException("Nexus ended the login request without authorization.");
                length += response.Count;
                if (response.EndOfMessage) break;
                if (length == buffer.Length) throw new InvalidOperationException("Nexus sent an invalid login response.");
            }
            var key = ParseMessage(buffer.AsMemory(0, length), ready);
            Array.Clear(buffer);
            if (key is not null) return key;
            if (ready) throw new InvalidOperationException("Nexus sent an unexpected login response.");
            ready = true;
            openBrowser(new Uri($"https://www.nexusmods.com/sso?id={id:D}&application=modorganizer2"));
        }
    }

    internal static string? ParseMessage(ReadOnlyMemory<byte> message, bool ready)
    {
        try {
            using var json = JsonDocument.Parse(message);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True ||
                !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object) throw new FormatException();
            if (data.TryGetProperty("api_key", out var api)) {
                if (!ready || api.ValueKind != JsonValueKind.String) throw new FormatException();
                var key = api.GetString()!;
                if (key.Length is 0 or > 4096 || key.Any(c => c < 33 || c > 126)) throw new FormatException();
                return key;
            }
            if (!ready && data.TryGetProperty("connection_token", out var token) && token.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(token.GetString())) return null;
            throw new FormatException();
        } catch (Exception error) when (error is JsonException or FormatException or InvalidOperationException) {
            throw new InvalidOperationException("Nexus rejected the login request or sent an invalid response.");
        }
    }
}
