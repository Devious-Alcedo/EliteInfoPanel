// CARRIER JUMP SIMPLIFIED IMPLEMENTATION
// This file contains the clean carrier jump logic to replace the messy implementation

using System;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using System.Linq;
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

            // Timeout timer for overlay (safety auto-hide)
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

            // Notify UI of countdown state change only (overlay remains off until T0 and player is on carrier)
            OnPropertyChanged(nameof(CarrierJumpCountdownSeconds));
            OnPropertyChanged(nameof(CarrierJumpDestination));
            OnPropertyChanged(nameof(ShowCarrierJumpOverlay));
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
            // Stop countdown timer and overlay safety timer; overlay will be hidden immediately
            _carrierJumpTimer.Stop();
            _overlayTimeoutTimer.Stop();

            // Notify UI now to hide overlay and clear countdown
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

            // Update countdown (UI progress)
            OnPropertyChanged(nameof(CarrierJumpCountdownSeconds));

            // Check if jump time has arrived
            if (_carrierJumpState.CountdownSeconds <= 0)
            {
                Log.Information("🚀 Jump time reached - checking if player is on carrier");
                
                // Only show overlay if player is on the carrier at T0
                if (IsOnFleetCarrier)
                {
                    Log.Information("🚀 Player is on carrier at T0 - activating overlay and waiting for CarrierJump event");
                    _carrierJumpState.ActivateOverlay();
                    OnPropertyChanged(nameof(ShowCarrierJumpOverlay));
                    
                    // Start safety timeout in case CarrierJump event is missed
                    _overlayTimeoutTimer.Start();
                }
                else
                {
                    Log.Information("🚀 Player not on carrier at T0 - do not show overlay, reset jump state");
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
            try
            {
                var journalFiles = System.IO.Directory.GetFiles(gamePath, "Journal.*.log")
                    .OrderBy(f => System.IO.File.GetLastWriteTime(f)) // chronological
                    .ToList();

                if (journalFiles.Count == 0)
                {
                    Log.Warning("🚀 No journal files found when scanning for pending carrier jump");
                    return;
                }

                Log.Information("🚀 Scanning {Count} journal files for pending carrier jump", journalFiles.Count);

                DateTime? latestRequestTimestamp = null; // event timestamp
                DateTime? latestDepartureTimeUtc = null; // scheduled departure
                string latestSystem = null;
                string latestBody = null;
                bool cancelledOrCompleted = false;

                foreach (var path in journalFiles)
                {
                    using var sr = new System.IO.StreamReader(new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite));
                    while (!sr.EndOfStream)
                    {
                        var line = sr.ReadLine();
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(line);
                            var root = doc.RootElement;
                            if (!root.TryGetProperty("event", out var eventProp)) continue;
                            var et = eventProp.GetString();

                            if (et == "CarrierJumpRequest")
                            {
                                // Record the request time and future departure
                                if (root.TryGetProperty("timestamp", out var tsProp) &&
                                    System.DateTime.TryParse(tsProp.GetString(), out var ts))
                                {
                                    latestRequestTimestamp = ts.Kind == DateTimeKind.Utc ? ts : ts.Kind == DateTimeKind.Local ? ts.ToUniversalTime() : DateTime.SpecifyKind(ts, DateTimeKind.Utc);
                                }
                                if (root.TryGetProperty("DepartureTime", out var dtProp) &&
                                    System.DateTime.TryParse(dtProp.GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                                {
                                    latestDepartureTimeUtc = dt.Kind == DateTimeKind.Utc ? dt : dt.Kind == DateTimeKind.Local ? dt.ToUniversalTime() : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                                }
                                latestSystem = root.TryGetProperty("SystemName", out var sys) ? sys.GetString() : null;
                                latestBody = root.TryGetProperty("Body", out var body) ? body.GetString() : null;
                                cancelledOrCompleted = false; // reset on new request
                            }
                            else if ((et == "CarrierJump" || et == "CarrierJumpCancelled" || et == "CarrierCancelJump") && latestRequestTimestamp.HasValue)
                            {
                                // Only mark complete/cancel if after the last request
                                if (root.TryGetProperty("timestamp", out var tsProp) && System.DateTime.TryParse(tsProp.GetString(), out var evtTs))
                                {
                                    var evtTsUtc = evtTs.Kind == DateTimeKind.Utc ? evtTs : evtTs.Kind == DateTimeKind.Local ? evtTs.ToUniversalTime() : DateTime.SpecifyKind(evtTs, DateTimeKind.Utc);
                                    if (evtTsUtc > latestRequestTimestamp.Value)
                                    {
                                        cancelledOrCompleted = true;
                                    }
                                }
                            }
                        }
                        catch (System.Text.Json.JsonException)
                        {
                            // skip malformed lines
                        }
                    }
                }

                // Decide what to do based on the latest request and its status
                if (latestRequestTimestamp.HasValue && latestDepartureTimeUtc.HasValue && !cancelledOrCompleted)
                {
                    if (latestDepartureTimeUtc.Value > DateTime.UtcNow)
                    {
                        Log.Information("🚀 Pending carrier jump detected at startup: {System} departs {Time}", latestSystem, latestDepartureTimeUtc);
                        HandleCarrierJumpRequest(latestDepartureTimeUtc.Value, latestSystem, latestBody);
                    }
                    else
                    {
                        // T0 already reached but no completion/cancel recorded yet
                        Log.Information("🚀 Carrier jump T0 already reached without completion in logs");
                        if (IsOnFleetCarrier)
                        {
                            _carrierJumpState.ScheduleJump(latestDepartureTimeUtc.Value, latestSystem, latestBody);
                            _carrierJumpState.ActivateOverlay();
                            OnPropertyChanged(nameof(ShowCarrierJumpOverlay));
                            _overlayTimeoutTimer.Start(); // safety timeout
                        }
                        else
                        {
                            _carrierJumpState.Reset();
                        }
                    }
                }
                else
                {
                    Log.Information("🚀 No pending carrier jump found at startup");
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
