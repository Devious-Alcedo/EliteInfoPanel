using System;
using System.Windows.Threading;
using EliteInfoPanel.Core.Models;

namespace EliteInfoPanel.Core.Services
{
    internal sealed class CarrierJumpManager : ICarrierJumpManager
    {
        private readonly Dispatcher _dispatcher;
        private readonly Func<bool> _isOnFleetCarrier;
        private readonly Action _onStateChanged;

        private readonly CarrierJumpState _state = new();
        private readonly DispatcherTimer _countdownTimer;
        private readonly DispatcherTimer _overlayTimeoutTimer;

        public event EventHandler<CarrierJumpScheduledEventArgs>? CarrierJumpScheduled;
        public event EventHandler? CarrierJumpCompleted;

        public CarrierJumpManager(Dispatcher dispatcher, Func<bool> isOnFleetCarrier, Action onStateChanged)
        {
            _dispatcher = dispatcher;
            _isOnFleetCarrier = isOnFleetCarrier;
            _onStateChanged = onStateChanged ?? (() => { });

            _countdownTimer = new DispatcherTimer(DispatcherPriority.Normal, _dispatcher)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _countdownTimer.Tick += (_, __) => OnCountdownTick();

            _overlayTimeoutTimer = new DispatcherTimer(DispatcherPriority.Normal, _dispatcher)
            {
                Interval = TimeSpan.FromMinutes(3)
            };
            _overlayTimeoutTimer.Tick += (_, __) => OnOverlayTimeout();
        }

        public bool ShouldShowOverlay => _state.ShouldShowOverlay;
        public string DestinationSystem => _state.DestinationSystem;
        public string DestinationBody => _state.DestinationBody;
        public int CountdownSeconds => _state.CountdownSeconds;
        public bool IsJumpScheduled => _state.IsJumpScheduled;
        public DateTime? ScheduledJumpTime => _state.ScheduledJumpTime;
        public bool JumpCompleted => _state.JumpCompleted;

        public void ScheduleJump(DateTime departureTimeUtc, string systemName, string bodyName)
        {
            _state.ScheduleJump(departureTimeUtc, systemName, bodyName);
            _countdownTimer.Start();
            _onStateChanged();
            CarrierJumpScheduled?.Invoke(this, new CarrierJumpScheduledEventArgs(departureTimeUtc, systemName, bodyName));
        }

        public void CancelJump()
        {
            _state.CancelJump();
            _countdownTimer.Stop();
            _overlayTimeoutTimer.Stop();
            _onStateChanged();
        }

        public void CompleteJump()
        {
            _state.CompleteJump();
            _countdownTimer.Stop();
            _overlayTimeoutTimer.Stop();
            _onStateChanged();
            CarrierJumpCompleted?.Invoke(this, EventArgs.Empty);
        }

        public void ActivateOverlay()
        {
            _state.ActivateOverlay();
            _overlayTimeoutTimer.Start();
            _onStateChanged();
        }

        public void Reset()
        {
            _state.Reset();
            _countdownTimer.Stop();
            _overlayTimeoutTimer.Stop();
            _onStateChanged();
        }

        private void OnCountdownTick()
        {
            // notify countdown progress
            _onStateChanged();

            if (!_state.IsJumpScheduled)
            {
                _countdownTimer.Stop();
                return;
            }

            if (_state.CountdownSeconds <= 0)
            {
                if (_isOnFleetCarrier())
                {
                    ActivateOverlay();
                }
                else
                {
                    Reset();
                }
                _countdownTimer.Stop();
            }
        }

        private void OnOverlayTimeout()
        {
            _overlayTimeoutTimer.Stop();
            _state.Reset();
            _onStateChanged();
        }
    }
}
