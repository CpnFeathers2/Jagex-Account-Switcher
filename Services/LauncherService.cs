using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JagexAccountSwitcher.Helpers;
using JagexAccountSwitcher.Model;
using JagexAccountSwitcher.ViewModels;

namespace JagexAccountSwitcher.Services
{
    /// <summary>
    /// Manages Java client launching, break scheduling, and state persistence
    /// </summary>
    public class LauncherService
    {
        private readonly object _lock = new();
        private static readonly object _launchLock = new(); 
        private CancellationTokenSource? _globalLaunchCts;
        private readonly Dictionary<string, CancellationTokenSource> _breakCancellations = new();
        public Action<string, bool, DateTime?>? OnBreakStatusChanged;
        
        private readonly Dictionary<string, Process> _processMap = new();
        private readonly Dictionary<string, string> _accountWorkingDirs = new();
        
        private readonly UserSettings _settings;
        private MassAccountHandlerViewModel _massAccountHandler;
        
        private HttpListener _listener;
        private bool _isServerRunning;
        
        // Authentication key for HTTP API
        // private static readonly string API_KEY = Guid.NewGuid().ToString();
        private static readonly string API_KEY = "MY_SAFE_KEY_123";

        public IReadOnlyDictionary<string, Process> AccountProcesses => _processMap;

        public LauncherService(UserSettings settings)
        {
            _settings = settings;
        }

        public void SetMassAccountHandler(MassAccountHandlerViewModel handler) => _massAccountHandler = handler;

        public void Start()
        {
            Console.WriteLine("[LauncherService] Starting HTTP Listener...");
            Console.WriteLine($"[LauncherService] API Key: {API_KEY}");
            StartHttpServer(8080);
            
            // Resume any pending breaks from previous session
            ResumePendingBreaks();
        }

        private void StartHttpServer(int port)
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://127.0.0.1:{port}/break/");
                _listener.Prefixes.Add($"http://127.0.0.1:{port}/health/");
                _listener.Start();
                _isServerRunning = true;

                Task.Run(async () =>
                {
                    while (_isServerRunning)
                    {
                        try
                        {
                            var context = await _listener.GetContextAsync();
                            _ = Task.Run(() => HandleIncomingRequest(context));
                        }
                        catch (Exception ex)
                        {
                            if (_isServerRunning) Console.WriteLine($"[HTTP] Error: {ex.Message}");
                        }
                    }
                });
                
