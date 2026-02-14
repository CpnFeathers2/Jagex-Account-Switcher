using System;

namespace JagexAccountSwitcher.Model
{
    /// <summary>
    /// Tracks session data for each account across restarts
    /// </summary>
    public class AccountSessionData
    {
        /// <summary>
        /// Unique account identifier from credentials file (e.g., "0017")
        /// </summary>
        public string AccountId { get; set; }
        
        /// <summary>
        /// Display name for the account
        /// </summary>
        public string AccountName { get; set; }
        
        /// <summary>
        /// Total playtime across all sessions in milliseconds
        /// </summary>
        public long TotalPlaytimeMs { get; set; }
        
        /// <summary>
        /// Cumulative Defence XP gained across all sessions
        /// </summary>
        public int CumulativeDefXp { get; set; }
        
        /// <summary>
        /// Starting Defence XP when tracking began
        /// </summary>
        public int StartDefXp { get; set; }
        
        /// <summary>
        /// Last recorded Defence XP
        /// </summary>
        public int LastDefXp { get; set; }
        
        /// <summary>
        /// Whether this is a fresh account
        /// </summary>
        public bool IsFreshie { get; set; }
        
        /// <summary>
        /// Number of rounds completed as a freshie
        /// </summary>
        public int FreshieRoundCount { get; set; }
        
        /// <summary>
        /// Last activity: "monks" or "mossies"
        /// </summary>
        public string LastActivity { get; set; }
        
        /// <summary>
        /// Whether this account is banned
        /// </summary>
        public bool IsBanned { get; set; }
        
        /// <summary>
        /// Last time this account logged in
        /// </summary>
        public DateTime? LastLoginTime { get; set; }
        
        /// <summary>
        /// When the current session started (null if not in session)
        /// </summary>
        public DateTime? CurrentSessionStart { get; set; }
        
        /// <summary>
        /// Path to the credentials file for this account
        /// </summary>
        public string CredentialsFilePath { get; set; }

        public AccountSessionData()
        {
            AccountId = string.Empty;
            AccountName = string.Empty;
            TotalPlaytimeMs = 0;
            CumulativeDefXp = 0;
            StartDefXp = -1;
            LastDefXp = -1;
            IsFreshie = false;
            FreshieRoundCount = 0;
            LastActivity = string.Empty;
            IsBanned = false;
            LastLoginTime = null;
            CurrentSessionStart = null;
            CredentialsFilePath = string.Empty;
        }
        
        /// <summary>
        /// Gets the current session duration if in session
        /// </summary>
        public TimeSpan GetCurrentSessionDuration()
        {
            if (CurrentSessionStart.HasValue)
            {
                return DateTime.Now - CurrentSessionStart.Value;
            }
            return TimeSpan.Zero;
        }
        
        /// <summary>
        /// Gets total playtime including current session
        /// </summary>
        public TimeSpan GetTotalPlaytime()
        {
            var total = TimeSpan.FromMilliseconds(TotalPlaytimeMs);
            if (CurrentSessionStart.HasValue)
            {
                total += GetCurrentSessionDuration();
            }
            return total;
        }
    }
}
