using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Smakosz.E2E.Infrastructure;

namespace Smakosz.E2E;

[SetUpFixture]
public class E2ETestFixture
{
    private static Process? _apiProcess;
    private static Process? _clientProcess;
    private static bool _externalProcesses;

    [OneTimeSetUp]
    public async Task GlobalSetup()
    {
        Log("=== E2E Test Setup ===");

        Log("Creating and seeding E2E database...");
        try
        {
            await E2EDatabaseSeeder.CleanupAsync();
        }
        catch
        {
            // Database may not exist yet
        }

        await E2EDatabaseSeeder.SeedAsync();
        Log("Database seeded successfully.");

        _externalProcesses = Environment.GetEnvironmentVariable("SMAKOSZ_E2E_API_RUNNING") == "true";

        if (!_externalProcesses)
        {
            var repoRoot = FindRepoRoot();
            Log($"Repository root: {repoRoot}");

            EnsurePortFree(5000, "API");
            EnsurePortFree(5003, "Client");

            Log("Starting API...");
            _apiProcess = StartProcess(
                repoRoot,
                "src/Smakosz.API",
                extraArgs: null,
                new Dictionary<string, string>
                {
                    ["ConnectionStrings__DefaultConnection"] = TestConstants.ConnectionString,
                    ["ASPNETCORE_ENVIRONMENT"] = "E2E",
                    ["ASPNETCORE_URLS"] = TestConstants.ApiBaseUrl
                });

            Log("Starting Client...");
            _clientProcess = StartProcess(
                repoRoot,
                "src/Smakosz.Client",
                extraArgs: null,
                new Dictionary<string, string>
                {
                    ["ASPNETCORE_URLS"] = TestConstants.ClientBaseUrl
                });
        }
        else
        {
            Log("SMAKOSZ_E2E_API_RUNNING=true - skipping process startup.");
        }

        Log("Waiting for services to be ready...");
        await WaitForHealthy();

        if (!_externalProcesses)
            await VerifyApiDatabase();

        Log("=== Setup complete ===");
    }

    [OneTimeTearDown]
    public async Task GlobalTeardown()
    {
        Log("=== E2E Test Teardown ===");

        if (!_externalProcesses)
        {
            KillProcess(ref _apiProcess, "API");
            KillProcess(ref _clientProcess, "Client");
        }

        try
        {
            await E2EDatabaseSeeder.CleanupAsync();
            Log("Database dropped.");
        }
        catch (Exception ex)
        {
            Log($"Teardown warning: {ex.Message}");
        }
    }

    private static void Log(string message)
    {
        TestContext.Progress.WriteLine(message);
        Console.Error.WriteLine(message);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (Directory.GetFiles(dir, "*.slnx").Length > 0
                || Directory.GetFiles(dir, "*.sln").Length > 0)
                return dir;

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException(
            "Could not find repository root (no .sln/.slnx file found above test assembly).");
    }

    private static Process StartProcess(
        string repoRoot,
        string projectPath,
        string? extraArgs,
        Dictionary<string, string>? envVars = null)
    {
        var args = $"run --project {projectPath} --no-launch-profile";
        if (extraArgs is not null)
            args += $" {extraArgs}";

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = args,
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (envVars is not null)
        {
            foreach (var (key, value) in envVars)
                psi.EnvironmentVariables[key] = value;
        }

        var process = new Process { StartInfo = psi };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                Log($"[{projectPath}] {e.Data}");
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                Log($"[{projectPath}:err] {e.Data}");
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return process;
    }

    private async Task WaitForHealthy()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow.AddSeconds(60);
        var apiReady = false;
        var clientReady = false;

        while (DateTime.UtcNow < deadline)
        {
            // Fail fast if a managed process crashed during startup
            if (!_externalProcesses)
            {
                if (_apiProcess is { HasExited: true })
                    Assert.Fail($"API process exited prematurely with code {_apiProcess.ExitCode}. Check test output for logs.");
                if (_clientProcess is { HasExited: true })
                    Assert.Fail($"Client process exited prematurely with code {_clientProcess.ExitCode}. Check test output for logs.");
            }

            try
            {
                if (!apiReady)
                    apiReady = (await http.GetAsync($"{TestConstants.ApiBaseUrl}/api/home")).IsSuccessStatusCode;
            }
            catch
            {
            }

            try
            {
                if (!clientReady)
                    clientReady = (await http.GetAsync(TestConstants.ClientBaseUrl)).IsSuccessStatusCode;
            }
            catch
            {
            }

            if (apiReady && clientReady)
            {
                Log("API and Client are healthy.");
                return;
            }

            await Task.Delay(1000);
        }

        Assert.Fail(
            $"Services did not become healthy within 60s. " +
            $"API ready: {apiReady}, Client ready: {clientReady}. " +
            $"Check test output for process logs.");
    }

    private static void KillProcess(ref Process? process, string name)
    {
        if (process is null) return;

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                Log($"{name} process killed.");
            }
        }
        catch (Exception ex)
        {
            Log($"Warning: Could not kill {name} process: {ex.Message}");
        }
        finally
        {
            process.Dispose();
            process = null;
        }
    }

    private static void EnsurePortFree(int port, string serviceName)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            Log($"Port {port} ({serviceName}) is free.");
        }
        catch (SocketException)
        {
            Log($"WARNING: Port {port} ({serviceName}) is already in use! Attempting to free it...");
            try
            {
                // Use PowerShell to find and kill the process on this port
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -Command \"Get-NetTCPConnection -LocalPort {port} -State Listen -ErrorAction SilentlyContinue | ForEach-Object {{ Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                var proc = Process.Start(psi);
                proc?.WaitForExit(10_000);
                Thread.Sleep(2000);
                Log($"Freed port {port}.");
            }
            catch (Exception ex)
            {
                Log($"Warning: Could not free port {port}: {ex.Message}. Tests may connect to a stale process.");
            }
        }
    }

    private async Task VerifyApiDatabase()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        try
        {
            // Call search filters - if E2E DB is used, we should see our seeded cuisine types
            var response = await http.GetAsync($"{TestConstants.ApiBaseUrl}/api/search/filters");
            var body = await response.Content.ReadAsStringAsync();

            if (body.Contains("Wloska") && body.Contains("Turecka"))
            {
                Log("API database verification: OK (E2E cuisines found).");
            }
            else
            {
                Log($"WARNING: API may not be using E2E database! Filters response: {body[..Math.Min(300, body.Length)]}");
            }

            // Also verify restaurants are visible via search
            var searchResponse = await http.GetAsync($"{TestConstants.ApiBaseUrl}/api/search?type=restaurants");
            var searchBody = await searchResponse.Content.ReadAsStringAsync();
            Log($"API search verification: {searchBody[..Math.Min(500, searchBody.Length)]}");
        }
        catch (Exception ex)
        {
            Log($"API database verification failed: {ex.Message}");
        }
    }
}
