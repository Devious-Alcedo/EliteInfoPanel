using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using EliteInfoPanel.Core;
using Serilog;

namespace EliteInfoPanel.Controls
{
    public partial class HyperspaceOverlay : UserControl
    {
        private GameStateService _gameState;

        public HyperspaceOverlay()
        {
            InitializeComponent();

            // Force the overlay to be hidden initially
            RootGrid.Visibility = Visibility.Collapsed;

            Log.Information("🚀 HyperspaceOverlay created - initially hidden");
        }

        public void SetGameState(GameStateService gameState)
        {
            try
            {
                if (_gameState != null)
                {
                    Log.Information("🔌 Disconnecting HyperspaceOverlay from previous GameState");
                    _gameState.PropertyChanged -= GameState_PropertyChanged;
                }

                _gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));

                // Subscribe to property changes
                Log.Information("🔌 Connecting HyperspaceOverlay to GameState");
                _gameState.PropertyChanged += GameState_PropertyChanged;

                // Set initial state
                Log.Information("🚀 HyperspaceOverlay initial state: IsJumping={0}, Dest={1}, Class={2}",
                    _gameState.IsHyperspaceJumping,
                    _gameState.HyperspaceDestination ?? "(null)",
                    _gameState.HyperspaceStarClass ?? "(null)");

                // Force initial visibility state
                UpdateVisibility();

                // Set initial text values
                UpdateJumpText(_gameState.HyperspaceDestination);
                UpdateStarClassText(_gameState.HyperspaceStarClass);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in HyperspaceOverlay.SetGameState");
            }
        }

        public void ForceHidden()
        {
            Log.Information("🚀 HyperspaceOverlay forced to hidden state");
            RootGrid.Visibility = Visibility.Collapsed;
        }

        private void GameState_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            try
            {
                if (!Dispatcher.CheckAccess())
                {
                    Dispatcher.BeginInvoke(new Action(() => GameState_PropertyChanged(sender, e)));
                    return;
                }

                Log.Debug("🚀 HyperspaceOverlay received property change: {0}", e.PropertyName);

                switch (e.PropertyName)
                {
                    case nameof(GameStateService.IsHyperspaceJumping):
                        Log.Information("🚀 IsHyperspaceJumping changed to: {0}", _gameState.IsHyperspaceJumping);
                        UpdateVisibility();
                        break;

                    case nameof(GameStateService.HyperspaceDestination):
                        Log.Information("🚀 HyperspaceDestination changed to: {0}",
                            _gameState.HyperspaceDestination ?? "(null)");
                        UpdateJumpText(_gameState.HyperspaceDestination);
                        break;

                    case nameof(GameStateService.HyperspaceStarClass):
                        Log.Information("🚀 HyperspaceStarClass changed to: {0}",
                            _gameState.HyperspaceStarClass ?? "(null)");
                        UpdateStarClassText(_gameState.HyperspaceStarClass);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in HyperspaceOverlay.GameState_PropertyChanged");
            }
        }

        private void UpdateVisibility()
        {
            try
            {
                // Use direct Visibility property setting instead of bindings
                if (_gameState?.IsHyperspaceJumping == true)
                {
                    RootGrid.Visibility = Visibility.Visible;
                    Log.Information("🚀 HyperspaceOverlay now VISIBLE");
                }
                else
                {
                    RootGrid.Visibility = Visibility.Collapsed;
                    Log.Information("🚀 HyperspaceOverlay now HIDDEN");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in HyperspaceOverlay.UpdateVisibility");
            }
        }

        private void UpdateJumpText(string destination)
        {
            try
            {
                if (JumpDestinationText != null)
                {
                    string text = string.IsNullOrEmpty(destination)
                        ? "Hyperspace Jump in Progress..."
                        : $"Jumping to {destination}...";

                    JumpDestinationText.Text = text;
                    Log.Information("📝 JumpDestinationText set to: \"{0}\"", text);
                }
                else
                {
                    Log.Warning("⚠️ JumpDestinationText is null!");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in HyperspaceOverlay.UpdateJumpText");
            }
        }

        private void UpdateStarClassText(string starClass)
        {
            try
            {
                if (StarClassText != null)
                {
                    string text = string.IsNullOrEmpty(starClass)
                        ? "Star Class: Unknown"
                        : $"Star Class: {starClass}";

                    StarClassText.Text = text;
                    Log.Information("📝 StarClassText set to: \"{0}\"", text);
                }
                else
                {
                    Log.Warning("⚠️ StarClassText is null!");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in HyperspaceOverlay.UpdateStarClassText");
            }
        }
    }
}