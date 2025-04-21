using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using EliteInfoPanel.Core;
using Serilog;

namespace EliteInfoPanel.Controls
{
    public partial class HyperspaceOverlay : UserControl
    {
        private GameStateService _gameState;
        private System.Threading.Timer _visibilityTimer;
        private readonly int _maxVisibilityTimeMs = 25000; // Maximum time overlay can stay visible (25 seconds)
        private readonly Random _random = new Random();
        private readonly List<Ellipse> _stars = new List<Ellipse>();
        private Storyboard _starfieldAnimation;

        // Legal state colors
        private readonly Dictionary<string, SolidColorBrush> _legalStateColors = new Dictionary<string, SolidColorBrush>
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

        public HyperspaceOverlay()
        {
            InitializeComponent();

            // Force the overlay to be hidden initially
            RootGrid.Visibility = Visibility.Collapsed;

            // Initialize starfield animation if enabled
            InitializeStarfield();

            Log.Information("🚀 HyperspaceOverlay created - initially hidden");
        }

        private void InitializeStarfield()
        {
            try
            {
                // Only proceed if the canvas exists
                if (StarfieldCanvas == null) return;

                // Create a storyboard for animation
                _starfieldAnimation = new Storyboard();

                // Create stars
                for (int i = 0; i < 150; i++)
                {
                    CreateStar();
                }

                // Start the animation when loaded
                this.Loaded += (s, e) =>
                {
                    _starfieldAnimation.Begin();
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing starfield");
            }
        }

        private void CreateStar()
        {
            try
            {
                // Only proceed if the canvas exists
                if (StarfieldCanvas == null) return;

                // Create a star (small ellipse)
                var star = new Ellipse
                {
                    Width = _random.Next(1, 4),
                    Height = _random.Next(1, 4),
                    Fill = new SolidColorBrush(Colors.White),
                    Opacity = _random.NextDouble() * 0.8 + 0.2 // Between 0.2 and 1.0
                };

                // Position randomly on canvas
                Canvas.SetLeft(star, _random.Next(0, (int)StarfieldCanvas.ActualWidth));
                Canvas.SetTop(star, _random.Next(0, (int)StarfieldCanvas.ActualHeight));

                // Add to canvas and collection
                StarfieldCanvas.Children.Add(star);
                _stars.Add(star);

                // Create animation for this star
                var animation = new DoubleAnimation
                {
                    From = Canvas.GetLeft(star),
                    To = Canvas.GetLeft(star) - 1000, // Move left
                    Duration = TimeSpan.FromSeconds(_random.Next(3, 10)),
                    RepeatBehavior = RepeatBehavior.Forever
                };

                Storyboard.SetTarget(animation, star);
                Storyboard.SetTargetProperty(animation, new PropertyPath("(Canvas.Left)"));

                _starfieldAnimation.Children.Add(animation);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating star");
            }
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
                Log.Information("🚀 HyperspaceOverlay initial state: IsJumping={0}, Dest={1}, Class={2}, LegalState={3}",
                    _gameState.IsHyperspaceJumping,
                    _gameState.HyperspaceDestination ?? "(null)",
                    _gameState.HyperspaceStarClass ?? "(null)",
                    _gameState.LegalState ?? "Clean");

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

            // Stop any animations
            _starfieldAnimation?.Stop();

            // Hide the overlay
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

                    case nameof(GameStateService.LegalState):
                        Log.Information("🚀 LegalState changed to: {0}",
                            _gameState.LegalState ?? "Clean");
                        UpdateLegalStateText(_gameState.LegalState);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in HyperspaceOverlay.GameState_PropertyChanged");
            }
        }

        private void StartVisibilityTimer()
        {
            // Cancel any existing timer
            _visibilityTimer?.Dispose();

            // Create a new timer that will force-hide the overlay after the maximum time
            _visibilityTimer = new System.Threading.Timer((state) =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (RootGrid.Visibility == Visibility.Visible)
                    {
                        Log.Warning("⚠️ Hyperspace overlay safety timer triggered - forcing overlay to hide");
                        RootGrid.Visibility = Visibility.Collapsed;

                        // Stop animations
                        _starfieldAnimation?.Stop();
                    }
                }));
            }, null, _maxVisibilityTimeMs, Timeout.Infinite); // One-time timer
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

                    // Start animations
                    _starfieldAnimation?.Begin();

                    // Start safety timer when showing the overlay
                    StartVisibilityTimer();
                }
                else
                {
                    RootGrid.Visibility = Visibility.Collapsed;
                    Log.Information("🚀 HyperspaceOverlay now HIDDEN");

                    // Stop animations
                    _starfieldAnimation?.Stop();
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
                        // Default to white if state not found
                        LegalStateText.Foreground = new SolidColorBrush(Colors.White);
                    }

                    Log.Information("📝 LegalStateText set to: \"{0}\" with color {1}",
                        text, LegalStateText.Foreground);
                }
                else
                {
                    Log.Warning("⚠️ LegalStateText is null!");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in HyperspaceOverlay.UpdateLegalStateText");
            }
        }
    }
}