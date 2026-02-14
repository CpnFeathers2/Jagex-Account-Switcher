using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace JagexAccountSwitcher.Services
{
    public class BreakHttpServer
    {
        private HttpListener _listener;
        private readonly LauncherService _launcherService;
        private bool _isRunning;

        public BreakHttpServer(LauncherService launcherService)
        {
            _launcherService = launcherService;
        }

        public void Start(int port = 8080)
        {
            _listener = new HttpListener();
            // Using 127.0.0.1 instead of localhost to avoid IPv6 issues
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/break/");
            _listener.Start();
            _isRunning = true;
            
            Task.Run(ListenLoop);
            Console.WriteLine($"[HTTP] Server started on 127.0.0.1:{port}/break/");
        }

        private async Task ListenLoop()
        {
            while (_isRunning)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequest(context));
                }
                catch (Exception ex) { Console.WriteLine($"[HTTP] Error: {ex.Message}"); }
            }
        }

        private async Task HandleRequest(HttpListenerContext context)
        {
            try
            {
                if (context.Request.HttpMethod == "POST")
                {
                    using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                    string json = await reader.ReadToEndAsync();
                    
                    // Simple parse - adjust based on your exact JSON structure
                    using JsonDocument doc = JsonDocument.Parse(json);
                    JsonElement root = doc.RootElement;

                    string accountId = root.GetProperty("accountId").GetString();
                    int duration = root.TryGetProperty("duration", out var d) ? d.GetInt32() : 0;
                    string nextActivity = root.TryGetProperty("nextActivity", out var n) ? n.GetString() : "none";

                    Console.WriteLine($"[HTTP] Break Request received for {accountId}");
                    
                    // Call your existing logic
                    _launcherService.HandleBreak(accountId, duration, nextActivity);
                }

                context.Response.StatusCode = 200;
                byte[] buffer = Encoding.UTF8.GetBytes("OK");
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                Console.WriteLine($"[HTTP] Request handling error: {ex.Message}");
            }
            finally
            {
                context.Response.Close();
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();
        }
    }
}
