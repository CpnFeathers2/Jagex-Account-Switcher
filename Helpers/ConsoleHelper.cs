using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace JagexAccountSwitcher.Helpers
{
    public static class ConsoleHelper
    {
        // Windows-specific console APIs
        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole();

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        private static TextWriterTraceListener _consoleTraceListener;
        private static ConsoleTraceListener _systemConsoleTraceListener;
        private static bool _isConsoleAllocated = false;

        /// <summary>
        /// Shows the console window and redirects all output to it
        /// </summary>
        public static void ShowConsole()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ShowConsoleWindows();
            }
            else
            {
                // On Linux/macOS, console is always available if launched from terminal
                // Just ensure we're writing to it
                SetupConsoleOutput();
            }
        }

        /// <summary>
        /// Hides the console window (Windows only)
        /// </summary>
        public static void HideConsole()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                HideConsoleWindows();
            }
            // On Linux/macOS, we don't hide the terminal
        }

        /// <summary>
        /// Checks if console is visible
        /// </summary>
        public static bool IsConsoleVisible()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetConsoleWindow() != IntPtr.Zero;
            }
            else
            {
                // On Linux/macOS, if stdout is a terminal, console is "visible"
                return !Console.IsOutputRedirected;
            }
        }

        #region Windows-specific implementation

        private static void ShowConsoleWindows()
        {
            IntPtr consoleWindow = GetConsoleWindow();

            if (consoleWindow == IntPtr.Zero)
            {
                // Allocate a new console
                if (AllocConsole())
                {
                    _isConsoleAllocated = true;
                    
                    // Set console title
                    Console.Title = "JagexAccountSwitcher - Debug Console";
                    
                    // Reset standard output and error streams
                    var standardOutput = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                    Console.SetOut(standardOutput);
                    
                    var standardError = new StreamWriter(Console.OpenStandardError()) { AutoFlush = true };
                    Console.SetError(standardError);

                    SetupConsoleOutput();
                    
                    Console.WriteLine("=== Console Started ===");
                    Console.WriteLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    Console.WriteLine("=======================");
                }
            }
            else
            {
                // Console exists, just show it
                ShowWindow(consoleWindow, SW_SHOW);
                SetupConsoleOutput();
            }
        }

        private static void HideConsoleWindows()
        {
            IntPtr consoleWindow = GetConsoleWindow();
            
            if (consoleWindow != IntPtr.Zero)
            {
                // Remove trace listeners before hiding
                RemoveConsoleListeners();
                
                if (_isConsoleAllocated)
                {
                    // If we allocated it, free it
                    FreeConsole();
                    _isConsoleAllocated = false;
                }
                else
                {
                    // Otherwise just hide it
                    ShowWindow(consoleWindow, SW_HIDE);
                }
            }
        }

        #endregion

        #region Cross-platform console output setup

        private static void SetupConsoleOutput()
        {
            // Remove existing listeners to avoid duplicates
            RemoveConsoleListeners();

            // Add trace listener to capture Debug.WriteLine and Trace.WriteLine
            _consoleTraceListener = new TextWriterTraceListener(Console.Out)
            {
                Name = "ConsoleTraceListener"
            };
            Trace.Listeners.Add(_consoleTraceListener);

            // Add system console trace listener for complete output capture
            _systemConsoleTraceListener = new ConsoleTraceListener(useErrorStream: false)
            {
                Name = "SystemConsoleTraceListener"
            };
            Trace.Listeners.Add(_systemConsoleTraceListener);

            // Enable auto-flush so output appears immediately
            Trace.AutoFlush = true;
            Debug.AutoFlush = true;

            Console.WriteLine("[ConsoleHelper] Output redirection setup complete");
        }

        private static void RemoveConsoleListeners()
        {
            if (_consoleTraceListener != null)
            {
                Trace.Listeners.Remove(_consoleTraceListener);
                _consoleTraceListener.Dispose();
                _consoleTraceListener = null!;
            }

            if (_systemConsoleTraceListener != null)
            {
                Trace.Listeners.Remove(_systemConsoleTraceListener);
                _systemConsoleTraceListener.Dispose();
                _systemConsoleTraceListener = null!;
            }
        }

        #endregion

        /// <summary>
        /// Writes a timestamped message to the console
        /// </summary>
        public static void WriteLine(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] {message}");
        }

        /// <summary>
        /// Writes a timestamped error message to the console
        /// </summary>
        public static void WriteError(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            Console.Error.WriteLine($"[{timestamp}] ERROR: {message}");
        }
    }
}
