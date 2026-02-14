using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Linq;

namespace JagexAccountSwitcher.Helpers
{
    public static class ProcessHelper
    {
        /// <summary>
        /// Starts a process with proper output buffer draining to prevent deadlocks
        /// </summary>
        public static Process StartProcess(string file, string args, bool showConsole)
        {
            var psi = new ProcessStartInfo
            {
                FileName = file,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = !showConsole
            };

            Console.WriteLine($"[ProcessHelper] Executing: {file} {args}");

            var p = new Process { StartInfo = psi };

            // CRITICAL: Attach handlers to drain buffers asynchronously
            p.OutputDataReceived += (sender, e) => 
            { 
                if (e.Data != null) 
                    Console.WriteLine($"[Client-Out] {e.Data}"); 
            };
            
            p.ErrorDataReceived += (sender, e) => 
            { 
                if (e.Data != null) 
                    Console.WriteLine($"[Client-Err] {e.Data}"); 
            };

            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            return p;
        }

        /// <summary>
        /// Kills a process and all its children using platform-specific methods
        /// </summary>
        public static void KillTree(Process p)
        {
            if (p == null)
            {
                Console.WriteLine("[ProcessHelper] KillTree called with null process");
                return;
            }
            
            try 
            {
                if (p.HasExited)
                {
                    Console.WriteLine($"[ProcessHelper] Process {p.Id} already exited");
                    return;
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    KillWindowsTree(p.Id);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    KillLinuxTree(p.Id);
                }
                else
                {
                    // Fallback for other platforms
                    p.Kill(entireProcessTree: true);
                }
                
                Console.WriteLine($"[ProcessHelper] ✓ Killed process tree for PID {p.Id}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProcessHelper] Error killing process {p.Id}: {ex.Message}");
            }
        }

        /// <summary>
        /// Windows-specific process tree termination
        /// </summary>
        private static void KillWindowsTree(int pid)
        {
            try
            {
                var killProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/PID {pid} /T /F",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                killProcess.Start();
                killProcess.WaitForExit(5000);
                
                if (killProcess.ExitCode != 0)
                {
                    Console.WriteLine($"[ProcessHelper] taskkill returned code {killProcess.ExitCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProcessHelper] Windows kill error: {ex.Message}");
            }
        }

        /// <summary>
        /// Linux-specific process tree termination using process groups
        /// </summary>
        private static void KillLinuxTree(int pid)
        {
            try
            {
                // Method 1: Kill the process group (recommended for Java processes)
                // The negative PID kills all processes in the process group
                try
                {
                    var killGroupProcess = Process.Start(new ProcessStartInfo
                    {
                        FileName = "kill",
                        Arguments = $"-9 -{pid}",  // Negative PID = process group
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    });
                    
                    killGroupProcess?.WaitForExit(2000);
                    Console.WriteLine($"[ProcessHelper] Killed process group -{pid}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ProcessHelper] Process group kill failed: {ex.Message}");
                }

                // Method 2: Kill all children using pkill
                try
                {
                    var pkillProcess = Process.Start(new ProcessStartInfo
                    {
                        FileName = "pkill",
                        Arguments = $"-9 -P {pid}",  // Kill all children of PID
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    });
                    
                    pkillProcess?.WaitForExit(2000);
                    Console.WriteLine($"[ProcessHelper] Killed children of PID {pid}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ProcessHelper] pkill failed: {ex.Message}");
                }

                // Method 3: Direct kill of the parent process
                try
                {
                    var killProcess = Process.Start(new ProcessStartInfo
                    {
                        FileName = "kill",
                        Arguments = $"-9 {pid}",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    });
                    
                    killProcess?.WaitForExit(2000);
                    Console.WriteLine($"[ProcessHelper] Killed parent process {pid}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ProcessHelper] Direct kill failed: {ex.Message}");
                }

                // Small delay to allow OS cleanup
                Thread.Sleep(500);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProcessHelper] Linux kill error: {ex.Message}");
            }
        }

        /// <summary>
        /// Safely waits for a process to exit with timeout
        /// </summary>
        public static bool WaitForExitSafe(Process p, int timeoutMs)
        {
            if (p == null) return true;
            
            try 
            { 
                return p.WaitForExit(timeoutMs); 
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProcessHelper] WaitForExit error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if a process is actually running (not zombie)
        /// </summary>
        public static bool IsProcessRunning(int pid)
        {
            try
            {
                var process = Process.GetProcessById(pid);
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets all child process IDs for a given parent PID (Linux)
        /// </summary>
        public static int[] GetChildProcessIds(int parentPid)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return Array.Empty<int>();

            try
            {
                var pgrep = Process.Start(new ProcessStartInfo
                {
                    FileName = "pgrep",
                    Arguments = $"-P {parentPid}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (pgrep == null) return Array.Empty<int>();

                var output = pgrep.StandardOutput.ReadToEnd();
                pgrep.WaitForExit();

                if (string.IsNullOrWhiteSpace(output))
                    return Array.Empty<int>();

                return output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s.Trim(), out var id) ? id : -1)
                    .Where(id => id > 0)
                    .ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProcessHelper] Error getting child PIDs: {ex.Message}");
                return Array.Empty<int>();
            }
        }
    }
}
