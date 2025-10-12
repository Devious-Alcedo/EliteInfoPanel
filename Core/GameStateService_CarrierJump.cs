// CARRIER JUMP SIMPLIFIED IMPLEMENTATION
// This file contains the clean carrier jump logic to replace the messy implementation

using System;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using EliteInfoPanel.Core.Models;
using Serilog;

namespace EliteInfoPanel.Core
{
    public partial class GameStateService
    {
        #region Carrier Jump - Private Fields
        
        private readonly CarrierJumpState _carrierJumpState = new();
        private DispatcherTimer _carrierJumpTimer;
        private DispatcherTimer _overlayTimeoutTimer;
        
        #endregion

        #region Carrier Jump - Public Properties
        
        /// <summary>
        /// Indicates whether the carrier jump overlay should be visible
        /// </summary>
        public bool ShowCarrierJumpOverlay => IsOnFleetCarrier && (_carrierJumpState?.ShouldShowOverlay ?? false);
        
        /// <summary>
        /// Gets the destination system for the carrier jump
        /// </summary>
        public string CarrierJumpDestination => _carrierJumpState?.DestinationSystem;
        
        /// <summary>
        /// Gets the countdown seconds until jump (0 if not scheduled or time has passed)
        /// </summary>
        public int CarrierJumpCountdownSeconds => _carrierJumpState?.CountdownSeconds ?? 0;
        
        // Compatibility properties for ViewModels
        
        /// <summary>
        /// Legacy property: Use CarrierJumpDestination instead
        /// </summary>
        public string CarrierJumpDestinationSystem => _carrierJumpState?.DestinationSystem;
        
        /// <summary>
        /// Legacy property: Use CarrierJumpDestination instead
        /// </summary>
        public string CarrierJumpDestinationBody => _carrierJumpState?.DestinationBody;
        
        /// <summary>
        /// Legacy property: Use CarrierJumpState.IsJumpScheduled instead
        /// </summary>
        public bool FleetCarrierJumpInProgress => _carrierJumpState?.IsJumpScheduled ?? false;
        
        /// <summary>
        /// Legacy property: Use CarrierJumpState.ScheduledJumpTime instead
        /// </summary>
        public DateTime? FleetCarrierJumpTime => _carrierJumpState?.ScheduledJumpTime;
        
        /// <summary>
        /// Legacy property: Same as FleetCarrierJumpTime
        /// </summary>
        public DateTime? CarrierJumpScheduledTime => _carrierJumpState?.ScheduledJumpTime;
        
        /// <summary>
        /// Legacy property: Indicates if jump has completed (but overlay may still be visible)
        /// </summary>
        public bool JumpArrived => _carrierJumpState?.JumpCompleted ?? false;
        
        /// <summary>
        /// Legacy property: Returns TimeSpan for countdown (nullable)
        /// </summary>
        public TimeSpan? JumpCountdown
        {
            get
            {
                int seconds = _carrierJumpState?.CountdownSeconds ?? 0;
                return seconds > 0 ? TimeSpan.FromSeconds(seconds) : null;
            }
        }
        
        /// <summary>
        /// Legacy property: Shows countdown in UI (when jump is scheduled)
        /// </summary>
        public bool ShowCarrierJumpCountdown => _carrierJumpState?.IsJumpScheduled ?? false;
        
        #endregion

        #region Carrier Jump - Private Methods

        /// <summary>
        /// Initializes timers for carrier jump handling
        /// </summary>
        private void InitializeCarrierJumpTimers()
        {
            // Timer to check when jump should occur
            _carrierJumpTimer = new DispatcherTimer(DispatcherPriority.Normal, System.Windows.Application.Current.Dispatcher)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _carrierJumpTimer.Tick += CarrierJumpTimer_Tick;

            // Timeout timer for overlay (3 minutes max)
            _overlayTimeoutTimer = new DispatcherTimer(DispatcherPriority.Normal, System.Windows.Application.Current.Dispatcher)
            {
                Interval = TimeSpan.FromMinutes(3)
            };
            _overlayTimeoutTimer.Tick += OverlayTimeout_Tick;
        }

