using System.Reflection;
using System.Text.Json;
namespace Mo2.Frontend;

internal static class Mo2BridgeErrorCheck
{
    public static async Task Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mo2-bridge-errors-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory,"requests"));
        Directory.CreateDirectory(Path.Combine(directory,"responses"));
        await File.WriteAllTextAsync(Path.Combine(directory,"endpoint.json"), JsonSerializer.Serialize(new { protocol = 1, session = "test-session" }));
        try {
            async Task<Exception> Reject(string action, string? wrongId = null, string? wrongSession = null)
            {
                var requestTask = new Mo2BridgeClient(directory).SendAsync(action, timeout:TimeSpan.FromSeconds(3));
                var deadline = DateTime.UtcNow.AddSeconds(3);
                string? request = null;
                while ((request = Directory.GetFiles(Path.Combine(directory,"requests"),"*.json").SingleOrDefault()) is null) {
                    if (DateTime.UtcNow >= deadline) throw new Exception("Test request was not submitted");
                    await Task.Delay(10);
                }
                using var submitted = JsonDocument.Parse(await File.ReadAllTextAsync(request));
                var id = Path.GetFileNameWithoutExtension(request);
                var response = Path.Combine(directory,"responses",id+".json");
                await File.WriteAllTextAsync(response+".tmp",JsonSerializer.Serialize(new {
                    id = wrongId ?? id, session = wrongSession ?? submitted.RootElement.GetProperty("session").GetString(), ok = false, error = "Native action unavailable" }));
                File.Delete(request); File.Move(response+".tmp",response);
                try { await requestTask; throw new Exception("Rejected action was accepted"); }
                catch (InvalidOperationException e) { return e; }
                catch (InvalidDataException e) { return e; }
            }
            var rejection = await Reject("saveAction");
            if (rejection is not Mo2BridgeCommandException { Action: "saveAction" }) throw new Exception("Native rejection lost its action identity");
            var snapshot = await Reject("snapshot");
            var wrongId = await Reject("saveAction",wrongId:"other-request");
            var wrongSession = await Reject("saveAction",wrongSession:"other-session");
            if (wrongId is not InvalidDataException || wrongSession is not InvalidDataException) throw new Exception("Invalid response identity was accepted as native rejection");
            var profile = new Mo2LiveProfile("");
            var state = typeof(Mo2LiveProfile).GetField("_lastSnapshot", BindingFlags.Instance|BindingFlags.NonPublic)!;
            var report = typeof(Mo2LiveProfile).GetMethod("Report", BindingFlags.Instance|BindingFlags.NonPublic)!;
            foreach (var (error, connected) in new[] { (rejection,true), (snapshot,false), (wrongId,false), (wrongSession,false), ((Exception)new TimeoutException("unknown outcome"),false), (new IOException("host stopped"),false) }) {
                state.SetValue(profile,"confirmed snapshot");
                report.Invoke(profile,[error]);
                if (profile.IsConnected != connected || profile.Status != error.Message) throw new Exception("Wrong connection state or missing feedback after " + error.GetType().Name);
            }
            state.SetValue(profile,null);
            report.Invoke(profile,[rejection]);
            if (profile.IsConnected) throw new Exception("A native rejection fabricated a connected snapshot");
            Console.WriteLine("PASS: native rejection preserves connection; failed snapshot, identity mismatch, timeout and host I/O failure invalidate it; feedback retained");
        } finally { Directory.Delete(directory,true); }
    }
}
