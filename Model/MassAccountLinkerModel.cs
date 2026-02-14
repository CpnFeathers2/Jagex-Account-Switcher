using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace JagexAccountSwitcher.Model
{
    public class MassAccountLinkerModel : INotifyPropertyChanged
    {
        private Process? _process;
        private RunescapeAccount _account;
        private bool _isOnBreak;
        private DateTime? _sessionStartTime;
        private DateTime? _breakEndTime;

        public RunescapeAccount Account
        {
            get => _account;
            set { _account = value; OnPropertyChanged(); }
        }

        public string StatusDisplay
        {
            get
            {
                if (IsOnBreak) return "On Break";
                if (IsRunning) return "Running";
                return "Stopped";
            }
        }

        public string StatusColor
        {
            get
            {
                if (IsOnBreak) return "Orange";
                if (IsRunning) return "LightGreen";
                return "White";
            }
        }

        public bool IsOnBreak
        {
            get => _isOnBreak;
            set 
            { 
                _isOnBreak = value; 
                // If turning off break, clear the end time
                if (!value) _breakEndTime = null;
                
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(StatusDisplay));
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(BreakTimer)); // Notify UI to update timer
            }
        }

        public DateTime? BreakEndTime
        {
            get => _breakEndTime;
            set 
            { 
                _breakEndTime = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(BreakTimer)); 
            }
        }

        public string BreakTimer
        {
            get
            {
                if (!IsOnBreak || _breakEndTime == null) return "";
                var remaining = _breakEndTime.Value - DateTime.Now;
                if (remaining.TotalSeconds <= 0) return "Resuming...";
                return $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
            }
        }

        public Process? Process
{
    get => _process;
    set
    {
        if (_process != value)
        {
            _process = value;
            if (value != null)
            {
                // Process started - set start time if not already set
                if (_sessionStartTime == null) 
                {
                    _sessionStartTime = DateTime.Now;
                    Console.WriteLine($"[MassAccountLinkerModel] Session started for {Account?.AccountName ?? "Unknown"}");
                }
            }
            else
            {
                // ============================================================
                // CRITICAL FIX: Only reset lifetime if NOT on break
                // ============================================================
                // When the bot is on break, we want to maintain the session 
                // start time so the Lifetime counter continues to run.
                // We only reset the lifetime when the bot is explicitly killed
                // or stopped (not during a break).
                
                if (!_isOnBreak)
                {
                    // Process killed/stopped (not on break) - Hard reset lifetime
                    _sessionStartTime = null;
                    Console.WriteLine($"[MassAccountLinkerModel] Session ended for {Account?.AccountName ?? "Unknown"} (lifetime reset)");
                }
                else
                {
                    // Bot is on break - keep session time intact
                    Console.WriteLine($"[MassAccountLinkerModel] Process cleared for {Account?.AccountName ?? "Unknown"} but on break (lifetime preserved)");
                }
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(IsNotRunning)); 
            OnPropertyChanged(nameof(ProcessIdDisplay));
            OnPropertyChanged(nameof(StatusDisplay));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(Lifetime)); // Force UI update
        }
    }
}

        public string Lifetime
        {
            get
            {
                if (_sessionStartTime == null) return "00:00:00";
                var elapsed = DateTime.Now - _sessionStartTime.Value;
                return $"{(int)elapsed.TotalHours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
            }
        }

        public string ProcessIdDisplay => (IsRunning && _process != null) ? _process.Id.ToString() : "N/A";
        public bool IsRunning => _process != null && !_process.HasExited;
        public bool IsNotRunning => !IsRunning && !IsOnBreak;

        public void ResetSession()
        {
            _sessionStartTime = null;
            _breakEndTime = null;
            IsOnBreak = false;
            Process = null;
            OnPropertyChanged(nameof(Lifetime));
            OnPropertyChanged(nameof(BreakTimer));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Methods for the Service to call
        public void RefreshLifetime() => OnPropertyChanged(nameof(Lifetime));
        public void RefreshBreakTimer() => OnPropertyChanged(nameof(BreakTimer));
    }
}