        /// <summary>
        /// Handles CarrierJumpRequest event from journal
        /// </summary>
        private void HandleCarrierJumpRequest(DateTime departureTime, string systemName, string bodyName)
        {
            // Ensure time is UTC
            if (departureTime.Kind == DateTimeKind.Local)
                departureTime = departureTime.ToUniversalTime();
            else if (departureTime.Kind == DateTimeKind.Unspecified)
                departureTime = DateTime.SpecifyKind(departureTime, DateTimeKind.Utc);

            // Only schedule if jump is in the future
            if (departureTime <= DateTime.UtcNow)
            {
                Log.Warning("🚀 Ignoring CarrierJumpRequest - scheduled time {Time} is in the past", departureTime);
                return;
            }

            Log.Information("🚀 Carrier jump scheduled: {System} at {Time}", systemName, departureTime);
            
            _carrierJumpState.ScheduleJump(departureTime, systemName, bodyName);
            
            // Start countdown timer
            _carrierJumpTimer.Start();
            
            // Notify UI of state change
            OnPropertyChanged(nameof(CarrierJumpCountdownSeconds));
            OnPropertyChanged(nameof(CarrierJumpDestination));
        }

        /// <summary>
        /// Handles jump cancellation
        /// </summary>
        private void HandleCarrierJumpCancelled()
        {
            Log.Information("🚀 Carrier jump cancelled");
            
            _carrierJumpState.CancelJump();
            _carrierJumpTimer.Stop();
            _overlayTimeoutTimer.Stop();
            
            OnPropertyChanged(nameof(ShowCarrierJumpOverlay));
            OnPropertyChanged(nameof(CarrierJumpCountdownSeconds));
            OnPropertyChanged(nameof(CarrierJumpDestination));
        }

        /// <summary>
        /// Handles jump completion
        /// </summary>
        private void HandleCarrierJumpCompleted()
        {
            Log.Information("🚀 Carrier jump completed");
            
            _carrierJumpState.CompleteJump();
            // Stop countdown timer; reconfigure overlay timeout to auto-hide shortly
            _carrierJumpTimer.Stop();

            // Ensure the overlay will be hidden shortly after completion
            if (_overlayTimeoutTimer != null)
            {
                _overlayTimeoutTimer.Stop();
                _overlayTimeoutTimer.Interval = TimeSpan.FromSeconds(6); // slightly longer than ShowOverlay grace
                _overlayTimeoutTimer.Start();
            }

            // Notify UI now (overlay may still show briefly due to JumpCompleted grace window)
            OnPropertyChanged(nameof(ShowCarrierJumpOverlay));
            OnPropertyChanged(nameof(CarrierJumpCountdownSeconds));
        }

        /// <summary>
        /// Timer tick - checks if it's time to show overlay
        /// </summary>
        private void CarrierJumpTimer_Tick(object sender, EventArgs e)
        {
            if (!_carrierJumpState.IsJumpScheduled)
            {
                _carrierJumpTimer.Stop();
                return;
            }

            // Update countdown
            OnPropertyChanged(nameof(CarrierJumpCountdownSeconds));

            // Check if jump time has arrived
            if (_carrierJumpState.CountdownSeconds <= 0)
            {
                Log.Information("🚀 Jump time reached - checking if player is on carrier");
                
                // CRITICAL: Only show overlay if player is on the carrier
                if (IsOnFleetCarrier)
                {
                    Log.Information("🚀 Player is on carrier - showing overlay");
                    OnPropertyChanged(nameof(ShowCarrierJumpOverlay));
                    
                    // Start timeout timer
                    _overlayTimeoutTimer.Start();
                }
                else
                {
                    Log.Information("🚀 Player not on carrier - resetting jump state and hiding overlay");
                    _carrierJumpState.Reset();
                    OnPropertyChanged(nameof(ShowCarrierJumpOverlay));
                    OnPropertyChanged(nameof(CarrierJumpCountdownSeconds));
                }
                
                // Stop countdown timer
                _carrierJumpTimer.Stop();
            }
        }

        /// <summary>
        /// Overlay timeout - hide overlay if jump hasn't completed after 3 minutes
        /// </summary>
        private void OverlayTimeout_Tick(object sender, EventArgs e)
        {
            Log.Warning("🚀 Carrier jump overlay timeout - hiding overlay");
            
            _overlayTimeoutTimer.Stop();
            _carrierJumpState.Reset();
            
            OnPropertyChanged(nameof(ShowCarrierJumpOverlay));
            OnPropertyChanged(nameof(CarrierJumpCountdownSeconds));
        }
        