                Console.WriteLine($"[HTTP] Server started on 127.0.0.1:{port}");
            }
            catch (Exception ex) 
            { 
                Console.WriteLine($"[Critical] Server Error: {ex.Message}"); 
            }
        }

        private async Task HandleIncomingRequest(HttpListenerContext context)
        {
            try
            {
                // Health check endpoint
                if (context.Request.Url.AbsolutePath == "/health/")
                {
                    context.Response.StatusCode = 200;
                    byte[] msg = Encoding.UTF8.GetBytes("{\"status\":\"OK\"}");
                    context.Response.ContentType = "application/json";
                    await context.Response.OutputStream.WriteAsync(msg);
                    context.Response.Close();
                    return;
                }

                // Break endpoint - requires authentication
                if (context.Request.HttpMethod == "POST" && context.Request.Url.AbsolutePath == "/break/")
                {
                    // Validate API key
                    string authHeader = context.Request.Headers["X-API-Key"];
                    if (authHeader != API_KEY)
                    {
                        Console.WriteLine("[HTTP] Unauthorized request - invalid API key");
                        context.Response.StatusCode = 401;
                        byte[] errorMsg = Encoding.UTF8.GetBytes("{\"error\":\"Unauthorized\"}");
                        await context.Response.OutputStream.WriteAsync(errorMsg);
                        context.Response.Close();
                        return;
                    }

                    using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                    string json = await reader.ReadToEndAsync();
                    
                    Console.WriteLine($"[HTTP] Received break request: {json}");
                    
                    using JsonDocument doc = JsonDocument.Parse(json);
                    JsonElement root = doc.RootElement;

                    // Validate required fields
                    if (!root.TryGetProperty("accountId", out var accIdProp) || 
                        !root.TryGetProperty("duration", out var durProp) ||
                        !root.TryGetProperty("nextActivity", out var actProp))
                    {
                        Console.WriteLine("[HTTP] Invalid request - missing required fields");
                        context.Response.StatusCode = 400;
                        byte[] errorMsg = Encoding.UTF8.GetBytes("{\"error\":\"Missing required fields\"}");
                        await context.Response.OutputStream.WriteAsync(errorMsg);
                        context.Response.Close();
                        return;
                    }

                    string accountId = accIdProp.GetString() ?? "";
                    int duration = durProp.GetInt32();
                    string nextActivity = actProp.GetString() ?? "none";

                    // Validate duration range (1 minute to 24 hours)
                    if (duration < 60 || duration > 86400)
                    {
                        Console.WriteLine($"[HTTP] Invalid duration: {duration}");
                        context.Response.StatusCode = 400;
                        byte[] errorMsg = Encoding.UTF8.GetBytes("{\"error\":\"Duration out of range\"}");
                        await context.Response.OutputStream.WriteAsync(errorMsg);
                        context.Response.Close();
                        return;
                    }

                    HandleBreak(accountId, duration, nextActivity);
                    
                    context.Response.StatusCode = 200;
                    byte[] response = Encoding.UTF8.GetBytes("{\"status\":\"accepted\"}");
                    await context.Response.OutputStream.WriteAsync(response);
                }
                else
                {
                    context.Response.StatusCode = 404;
                }
                
                context.Response.Close();
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[HTTP] JSON parsing error: {ex.Message}");
                context.Response.StatusCode = 400;
                byte[] errorMsg = Encoding.UTF8.GetBytes("{\"error\":\"Invalid JSON\"}");
                await context.Response.OutputStream.WriteAsync(errorMsg);
                context.Response.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HTTP] Request handling error: {ex.Message}");
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
        }

        public Process? StartClient(string accountId, string activityArgs)
{
    // CRITICAL: Lock to ensure credentials aren't overwritten while another client is starting
    lock (_launchLock)
    {
        Console.WriteLine($"[LauncherService] Preparing to launch {accountId}...");
        
        // 1. Check Memory
        var account = _settings.Accounts.FirstOrDefault(a => 
            a.AccountName == accountId || a.Id == accountId);

        // 2. Check Disk (Reload logic)
        if (account == null)
        {
            Console.WriteLine($"[LauncherService] Account {accountId} not found in memory. Reloading settings...");
            _settings.LoadFromFile();

            // MANUAL FIX: Load from accounts.json because settings.json usually has empty accounts
            try 
            {
                string accountsPath = Path.Combine(_settings.ConfigurationsPath, "accounts.json");
                if (File.Exists(accountsPath))
                {
                    string json = File.ReadAllText(accountsPath);
                    var loadedAccounts = JsonSerializer.Deserialize<List<RunescapeAccount>>(json);
                    if (loadedAccounts != null)
                    {
                        _settings.Accounts.Clear();
                        foreach (var acc in loadedAccounts) _settings.Accounts.Add(acc);
                        Console.WriteLine($"[LauncherService] Reloaded {loadedAccounts.Count} accounts from accounts.json");
                    }
                }
                else
                {
                    Console.WriteLine($"[LauncherService] Warning: accounts.json not found at {accountsPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LauncherService] Failed to load accounts.json: {ex.Message}");
            }

            // Try finding it again
            account = _settings.Accounts.FirstOrDefault(a => 
                a.AccountName == accountId || a.Id == accountId);
        }

        if (account == null)
        {
            Console.WriteLine($"[LauncherService] ERROR: Account {accountId} not found in settings or disk.");
            return null;
        }

        string jarPath = _settings.MicroBotJarPath;
        if (string.IsNullOrEmpty(jarPath) || !File.Exists(jarPath))
        {
            Console.WriteLine($"[LauncherService] ERROR: Jar not found at {jarPath}");
            return null;
        }

        if (string.IsNullOrEmpty(account.FilePath) || !File.Exists(account.FilePath))
        {
            Console.WriteLine($"[LauncherService] ERROR: Credentials file not found: {account.FilePath}");
            return null;
        }

        // 3. Swap Credentials safely
        string runelitePath = _settings.RunelitePath;
        if (string.IsNullOrEmpty(runelitePath))
        {
            runelitePath = RuneliteHelper.GetRunelitePath();
        }

        string targetCredFile = Path.Combine(runelitePath, "credentials.properties");
        
        try
        {
            File.Copy(account.FilePath, targetCredFile, overwrite: true);
            Console.WriteLine($"[LauncherService] Copied credentials for {accountId} to {targetCredFile}");
            Thread.Sleep(100); // Brief pause to ensure file system flush
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LauncherService] ERROR copying credentials: {ex.Message}");
            return null;
        }

        // 4. Create per-account log directory
        string logDir = Path.Combine(Directory.GetCurrentDirectory(), "logs", accountId);
        Directory.CreateDirectory(logDir);
        string logFile = Path.Combine(logDir, $"{DateTime.Now:yyyyMMdd_HHmmss}.txt");

        // 5. Setup Process with CORRECTED ARGUMENT HANDLING
        var psi = new ProcessStartInfo
        {
            FileName = "java",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(jarPath)
        };

        // ============================================================
        // CRITICAL FIX: Pass ALL custom variables as JVM System Properties
        // These MUST come BEFORE -jar to avoid RuneLite parser errors
        // ============================================================
        
        // Always pass account-id and api-key as JVM System Properties
        psi.ArgumentList.Add($"-Daccount-id={accountId}");
        psi.ArgumentList.Add($"-Dapi-key={API_KEY}");
        
        Console.WriteLine($"[LauncherService] Added JVM property: -Daccount-id={accountId}");
        Console.WriteLine($"[LauncherService] Added JVM property: -Dapi-key={API_KEY}");

        // Parse and pass nextActivity as JVM System Property
        if (!string.IsNullOrEmpty(activityArgs))
        {
            // Handle different formats:
            // - "--script=monkkiller" → extract "monkkiller"
            // - "--script monkkiller" → extract "monkkiller"  
            // - "monkkiller" → use as-is
            string scriptName;
            
            if (activityArgs.Contains("="))
            {
                // Format: "--script=monkkiller"
                scriptName = activityArgs.Split('=', 2)[1].Trim();
            }
            else if (activityArgs.StartsWith("--"))
            {
                // Format: "--script monkkiller" or just "--script"
                scriptName = activityArgs.Replace("--script", "").Trim();
            }
            else
            {
                // Format: "monkkiller" (already clean)
                scriptName = activityArgs.Trim();
            }
            
            if (!string.IsNullOrEmpty(scriptName))
            {
                psi.ArgumentList.Add($"-DnextActivity={scriptName}");
                Console.WriteLine($"[LauncherService] Added JVM property: -DnextActivity={scriptName}");
            }
            else
            {
                Console.WriteLine($"[LauncherService] Warning: activityArgs provided but script name is empty after parsing");
            }
        }

        // Now add -jar and the jar path
        psi.ArgumentList.Add("-jar");
        psi.ArgumentList.Add(jarPath);

        // IMPORTANT: Do NOT add any command-line arguments after the jar path
        // RuneLite's joptsimple parser will reject unrecognized options like "--script"
        // All custom variables are now accessible in Java via System.getProperty()

        Console.WriteLine($"[LauncherService] Final command line: {string.Join(" ", psi.ArgumentList)}");
        Console.WriteLine($"[LauncherService] Launching process for {accountId}...");

        var p = new Process { StartInfo = psi };
        
        // Redirect to console AND log file
        p.OutputDataReceived += (s, e) => 
        { 
            if (e.Data != null)
            {
                Console.WriteLine($"[{accountId}] {e.Data}");
                try { File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss}] {e.Data}\n"); } catch { }
            }
        };
        
        p.ErrorDataReceived += (s, e) => 
        { 
            if (e.Data != null)
            {
                Console.WriteLine($"[{accountId}-Err] {e.Data}");
                try { File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss}] ERROR: {e.Data}\n"); } catch { }
            }
        };

        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        lock (_lock)
        {
            if (p != null) _processMap[accountId] = p;
        }

        // Monitor process exit asynchronously
        _ = MonitorProcessExit(accountId, p);

        // 6. SAFETY WAIT
        // Hold the lock for 5 seconds to ensure this client reads the credentials file
        // before we allow the next client to overwrite it.
        Console.WriteLine($"[LauncherService] Waiting 5s for client initialization...");
        Thread.Sleep(5000);

        return p;
    }
}

        private async Task MonitorProcessExit(string accountId, Process process)
        {
            try
            {
                await process.WaitForExitAsync();
                
                lock (_lock)
                {
                    if (_processMap.ContainsKey(accountId) && _processMap[accountId].Id == process.Id)
                    {
                        _processMap.Remove(accountId);
                        Console.WriteLine($"[LauncherService] Process {accountId} exited (Code: {process.ExitCode})");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LauncherService] Error monitoring process exit: {ex.Message}");
            }
        }

        public void StartAll(List<(string id, string args)> accounts)
        {
            _globalLaunchCts?.Cancel();
            _globalLaunchCts = new CancellationTokenSource();
            var token = _globalLaunchCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    foreach (var acc in accounts)
                    {
                        if (token.IsCancellationRequested) return;

                        StartClient(acc.id, acc.args);

                        await Task.Delay(5000, token); 
                    }
                }
                catch (TaskCanceledException) { /* Clean exit */ }
            }, token);
        }

        private void CancelBreak(string accountId)
        {
            lock (_lock)
            {
                if (_breakCancellations.TryGetValue(accountId, out var cts))
                {
                    cts.Cancel();
                    cts.Dispose();
                    _breakCancellations.Remove(accountId);
                }
            }
        }

        public void KillClient(string accountId)
        {
            // 1. Cancel any pending breaks/restarts
            CancelBreak(accountId);

            // 2. Notify UI that break is cancelled
            OnBreakStatusChanged?.Invoke(accountId, false, null);

            // 3. Kill the process
            lock (_lock)
            {
                if (_processMap.TryGetValue(accountId, out var p))
                {
                    try
                    {
                        if (!p.HasExited)
                        {
                            ProcessHelper.KillTree(p);
                            p.WaitForExit(1000);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[LauncherService] Error killing {accountId}: {ex.Message}");
                    }
                    _processMap.Remove(accountId);
                }
            }
            
            // 4. Clean up any saved break state
            DeleteBreakState(accountId);
        }

        public void KillAllClients()
        {
            // 1. SIGNAL THE LOOP TO STOP IMMEDIATELY
            _globalLaunchCts?.Cancel();
            
            lock (_lock)
            {
                // 2. Cancel ALL break tasks (including those without active processes)
                foreach (var accountId in _breakCancellations.Keys.ToList())
                {
                    CancelBreak(accountId);
                    OnBreakStatusChanged?.Invoke(accountId, false, null);
                }
                
                // 3. Kill every running process in the map
                foreach (var accountId in _processMap.Keys.ToList())
                {
                    try
                    {
                        if (_processMap.TryGetValue(accountId, out var p) && !p.HasExited)
                        {
                            ProcessHelper.KillTree(p);
                            p.WaitForExit(1000);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[LauncherService] Error killing {accountId}: {ex.Message}");
                    }
                }
                
                _processMap.Clear();
                _breakCancellations.Clear();
            }
            
            // 4. Clean up all break states
            string stateDir = Path.Combine(Directory.GetCurrentDirectory(), "BreakStates");
            if (Directory.Exists(stateDir))
            {
                try
                {
                    Directory.Delete(stateDir, recursive: true);
                    Console.WriteLine("[LauncherService] Cleared all break states");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LauncherService] Error clearing break states: {ex.Message}");
                }
            }
        }

        public void HandleBreak(string accountId, int durationSeconds, string nextActivity)
        {
            Console.WriteLine($"[LauncherService] Break request: {accountId} for {durationSeconds}s, next: {nextActivity}");
            
            // 1. Save state to disk for crash recovery
            var state = new AccountBreakState
            {
                AccountId = accountId,
                NextActivity = nextActivity,
                BreakEndTime = DateTime.Now.AddSeconds(durationSeconds)
            };
            SaveBreakState(state);

            // 2. Cancel any existing break task for this account first
            CancelBreak(accountId);

            // 3. Create new cancellation token
            var cts = new CancellationTokenSource();
            lock (_lock)
            {
                _breakCancellations[accountId] = cts;
            }

            // 4. Notify UI
            OnBreakStatusChanged?.Invoke(accountId, true, state.BreakEndTime);

            // 5. Schedule restart
            Task.Run(async () =>
            {
                try
                {
                    Console.WriteLine($"[LauncherService] Break started for {accountId}, waiting {durationSeconds}s");
                    
                    // Wait for the break duration, but allow cancellation
                    await Task.Delay(durationSeconds * 1000, cts.Token);

                    // If we weren't cancelled, restart the client
                    Console.WriteLine($"[LauncherService] Break finished for {accountId}. Restarting with script: {nextActivity}");
                    
                    string args = (nextActivity == "none") ? "" : $"--script={nextActivity}";
                    
                    StartClient(accountId, args);
                    
                    // Clean up the saved state
                    DeleteBreakState(accountId);
                }
                catch (TaskCanceledException)
                {
                    Console.WriteLine($"[LauncherService] Break cancelled for {accountId}.");
                    DeleteBreakState(accountId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LauncherService] Error during break for {accountId}: {ex.Message}");
                }
                finally
                {
                    OnBreakStatusChanged?.Invoke(accountId, false, null);
                    lock (_lock) 
                    {
                        _breakCancellations.Remove(accountId);
                    }
                }
            }, cts.Token);
        }

        #region State Persistence

        private class AccountBreakState
        {
            public string AccountId { get; set; }
            public string NextActivity { get; set; }
            public DateTime BreakEndTime { get; set; }
        }

        private void SaveBreakState(AccountBreakState state)
        {
            try
            {
                string stateDir = Path.Combine(Directory.GetCurrentDirectory(), "BreakStates");
                Directory.CreateDirectory(stateDir);
                
                string stateFile = Path.Combine(stateDir, $"{state.AccountId}.json");
                string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(stateFile, json);
                
                Console.WriteLine($"[LauncherService] Saved break state for {state.AccountId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LauncherService] Error saving break state: {ex.Message}");
            }
        }

        private AccountBreakState? LoadBreakState(string accountId)
        {
            try
            {
                string stateFile = Path.Combine(Directory.GetCurrentDirectory(), "BreakStates", $"{accountId}.json");
                if (!File.Exists(stateFile)) return null;

                string json = File.ReadAllText(stateFile);
                return JsonSerializer.Deserialize<AccountBreakState>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LauncherService] Error loading break state: {ex.Message}");
                return null;
            }
        }

        private void DeleteBreakState(string accountId)
        {
            try
            {
                string stateFile = Path.Combine(Directory.GetCurrentDirectory(), "BreakStates", $"{accountId}.json");
                if (File.Exists(stateFile))
                {
                    File.Delete(stateFile);
                    Console.WriteLine($"[LauncherService] Deleted break state for {accountId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LauncherService] Error deleting break state: {ex.Message}");
            }
        }

        private void ResumePendingBreaks()
        {
            Console.WriteLine("[LauncherService] Checking for pending breaks from previous session...");
            
            string stateDir = Path.Combine(Directory.GetCurrentDirectory(), "BreakStates");
            if (!Directory.Exists(stateDir))
            {
                Console.WriteLine("[LauncherService] No pending breaks found");
                return;
            }

            var stateFiles = Directory.GetFiles(stateDir, "*.json");
            if (stateFiles.Length == 0)
            {
                Console.WriteLine("[LauncherService] No pending breaks found");
                return;
            }

            Console.WriteLine($"[LauncherService] Found {stateFiles.Length} pending break(s)");

            foreach (var stateFile in stateFiles)
            {
                try
                {
                    var state = JsonSerializer.Deserialize<AccountBreakState>(File.ReadAllText(stateFile));
                    if (state == null) continue;

                    var remaining = (state.BreakEndTime - DateTime.Now).TotalSeconds;
                    
                    if (remaining > 0)
                    {
                        Console.WriteLine($"[LauncherService] Resuming break for {state.AccountId}, {(int)remaining}s remaining");
                        HandleBreak(state.AccountId, (int)remaining, state.NextActivity);
                    }
                    else
                    {
                        Console.WriteLine($"[LauncherService] Break expired for {state.AccountId}, restarting now");
                        
                        string args = string.IsNullOrEmpty(state.NextActivity) || state.NextActivity == "none" 
                            ? "" 
                            : $"--script={state.NextActivity}";
                        
                        StartClient(state.AccountId, args);
                        File.Delete(stateFile);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LauncherService] Error resuming break from {stateFile}: {ex.Message}");
                }
            }
        }

        #endregion

        public void Dispose()
        {
            _isServerRunning = false;
            _listener?.Stop();
            KillAllClients();
        }
    }
}
