using System.Text.Json;
namespace Mo2.Frontend;
internal static class Mo2SharedNexusLoginCheck
{
    public static async Task Live(string endpoint, string keyFile)
    {
        var registrations = new Mo2InstanceCatalog(endpoint).Read().Select(x => x.Registration).DistinctBy(x => x.Endpoint).ToArray();
        var result = await Mo2SharedNexusLogin.Apply((await File.ReadAllTextAsync(keyFile)).Trim(), registrations, _ => { }, default);
        var state = await Mo2SharedNexusLogin.ReadStatus(registrations);
        Console.WriteLine($"Shared login confirmed {result.Connected}/{result.Total}; native status confirmed {state.Connected}/{state.Total}; failures {result.Failures.Count}");
        foreach (var failure in result.Failures) Console.WriteLine(failure);
        if (result.Connected != registrations.Length || result.Failures.Count != 0 || state.Connected != state.Total || state.Total == 0)
            throw new Exception("Shared native login did not confirm every instance");
        Console.WriteLine($"PASS authorized credential applied to all {result.Total} native instances; matching runtime account confirmed");
    }
    public static async Task Run()
    {
        var registrations = new[] { new Mo2Registration("/fixture/one", "/one"), new Mo2Registration("/fixture/two", "/two") };
        static JsonElement Reply(int id = 1) => JsonSerializer.SerializeToElement(new { connected = true, account = new { userId = id, name = "Fixture" } });
        string? file = null;
        var calls = 0;
        Task<JsonElement> Apply(Mo2Registration registration, string hostPath) {
            file = OperatingSystem.IsWindows() ? hostPath : hostPath[2..];
            if (File.ReadAllText(file) != "fixture-key") throw new Exception("Credential handoff changed");
            if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(file) != (UnixFileMode.UserRead | UnixFileMode.UserWrite) || File.GetUnixFileMode(Path.GetDirectoryName(file)!) != (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute))) throw new Exception("Credential permissions too broad");
            calls++; return Task.FromResult(Reply());
        }
        var result = await Mo2SharedNexusLogin.ApplyCore("fixture-key",registrations,_ => { },default,_ => Task.CompletedTask,Apply);
        if (result.Connected != 2 || result.Failures.Count != 0 || calls != 2 || File.Exists(file) || Directory.Exists(Path.GetDirectoryName(file)!)) throw new Exception("Shared login or cleanup failed");
        calls = 0;
        result = await Mo2SharedNexusLogin.ApplyCore("fixture-key",registrations,_ => { },default,_ => Task.CompletedTask,(r,p) => {
            calls++;
            if (calls == 1) throw new Exception("private-fixture-body");
            return Task.FromResult(Reply());
        });
        if (calls != 2 || result.Connected != 1 || result.Failures.Count != 1 || result.Failures.Any(x => x.Contains("private-fixture-body"))) throw new Exception("Partial failure incorrectly reported");
        using var cancellation = new CancellationTokenSource(); calls = 0;
        result = await Mo2SharedNexusLogin.ApplyCore("fixture-key",registrations,_ => { },cancellation.Token,_ => Task.CompletedTask,async (r,p) => {
            calls++; cancellation.Cancel(); await Task.Delay(10);
            if (!File.Exists(OperatingSystem.IsWindows() ? p : p[2..])) throw new Exception("Credential removed before submitted operation completed");
            return Reply();
        });
        if (calls != 1 || result.Connected != 1 || result.Failures.Count != 1) throw new Exception("Cancellation did not preserve submitted operation and skip remaining instances");
        Console.WriteLine("PASS shared login, private credential handoff/cleanup, partial failure isolation, cancellation after submission");
    }
}
