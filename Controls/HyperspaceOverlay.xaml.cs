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
        private readonly List<Storyboard> _starAnimations = new List<Storyboard>();
        private readonly int _numStars = 200; // Number of stars to create
        private bool _starfieldInitialized = false;

        // Star color options - blueish-white colors for a realistic space look
        private readonly Color[] _starColors = new[]
        {
            Color.FromRgb(255, 255, 255),    // Pure white
            Color.FromRgb(230, 240, 255),    // Light blue-white
            Color.FromRgb(220, 225, 255),    // Pale blue
            Color.FromRgb(200, 200, 255),    // Light lavender
            Color.FromRgb(255, 240, 220),    // Warm white (for some contrast)
            Color.FromRgb(220, 220, 255),    // Pale purple
        };

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

            // Setup event handlers
            this.Loaded += HyperspaceOverlay_Loaded;
            this.SizeChanged += HyperspaceOverlay_SizeChanged;

            Log.Information("🚀 HyperspaceOverlay created - initially hidden");
        }

        private void HyperspaceOverlay_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Initialize starfield if not already done
                if (!_starfieldInitialized)
                {
                    InitializeStarfield();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in HyperspaceOverlay_Loaded");
            }
        }

        private void HyperspaceOverlay_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                // Re-initialize the starfield when the size changes
                if (RootGrid.Visibility == Visibility.Visible && e.NewSize.Width > 0 && e.NewSize.Height > 0)
                {
                    ClearStarfield();
                    InitializeStarfield();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in HyperspaceOverlay_SizeChanged");
            }
        }

        private void ClearStarfield()
        {
            try
            {
                // Stop all animations
                foreach (var anim in _starAnimations)
                {
                    anim.Stop();
                }
                _starAnimations.Clear();

                // Clear all stars
                StarfieldCanvas.Children.Clear();
                _stars.Clear();

                _starfieldInitialized = false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error clearing starfield");
            }
        }

        private void InitializeStarfield()
        {
            try
            {
                // Only proceed if the canvas exists and has size
                if (StarfieldCanvas == null || ActualWidth <= 0 || ActualHeight <= 0)
                {
                    Log.Debug("Skipping starfield initialization - canvas not ready");
                    return;
                }

                double screenWidth = ActualWidth;
                double screenHeight = ActualHeight;

                // Clear any existing stars first
                StarfieldCanvas.Children.Clear();
                _stars.Clear();
                _starAnimations.Clear();

                // Create stars - different sizes and speeds
                for (int i = 0; i < _numStars; i++)
                {
                    CreateStar(screenWidth, screenHeight);
                }

                _starfieldInitialized = true;
                Log.Debug("✨ Starfield initialized with {0} stars", _numStars);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing starfield");
            }
        }

        private void CreateStar(double screenWidth, double screenHeight)
        {
            try
            {
                // Only proceed if the canvas exists
                if (StarfieldCanvas == null) return;

                // Determine star properties
                double starSize = _random.NextDouble() * 2.5 + 0.5;  // Size between 0.5 and 3.0 pixels
                double initialOpacity = _random.NextDouble() * 0.7 + 0.3;  // Opacity between 0.3 and 1.0

                // Choose a random color from our star colors array
                Color starColor = _starColors[_random.Next(_starColors.Length)];

                // Create a star (small ellipse)
                var star = new Ellipse
                {
                    Width = starSize,
                    Height = starSize,
                    Fill = new SolidColorBrush(starColor),
                    Opacity = initialOpacity
                };

                // Position randomly on canvas
                Canvas.SetLeft(star, _random.NextDouble() * screenWidth * 1.2); // Some stars start off-screen
                Canvas.SetTop(star, _random.NextDouble() * screenHeight);

                // Add to canvas and collection
                StarfieldCanvas.Children.Add(star);
                _stars.Add(star);

                // Create animation storyboard for this star
                var storyboard = new Storyboard();

                // Create horizontal movement animation (stars move left)
                var moveAnimation = new DoubleAnimation
                {
                    From = Canvas.GetLeft(star),
                    To = -30, // Move left off screen
                    Duration = TimeSpan.FromSeconds(2 + (_random.NextDouble() * 5)),
                    FillBehavior = FillBehavior.Stop
                };

                Storyboard.SetTarget(moveAnimation, star);
                Storyboard.SetTargetProperty(moveAnimation, new PropertyPath("(Canvas.Left)"));
                storyboard.Children.Add(moveAnimation);

                // Optional: Add a slight twinkle animation for some stars
                if (_random.NextDouble() > 0.7) // 30% of stars
                {
                    var opacityAnimation = new DoubleAnimation
                    {
                        From = initialOpacity,
                        To = initialOpacity * 0.5, // Fade to half brightness
                        Duration = TimeSpan.FromSeconds(0.5 + (_random.NextDouble() * 1.0)),
                        AutoReverse = true,
                        RepeatBehavior = RepeatBehavior.Forever
                    };

                    Storyboard.SetTarget(opacityAnimation, star);
                    Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("Opacity"));
                    storyboard.Children.Add(opacityAnimation);
                }

                // Handle completion of the animation (star moves off-screen)
                storyboard.Completed += (s, e) =>
                {
                    // Reset star to right side of screen
                    Canvas.SetLeft(star, screenWidth + 10);
                    Canvas.SetTop(star, _random.NextDouble() * screenHeight);

                    // Restart animation after a small delay
                    storyboard.BeginTime = TimeSpan.FromMilliseconds(_random.Next(0, 300));
                    storyboard.Begin();
                };

                // Add to our list of animations
                _starAnimations.Add(storyboard);

                // Start with a random delay to stagger star movement
                storyboard.BeginTime = TimeSpan.FromMilliseconds(_random.Next(0, 1500));
                storyboard.Begin();
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

            // Cancel visibility timer if active
            _visibilityTimer?.Dispose();
            _visibilityTimer = null;

            // Ensure we're on the UI thread
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(ForceHidden));
                return;
            }

            // Hide the overlay
            RootGrid.Visibility = Visibility.Collapsed;

            // Pause all star animations to save resources
            foreach (var anim in _starAnimations)
            {
                anim.Pause();
            }
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

                        // Pause all star animations to save resources
                        foreach (var anim in _starAnimations)
                        {
                            anim.Pause();
                        }
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
                    // Make sure starfield is initialized
                    if (!_starfieldInitialized)
                    {
                        InitializeStarfield();
                    }

                    RootGrid.Visibility = Visibility.Visible;
                    Log.Information("🚀 HyperspaceOverlay now VISIBLE");

                    // Resume all star animations
                    foreach (var anim in _starAnimations)
                    {
                        if (anim.GetCurrentState() != ClockState.Active)
                        {
                            anim.Begin();
                        }
                    }

                    // Ensure progress bar is animating
                    if (JumpProgressBar != null)
                    {
                        JumpProgressBar.IsIndeterminate = true;
                    }

                    // Start safety timer when showing the overlay
                    StartVisibilityTimer();
                }
                else
                {
                    RootGrid.Visibility = Visibility.Collapsed;
                    Log.Information("🚀 HyperspaceOverlay now HIDDEN");

                    // Pause all star animations to save resources
                    foreach (var anim in _starAnimations)
                    {
                        anim.Pause();
                    }
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