        /// <summary>
        /// Called by UI when countdown reaches zero to update overlay state
        /// </summary>
        public void NotifyCarrierJumpCountdownReachedZero()
        {
            Log.Information("🚀 UI notified countdown reached zero");
            // The timer already handles this, but we can force a property refresh
            OnPropertyChanged(nameof(ShowCarrierJumpOverlay));
            OnPropertyChanged(nameof(CarrierJumpCountdownSeconds));
        }

        /// <summary>
        /// Force hide the carrier jump overlay and clear jump state.
        /// Useful for emergency UI actions or recovery if an event was missed.
        /// </summary>
        public void ForceHideCarrierJumpOverlay()
        {
            try
            {
                Log.Information("🚀 ForceHideCarrierJumpOverlay called - resetting state and hiding overlay");
                _carrierJumpTimer?.Stop();
                _overlayTimeoutTimer?.Stop();
                _carrierJumpState?.Reset();
                OnPropertyChanged(nameof(ShowCarrierJumpOverlay));
                OnPropertyChanged(nameof(CarrierJumpCountdownSeconds));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in ForceHideCarrierJumpOverlay");
            }
        }

        /// <summary>
        /// Scans journal on startup for pending carrier jump
        /// </summary>
        private void ScanForPendingCarrierJump()
        {
            if (string.IsNullOrEmpty(latestJournalPath) || !System.IO.File.Exists(latestJournalPath))
                return;

            try
            {
                Log.Information("🚀 Scanning journal for pending carrier jump");
                
                var fileInfo = new System.IO.FileInfo(latestJournalPath);
                using var fs = new System.IO.FileStream(latestJournalPath, System.IO.FileMode.Open, 
                    System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
                
                // Read last 20KB to find recent carrier events
                long startPos = Math.Max(0, fileInfo.Length - 20480);
                fs.Seek(startPos, System.IO.SeekOrigin.Begin);
                
                using var sr = new System.IO.StreamReader(fs);
                
                DateTime? lastJumpRequest = null;
                string lastSystem = null;
                string lastBody = null;
                bool jumpCompleted = false;
                
                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine();
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(line);
                        var root = doc.RootElement;
                        
                        if (!root.TryGetProperty("event", out var eventProp)) continue;
                        
                        string eventType = eventProp.GetString();
                        
                        if (eventType == "CarrierJumpRequest" && 
                            root.TryGetProperty("DepartureTime", out var timeProp) &&
                            DateTime.TryParse(timeProp.GetString(), null, 
                                System.Globalization.DateTimeStyles.RoundtripKind, out var depTime))
                        {
                            lastJumpRequest = depTime.Kind == DateTimeKind.Utc ? depTime : 
                                depTime.Kind == DateTimeKind.Local ? depTime.ToUniversalTime() :
                                DateTime.SpecifyKind(depTime, DateTimeKind.Utc);
                            
                            lastSystem = root.TryGetProperty("SystemName", out var sys) ? sys.GetString() : null;
                            lastBody = root.TryGetProperty("Body", out var body) ? body.GetString() : null;
                            jumpCompleted = false;
                        }
                        else if (eventType == "CarrierJump")
                        {
                            jumpCompleted = true;
                        }
                    }
                    catch (System.Text.Json.JsonException jsonEx)
                    {
                        // Skip malformed lines - journal might have corruption or partial data
                        Log.Debug("🚀 Skipping malformed journal line during carrier jump scan: {Error}", jsonEx.Message);
                        continue;
                    }
                }
                
                // If we found a jump request without completion, and it's in the future, schedule it
                if (lastJumpRequest.HasValue && !jumpCompleted && lastJumpRequest.Value > DateTime.UtcNow)
                {
                    Log.Information("🚀 Found pending carrier jump: {System} at {Time}", lastSystem, lastJumpRequest.Value);
                    HandleCarrierJumpRequest(lastJumpRequest.Value, lastSystem, lastBody);
                }
                else if (lastJumpRequest.HasValue)
                {
                    Log.Information("🚀 Found completed or past carrier jump - ignoring");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "🚀 Error scanning for pending carrier jump");
            }
        }

        #endregion
    }
}
