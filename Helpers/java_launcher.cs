using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace JagexAccountSwitcher.Helpers
{
    public static class CrossPlatformJavaLauncher
    {
        public static Process? LaunchJar(
            string jarPath, 
            string arguments = "", 
            string accountName = "Unknown",
            bool enableAssertions = false,
            bool logOutputToConsole = false,
            bool showTerminalWindow = false)
        {
            Console.WriteLine($"[JavaLauncher] === Starting Launch Process ===");
            Console.WriteLine($"[JavaLauncher] Account: {accountName}");
            Console.WriteLine($"[JavaLauncher] JAR Path: {jarPath}");
            Console.WriteLine($"[JavaLauncher] Arguments: {arguments}");
            Console.WriteLine($"[JavaLauncher] Show Terminal: {showTerminalWindow}");
            Console.WriteLine($"[JavaLauncher] Platform: {RuntimeInformation.OSDescription}");

            if (string.IsNullOrWhiteSpace(jarPath))
            {
                Console.WriteLine("[JavaLauncher] ERROR: JAR path is null or empty!");
                return null;
            }

            jarPath = Path.GetFullPath(jarPath);
            Console.WriteLine($"[JavaLauncher] Full JAR Path: {jarPath}");

            if (!File.Exists(jarPath))
            {
                Console.WriteLine($"[JavaLauncher] ERROR: JAR file not found at: {jarPath}");
                return null;
            }

            string? javaExecutable = FindJavaExecutable();
            if (string.IsNullOrEmpty(javaExecutable))
            {
                Console.WriteLine("[JavaLauncher] ERROR: Java executable not found!");
                return null;
            }
            Console.WriteLine($"[JavaLauncher] Java Executable: {javaExecutable}");

            string workingDirectory = Path.GetDirectoryName(jarPath) ?? Directory.GetCurrentDirectory();
            Console.WriteLine($"[JavaLauncher] Working Directory: {workingDirectory}");

            string logDir = Path.Combine(workingDirectory, "logs");
            Directory.CreateDirectory(logDir);
            Console.WriteLine($"[JavaLauncher] Log Directory: {logDir}");

            string safeAccountName = string.Concat(accountName.Split(Path.GetInvalidFileNameChars()));
            string logFile = Path.Combine(logDir, $"{safeAccountName}_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            Console.WriteLine($"[JavaLauncher] Log File: {logFile}");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Console.WriteLine("[JavaLauncher] Using Linux launcher");
                return LaunchLinux(javaExecutable, jarPath, arguments, workingDirectory, logFile, showTerminalWindow, enableAssertions, accountName);
            }

            Console.WriteLine("[JavaLauncher] Using standard launcher");
            return LaunchStandard(javaExecutable, jarPath, arguments, workingDirectory, showTerminalWindow, enableAssertions);
        }

        private static Process? LaunchLinux(
            string javaExec, string jarPath, string args, string workingDir, 
            string logFile, bool showTerminal, bool assertions, string accountName)
        {
            Console.WriteLine($"[JavaLauncher-Linux] === Linux Launch Details ===");
            Console.WriteLine($"[JavaLauncher-Linux] Java: {javaExec}");
            Console.WriteLine($"[JavaLauncher-Linux] JAR: {jarPath}");
            Console.WriteLine($"[JavaLauncher-Linux] Args: {args}");
            Console.WriteLine($"[JavaLauncher-Linux] Working Dir: {workingDir}");
            Console.WriteLine($"[JavaLauncher-Linux] Show Terminal: {showTerminal}");

            // Build the Java command with JVM args BEFORE -jar
            StringBuilder javaCmd = new StringBuilder($"\"{javaExec}\" ");
            if (assertions) javaCmd.Append("-ea ");
            
            // Add JVM arguments (like -D properties) before -jar
            if (!string.IsNullOrWhiteSpace(args))
            {
                javaCmd.Append($"{args} ");
            }
            
            javaCmd.Append($"-jar \"{jarPath}\"");
            
            Console.WriteLine($"[JavaLauncher-Linux] Java Command: {javaCmd}");

            // ALWAYS launch Java directly to get the actual process
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = javaExec,
                Arguments = BuildJavaArguments(assertions, args, jarPath),
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                CreateNoWindow = !showTerminal,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = false
            };

            Console.WriteLine($"[JavaLauncher-Linux] Direct Java launch (proper tracking)");
            Console.WriteLine($"[JavaLauncher-Linux] FileName: {psi.FileName}");
            Console.WriteLine($"[JavaLauncher-Linux] Arguments: {psi.Arguments}");

            try
            {
                Console.WriteLine("[JavaLauncher-Linux] Starting process...");
                var process = Process.Start(psi);
                
                if (process == null)
                {
                    Console.WriteLine("[JavaLauncher-Linux] ERROR: Process.Start returned null!");
                    return null;
                }

                Console.WriteLine($"[JavaLauncher-Linux] ✓ Process started successfully!");
                Console.WriteLine($"[JavaLauncher-Linux] PID: {process.Id}");
                Console.WriteLine($"[JavaLauncher-Linux] Start Time: {process.StartTime}");
                Console.WriteLine($"[JavaLauncher-Linux] Has Exited: {process.HasExited}");

                // Setup output redirection
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Console.WriteLine($"[{accountName}] STDOUT: {e.Data}");
                        try
                        {
                            File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss}] {e.Data}\n");
                        }
                        catch { }
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Console.WriteLine($"[{accountName}] STDERR: {e.Data}");
                        try
                        {
                            File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss}] ERROR: {e.Data}\n");
                        }
                        catch { }
                    }
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                
                Console.WriteLine($"[JavaLauncher-Linux] Output redirection enabled");

                // Monitor process health
                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(5000); // Wait 5 seconds
                        if (process.HasExited)
                        {
                            Console.WriteLine($"[JavaLauncher-Linux] WARNING: Process {process.Id} exited early!");
                            Console.WriteLine($"[JavaLauncher-Linux] Exit Code: {process.ExitCode}");
                        }
                        else
                        {
                            Console.WriteLine($"[JavaLauncher-Linux] Process {process.Id} is still running after 5s");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[JavaLauncher-Linux] Error checking process: {ex.Message}");
                    }
                });

                return process;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[JavaLauncher-Linux] EXCEPTION during process start:");
                Console.WriteLine($"[JavaLauncher-Linux] Message: {ex.Message}");
                Console.WriteLine($"[JavaLauncher-Linux] Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[JavaLauncher-Linux] Inner Exception: {ex.InnerException.Message}");
                }
                return null;
            }
        }

        private static Process? LaunchStandard(string javaExec, string jarPath, string args, string workingDir, bool showTerminal, bool assertions)
        {
            Console.WriteLine($"[JavaLauncher-Standard] === Standard Launch ===");
            
            string javaArgs = BuildJavaArguments(assertions, args, jarPath);

            Console.WriteLine($"[JavaLauncher-Standard] Java: {javaExec}");
            Console.WriteLine($"[JavaLauncher-Standard] Args: {javaArgs}");

            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = javaExec,
                    Arguments = javaArgs,
                    WorkingDirectory = workingDir,
                    UseShellExecute = showTerminal,
                    CreateNoWindow = !showTerminal
                });

                if (process != null)
                {
                    Console.WriteLine($"[JavaLauncher-Standard] ✓ Process started: PID {process.Id}");
                }
                else
                {
                    Console.WriteLine($"[JavaLauncher-Standard] ERROR: Process.Start returned null");
                }

                return process;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[JavaLauncher-Standard] EXCEPTION: {ex.Message}");
                Console.WriteLine($"[JavaLauncher-Standard] Stack: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Builds Java arguments with JVM options BEFORE -jar
        /// </summary>
        private static string BuildJavaArguments(bool enableAssertions, string jvmArgs, string jarPath)
        {
            StringBuilder result = new StringBuilder();
            
            if (enableAssertions)
            {
                result.Append("-ea ");
            }
            
            // Add JVM arguments (like -D properties) BEFORE -jar
            if (!string.IsNullOrWhiteSpace(jvmArgs))
            {
                result.Append($"{jvmArgs} ");
            }
            
            result.Append($"-jar \"{jarPath}\"");
            
            return result.ToString();
        }

        // --- Required Methods for MassAccountHandler ---

        public static bool IsJavaAvailable()
        {
            bool available = !string.IsNullOrEmpty(FindJavaExecutable());
            Console.WriteLine($"[JavaLauncher] Java Available: {available}");
            return available;
        }

        public static async Task<string> GetJavaVersion()
        {
            string? javaExe = FindJavaExecutable();
            if (string.IsNullOrEmpty(javaExe))
            {
                Console.WriteLine("[JavaLauncher] Cannot get Java version: Java not found");
                return "Not Found";
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = javaExe,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    Console.WriteLine("[JavaLauncher] Cannot get Java version: Process.Start returned null");
                    return "Unknown";
                }

                string output = await process.StandardError.ReadToEndAsync();
                string version = output.Split(Environment.NewLine).FirstOrDefault() ?? "Unknown";
                Console.WriteLine($"[JavaLauncher] Java Version: {version}");
                return version;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[JavaLauncher] Error getting Java version: {ex.Message}");
                return "Error";
            }
        }

        public static void KillProcessWithChildren(Process? process)
        {
            if (process == null)
            {
                Console.WriteLine("[JavaLauncher] Kill: Process is null");
                return;
            }

            if (process.HasExited)
            {
                Console.WriteLine($"[JavaLauncher] Kill: Process {process.Id} already exited");
                return;
            }

            Console.WriteLine($"[JavaLauncher] Killing process {process.Id}...");

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    // Kill the process group to ensure all children die
                    try
                    {
                        var killProc = Process.Start("pkill", $"-P {process.Id}");
                        killProc?.WaitForExit(1000);
                        Console.WriteLine($"[JavaLauncher] Killed children of PID {process.Id}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[JavaLauncher] Warning: pkill failed: {ex.Message}");
                    }

                    process.Kill();
                    Console.WriteLine($"[JavaLauncher] ✓ Process {process.Id} killed");
                }
                else
                {
                    process.Kill(true);
                    Console.WriteLine($"[JavaLauncher] ✓ Process {process.Id} killed (with children)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[JavaLauncher] Error killing process: {ex.Message}");
            }
        }

        private static string? FindJavaExecutable()
        {
            Console.WriteLine("[JavaLauncher] Searching for Java executable...");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.WriteLine("[JavaLauncher] Platform: Windows, using 'java.exe'");
                return "java.exe";
            }

            // Linux/macOS
            string[] paths = {
                "/usr/bin/java",
                "/usr/lib/jvm/default-java/bin/java",
                "/usr/lib/jvm/java-17-openjdk-amd64/bin/java",
                "/usr/lib/jvm/java-11-openjdk-amd64/bin/java"
            };

            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    Console.WriteLine($"[JavaLauncher] Found Java at: {path}");
                    return path;
                }
            }

            // Try 'java' from PATH
            Console.WriteLine("[JavaLauncher] Trying 'java' from PATH");
            return "java";
        }
    }
}
