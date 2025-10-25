// HyperspaceOverlay with video playback for ship FSD jumps
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EliteInfoPanel.Core;
using Serilog;

namespace EliteInfoPanel.Controls
{
    public partial class HyperspaceOverlay : UserControl
    {
        #region Private Fields

        private GameStateService _gameState;

        // Legal state colors - cached for performance
        private readonly System.Collections.Generic.Dictionary<string, SolidColorBrush> _legalStateColors = new System.Collections.Generic.Dictionary<string, SolidColorBrush>
        {
            { "Clean", new SolidColorBrush(Colors.LightGreen) },
            { "IllegalCargo", new SolidColorBrush(Colors.Orange) },
            { "Speeding", new SolidColorBrush(Colors.Yellow) },
            { "Wanted", new SolidColorBrush(Colors.Red) },
            { "Hostile", new SolidColorBrush(Colors.Red) },
            { "PassengerWanted", new SolidColorBrush(Colors.Orange) },
            { "Warrant", new SolidColorBrush(Colors.OrangeRed) },
            { "Allied", new SolidColorBrush(Colors.LightBlue) },
            { "Thargoid", new SolidColorBrush(Colors.Purple) }
        };

        #endregion Private Fields

        #region Constructor

        public HyperspaceOverlay()
        {
            InitializeComponent();

            // Force the overlay to be hidden initially
            RootGrid.Visibility = Visibility.Collapsed;

            // Setup event handlers
            this.Loaded += HyperspaceOverlay_Loaded;
            this.Unloaded += HyperspaceOverlay_Unloaded;

            Log.Information("🚀 HyperspaceOverlay created - initially hidden (video mode)");
        }

        #endregion Constructor

        #region Event Handlers

        private void HyperspaceOverlay_Loaded(object sender, RoutedEventArgs e)
        {
            Log.Debug("HyperspaceOverlay loaded");

            // Set video source using absolute path
            try
            {
                var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                var videoPath = System.IO.Path.Combine(appDirectory, "Assets", "jump2.mp4");

                if (System.IO.File.Exists(videoPath))
                {
                    JumpVideoPlayer.Source = new Uri(videoPath, UriKind.Absolute);
                    Log.Information("🎬 Video source set to: {VideoPath}", videoPath);
                }
                else
                {
                    Log.Error("🎬 Video file not found at: {VideoPath}", videoPath);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error setting video source");
            }
        }

        private void HyperspaceOverlay_Unloaded(object sender, RoutedEventArgs e)
        {
            // Stop video when unloaded
            if (JumpVideoPlayer != null)
            {
                JumpVideoPlayer.Stop();
            }
        }

        private void JumpVideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            // Loop the video when it ends
            if (JumpVideoPlayer != null && RootGrid.Visibility == Visibility.Visible)
            {
                JumpVideoPlayer.Position = TimeSpan.Zero;
                JumpVideoPlayer.Play();
                Log.Debug("🎬 Video looped");
            }
        }

        private void JumpVideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            Log.Information("🎬 Video opened successfully - Duration: {Duration}", JumpVideoPlayer.NaturalDuration);
        }

        private void JumpVideoPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            Log.Error("🎬 Video failed to load: {Error}", e.ErrorException?.Message ?? "Unknown error");
        }

        #endregion Event Handlers

        #region Video Control

        private void StartVideo()
        {
            try
            {
                if (JumpVideoPlayer != null)
                {
                    if (JumpVideoPlayer.Source != null)
                    {
                        JumpVideoPlayer.Position = TimeSpan.Zero;
                        JumpVideoPlayer.Play();
                        Log.Information("🎬 Jump video playback started - Source: {Source}", JumpVideoPlayer.Source);
                    }
                    else
                    {
                        Log.Warning("🎬 Cannot start video - Source is null");
                    }
                }
                else
                {
                    Log.Warning("🎬 Cannot start video - JumpVideoPlayer is null");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error starting jump video playback");
            }
        }

        private void StopVideo()
        {
            try
            {
                if (JumpVideoPlayer != null)
                {
                    JumpVideoPlayer.Stop();
                    Log.Information("🎬 Jump video playback stopped");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error stopping jump video playback");
            }
        }

        #endregion Video Control

        #region Game State Management

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

                Log.Information("🔌 Connecting HyperspaceOverlay to GameState");
                _gameState.PropertyChanged += GameState_PropertyChanged;

                // Force initial visibility state
                UpdateVisibility();

                // Set initial text values
                UpdateJumpText(_gameState.HyperspaceDestination);
                UpdateStarClassText(_gameState.HyperspaceStarClass);
                UpdateLegalStateText(_gameState.LegalState);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in HyperspaceOverlay.SetGameState");
            }
        }

        public void ForceHidden()
        {
            Log.Information("🚀 HyperspaceOverlay forced to hidden state");

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(ForceHidden));
                return;
            }

            RootGrid.Visibility = Visibility.Collapsed;
            StopVideo();
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

                switch (e.PropertyName)
                {
                    case nameof(GameStateService.IsHyperspaceJumping):
                        Log.Information("🚀 IsHyperspaceJumping changed to: {0}", _gameState.IsHyperspaceJumping);
                        UpdateVisibility();
                        break;

                    case nameof(GameStateService.HyperspaceDestination):
                        UpdateJumpText(_gameState.HyperspaceDestination);
                        break;

                    case nameof(GameStateService.HyperspaceStarClass):
                        UpdateStarClassText(_gameState.HyperspaceStarClass);
                        break;

                    case nameof(GameStateService.LegalState):
                        UpdateLegalStateText(_gameState.LegalState);
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
                if (_gameState?.IsHyperspaceJumping == true)
                {
                    RootGrid.Visibility = Visibility.Visible;
                    StartVideo();
                    Log.Information("🚀 HyperspaceOverlay now VISIBLE");

                    // Ensure progress bar is animating
                    if (JumpProgressBar != null)
                    {
                        JumpProgressBar.IsIndeterminate = true;
                    }
                }
                else
                {
                    RootGrid.Visibility = Visibility.Collapsed;
                    StopVideo();
                    Log.Information("🚀 HyperspaceOverlay now HIDDEN");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in HyperspaceOverlay.UpdateVisibility");
            }
        }

        #endregion Game State Management

        #region UI Updates

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
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in HyperspaceOverlay.UpdateStarClassText");
            }
        }

        private void UpdateLegalStateText(string legalState)
        {
            try
            {
                if (LegalStateText != null)
                {
                    string state = string.IsNullOrEmpty(legalState) ? "Clean" : legalState;
                    string text = $"Legal Status: {state}";

                    LegalStateText.Text = text;

                    // Set appropriate color based on legal state
                    if (_legalStateColors.TryGetValue(state, out var brush))
                    {
                        LegalStateText.Foreground = brush;
                    }
                    else
                    {
                        LegalStateText.Foreground = new SolidColorBrush(Colors.White);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in HyperspaceOverlay.UpdateLegalStateText");
            }
        }

        #endregion UI Updates
    }
}