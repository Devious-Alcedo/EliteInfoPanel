using System;
using Serilog;

namespace EliteInfoPanel.Core.Models
{
    /// <summary>
    /// Encapsulates the state of a Fleet Carrier jump operation
    /// </summary>
    public class CarrierJumpState
    {
        private DateTime? _scheduledJumpTime;
        private string _destinationSystem;
        private string _destinationBody;
        private bool _jumpCompleted;
        private DateTime _stateLastUpdated;
        private bool _overlayActive;

        public DateTime? ScheduledJumpTime
        {
            get => _scheduledJumpTime;
            private set
            {
                _scheduledJumpTime = value;
                _stateLastUpdated = DateTime.UtcNow;
            }
        }

        public string DestinationSystem
        {
            get => _destinationSystem;
            private set
            {
                _destinationSystem = value;
                _stateLastUpdated = DateTime.UtcNow;
            }
        }

        public string DestinationBody
        {
            get => _destinationBody;
            private set
            {
                _destinationBody = value;
                _stateLastUpdated = DateTime.UtcNow;
            }
        }

        public bool JumpCompleted
        {
            get => _jumpCompleted;
            private set
            {
                _jumpCompleted = value;
                _stateLastUpdated = DateTime.UtcNow;
            }
        }

        public bool IsJumpScheduled => ScheduledJumpTime.HasValue && !JumpCompleted;

        public int CountdownSeconds
        {
            get
            {
                if (!ScheduledJumpTime.HasValue || JumpCompleted)
                    return 0;

                var remaining = (ScheduledJumpTime.Value - DateTime.UtcNow).TotalSeconds;
                return Math.Max(0, (int)remaining);
            }
        }

        /// <summary>
        /// Determines if the carrier jump overlay should be shown.
        /// This is explicitly activated at T0 when the player is on the carrier,
        /// and deactivated on completion/cancel/timeout.
        /// </summary>
        public bool ShouldShowOverlay => _overlayActive;

        public void ActivateOverlay()
        {
            _overlayActive = true;
            _stateLastUpdated = DateTime.UtcNow;
        }

        public void DeactivateOverlay()
        {
            _overlayActive = false;
            _stateLastUpdated = DateTime.UtcNow;
        }

        /// <summary>
        /// Schedules a new carrier jump
        /// </summary>
        public void ScheduleJump(DateTime scheduledTime, string systemName, string bodyName)
        {
            Log.Information("🚀 Scheduling carrier jump to {System} at {Time}", systemName, scheduledTime);
            
            ScheduledJumpTime = scheduledTime;
            DestinationSystem = systemName;
            DestinationBody = bodyName;
            JumpCompleted = false;
            DeactivateOverlay();
        }

        /// <summary>
        /// Marks the jump as completed
        /// </summary>
        public void CompleteJump()
        {
            Log.Information("🚀 Carrier jump completed");
            
            JumpCompleted = true;
            DeactivateOverlay();
        }

        /// <summary>
        /// Cancels a scheduled jump
        /// </summary>
        public void CancelJump()
        {
            Log.Information("🚀 Carrier jump cancelled");
            
            Reset();
        }

        /// <summary>
        /// Resets all jump state
        /// </summary>
        public void Reset()
        {
            Log.Debug("🚀 Resetting carrier jump state");
            
            ScheduledJumpTime = null;
            DestinationSystem = null;
            DestinationBody = null;
            JumpCompleted = false;
            DeactivateOverlay();
        }

        /// <summary>
        /// Cleans up stale jump state (e.g., jump time passed but no completion event received)
        /// </summary>
        public void CleanupStaleState()
        {
            if (!ScheduledJumpTime.HasValue)
                return;

            // If the scheduled jump time is more than 5 minutes in the past and not completed,
            // assume we missed the jump completion event and reset
            var timeSinceScheduled = DateTime.UtcNow - ScheduledJumpTime.Value;
            if (timeSinceScheduled.TotalMinutes > 5 && !JumpCompleted)
            {
                Log.Warning("🚀 Cleaning up stale carrier jump state - scheduled jump time passed over 5 minutes ago");
                Reset();
            }

            // If overlay somehow remained active after completion for too long, force deactivate
            if (JumpCompleted && _overlayActive)
            {
                Log.Debug("🚀 Forcing overlay deactivation after completion cleanup");
                DeactivateOverlay();
            }
        }

        public override string ToString()
        {
            return $"CarrierJumpState[Scheduled={ScheduledJumpTime}, Destination={DestinationSystem}, Body={DestinationBody}, Completed={JumpCompleted}, ShowOverlay={ShouldShowOverlay}]";
        }
    }
}
