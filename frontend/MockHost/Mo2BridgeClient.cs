using System.Text.Json;

namespace Mo2.Frontend;

internal sealed class Mo2BridgeClient(string directory)
{
    public async Task<JsonElement> SendAsync(string action, Dictionary<string, object?>? arguments = null, CancellationToken cancellationToken = default)
    {
        using var endpoint = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "endpoint.json"), cancellationToken));
        var session = endpoint.RootElement.GetProperty("session").GetString()!;
        if (endpoint.RootElement.GetProperty("protocol").GetInt32() != 1) throw new NotSupportedException("Unsupported MO2 bridge protocol");
        var id = Guid.NewGuid().ToString();
        var request = new Dictionary<string, object?>(arguments ?? []) { ["protocol"] = 1, ["session"] = session, ["action"] = action };
        var path = Path.Combine(directory, "requests", id + ".json");
        var responsePath = Path.Combine(directory, "responses", id + ".json");
        await File.WriteAllTextAsync(path + ".tmp", JsonSerializer.Serialize(request), cancellationToken);
        File.Move(path + ".tmp", path);
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!File.Exists(responsePath)) {
            cancellationToken.ThrowIfCancellationRequested();
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException($"MO2 bridge request {id} timed out. Its outcome is unknown; refresh before retrying a change.");
            await Task.Delay(50, cancellationToken);
        }
        using var response = JsonDocument.Parse(await File.ReadAllTextAsync(responsePath, cancellationToken));
        var root = response.RootElement;
        if (root.GetProperty("id").GetString() != id || root.GetProperty("session").GetString() != session)
            throw new InvalidDataException("MO2 bridge response identity mismatch");
        if (!root.GetProperty("ok").GetBoolean()) throw new InvalidOperationException(root.GetProperty("error").GetString());
        var result = root.GetProperty("result").Clone();
        // Leave the response receipt until the request has been removed by MO2,
        // so a delayed poll cannot repeat a mutation after response consumption.
        if (!File.Exists(path)) File.Delete(responsePath);
        return result;
    }
}
