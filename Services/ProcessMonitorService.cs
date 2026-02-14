using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using JagexAccountSwitcher.Model;

namespace JagexAccountSwitcher.Services
{
    public class ProcessMonitorService
    {
        private readonly LauncherService _launcherService;
        private CancellationTokenSource? _monitoringCts;
        private bool _isMonitoring;

        public ProcessMonitorService(LauncherService launcherService)
        {
            _launcherService = launcherService;
        }

        public void InitializeMonitoring(ObservableCollection<MassAccountLinkerModel> accounts)
        {
            if (_isMonitoring) return;
            _isMonitoring = true;
            _monitoringCts = new CancellationTokenSource();

            _ = MonitorProcessHealth(accounts, _monitoringCts.Token);
            _ = UpdateProcessLifetimes(accounts, _monitoringCts.Token);
        }

        private async Task MonitorProcessHealth(ObservableCollection<MassAccountLinkerModel> accounts, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), ct);
                    
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        foreach (var model in accounts.ToList())
                        {
                            // If on break, the LauncherService is handling the process state
                            if (model.IsOnBreak) continue;

                            var serviceProcess = _launcherService.AccountProcesses
                                .FirstOrDefault(kvp => kvp.Key == model.Account.AccountName || kvp.Key == model.Account.Id).Value;

                            if (model.Process != serviceProcess)
                            {
                                model.Process = serviceProcess;
                            }
                        }
                    });
                }
                catch (TaskCanceledException) { break; }
                catch (Exception ex) { Console.WriteLine($"[ProcessMonitor] Health Error: {ex.Message}"); }
            }
        }
        
        private async Task UpdateProcessLifetimes(ObservableCollection<MassAccountLinkerModel> accounts, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), ct);
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        foreach (var model in accounts)
                        {
                            // 1. Refresh Lifetime display
                            model.RefreshLifetime();
                            
                            // 2. Refresh Break Timer if active
                            if (model.IsOnBreak)
                            {
                                model.RefreshBreakTimer();
                            }
                        }
                    });
                }
                catch (TaskCanceledException) { break; }
                catch (Exception ex) { Console.WriteLine($"Error: {ex.Message}"); }
            }
        }

        public void StopMonitoring()
        {
            _monitoringCts?.Cancel();
            _isMonitoring = false;
        }
    }
}
