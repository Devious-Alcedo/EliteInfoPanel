using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
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
        private readonly List<FrameworkElement> _stars = new List<FrameworkElement>();
        private readonly List<Storyboard> _starAnimations = new List<Storyboard>();
        private readonly int _numStars = 150; // Number of stars to create
        private bool _starfieldInitialized = false;
        private Point _screenCenter;
        private readonly List<StarInfo> _starInfos = new();
        private DrawingVisual _starVisual;
        private DrawingImage _starImage;
        private Image _starImageControl;
        private bool _isRendering = false;
        private WriteableBitmap _bitmap;
        private Image _bitmapImageControl;
        private int _bitmapWidth, _bitmapHeight;
        private byte[] _pixelBuffer;
        private Stopwatch _renderTimer = Stopwatch.StartNew();
        private const double TargetFrameTimeMs = 1000.0 / 60;


        private bool _isUpdatingStars = false;
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
                // Update screen center
                _screenCenter = new Point(e.NewSize.Width / 2, e.NewSize.Height / 2);

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
            _bitmapWidth = (int)(ActualWidth / 1.5);  // 1.5x reduction = ~56% fewer pixels
            _bitmapHeight = (int)(ActualHeight / 1.5);


            if (_bitmapWidth <= 0 || _bitmapHeight <= 0) return;

            _bitmap = new WriteableBitmap(_bitmapWidth, _bitmapHeight, 96, 96, PixelFormats.Bgra32, null);
            _pixelBuffer = new byte[_bitmapWidth * _bitmapHeight * 4];

            _bitmapImageControl = new Image
            {
                Source = _bitmap,
                Stretch = Stretch.Fill,
                Width = ActualWidth,
                Height = ActualHeight
            };

            StarfieldCanvas.Children.Clear();
            StarfieldCanvas.Children.Add(_bitmapImageControl);

            _starInfos.Clear();
            _screenCenter = new Point(_bitmapWidth / 2, _bitmapHeight / 2);

            for (int i = 0; i < _numStars; i++)
            {
                var angle = _random.NextDouble() * Math.PI * 2;
                var speed = 0.5 + _random.NextDouble() * 2.5;
                var radius = 2 + _random.NextDouble() * 2;

                var velocity = new Vector(Math.Cos(angle) * speed, Math.Sin(angle) * speed);
                _starInfos.Add(new StarInfo
                {
                    Position = _screenCenter,
                    Velocity = velocity,
                    Radius = radius,
                    Color = _starColors[_random.Next(_starColors.Length)]
                });
            }

            if (!_isRendering)
            {
                DispatcherTimer _starTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
                };
                _starTimer.Tick += (s, e) => RenderStarsToBitmap(null, null);
                _starTimer.Start();

                _isRendering = true;
            }

            _starfieldInitialized = true;
        }
        private void RenderStarsToBitmap(object sender, EventArgs e)
        {
            if (_renderTimer.Elapsed.TotalMilliseconds < TargetFrameTimeMs) return;
            _renderTimer.Restart();
            // Fade previous frame for motion blur effect
            Span<byte> buffer = _pixelBuffer;

            for (int i = 0; i < buffer.Length; i += 4)
            {
                // Fade only non-zero pixels
                if (buffer[i + 0] > 0 || buffer[i + 1] > 0 || buffer[i + 2] > 0)
                {
                    buffer[i + 0] = (byte)(buffer[i + 0] * 0.92);
                    buffer[i + 1] = (byte)(buffer[i + 1] * 0.92);
                    buffer[i + 2] = (byte)(buffer[i + 2] * 0.92);
                }
                buffer[i + 3] = 255;
            }

            foreach (var star in _starInfos)
            {
                // Calculate acceleration from center (for tunnel effect)
                Vector toCenter = star.Position - _screenCenter;
                double depthFactor = toCenter.Length / (_bitmapWidth / 2); // Range 0–1+

                // Apply position change with perspective speed boost
                star.Position += star.Velocity * (1.0 + depthFactor * 4.0);

                // Reset star if offscreen
                if (star.Position.X < 0 || star.Position.X >= _bitmapWidth ||
                    star.Position.Y < 0 || star.Position.Y >= _bitmapHeight)
                {
                    double angle = _random.NextDouble() * Math.PI * 2;
                    double speed = 0.5 + _random.NextDouble() * 2.5;
                    double radius = 1.0 + _random.NextDouble() * 1.2;
                    var newPos = _screenCenter;

                    star.Position = newPos;
                    star.Velocity = new Vector(Math.Cos(angle) * speed, Math.Sin(angle) * speed);
                    star.Radius = radius;
                    star.Color = _starColors[_random.Next(_starColors.Length)];
                }

                // Draw trail as a fixed-length line
                Point trailStart = star.Position - (star.Velocity * 5);
                DrawLineWithFade((int)trailStart.X, (int)trailStart.Y, (int)star.Position.X, (int)star.Position.Y, star.Color);

                // Glow around the star
                int px = (int)star.Position.X;
                int py = (int)star.Position.Y;

                DrawGlowPixel(px, py, star.Color, 0.9);     // center
                DrawGlowPixel(px + 1, py, star.Color, 0.4); // neighbors
                DrawGlowPixel(px - 1, py, star.Color, 0.4);
                DrawGlowPixel(px, py + 1, star.Color, 0.4);
                DrawGlowPixel(px, py - 1, star.Color, 0.4);
            }

            // Commit pixel data to screen
            _bitmap.WritePixels(new Int32Rect(0, 0, _bitmapWidth, _bitmapHeight), _pixelBuffer, _bitmapWidth * 4, 0);
        }
        private byte Blend(byte background, byte foreground, double alpha)
        {
            return (byte)(background * (1 - alpha) + foreground * alpha);
        }

        private void DrawLineWithFade(int x0, int y0, int x1, int y1, Color color)
        {
            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;
            int length = Math.Max(dx, dy);
            double fadeStep = 1.0 / Math.Max(1, length);
            double alpha = 0.15;

            while (true)
            {
                if (x0 >= 0 && x0 < _bitmapWidth && y0 >= 0 && y0 < _bitmapHeight)
                {
                    int index = (y0 * _bitmapWidth + x0) * 4;

                    _pixelBuffer[index + 0] = Blend(_pixelBuffer[index + 0], color.B, alpha);
                    _pixelBuffer[index + 1] = Blend(_pixelBuffer[index + 1], color.G, alpha);
                    _pixelBuffer[index + 2] = Blend(_pixelBuffer[index + 2], color.R, alpha);
                    _pixelBuffer[index + 3] = 255;
                }

                if (x0 == x1 && y0 == y1)
                    break;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }

                alpha += fadeStep;
                if (alpha > 1.0) alpha = 1.0;
            }
        }
        private void DrawGlowPixel(int x, int y, Color color, double alpha)
        {
            if (x < 0 || x >= _bitmapWidth || y < 0 || y >= _bitmapHeight)
                return;

            Span<byte> buffer = _pixelBuffer;

            int index = (y * _bitmapWidth + x) * 4;
            buffer[index + 0] = Blend(buffer[index + 0], color.B, alpha);
            buffer[index + 1] = Blend(buffer[index + 1], color.G, alpha);
            buffer[index + 2] = Blend(buffer[index + 2], color.R, alpha);
            buffer[index + 3] = 255;
        }


        private void DrawGlowCircle(int centerX, int centerY, int radius, Color color, double opacity)
        {
            int rSquared = radius * radius;
            int minX = Math.Max(centerX - radius, 0);
            int maxX = Math.Min(centerX + radius, _bitmapWidth - 1);
            int minY = Math.Max(centerY - radius, 0);
            int maxY = Math.Min(centerY + radius, _bitmapHeight - 1);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int dx = x - centerX;
                    int dy = y - centerY;
                    int distanceSquared = dx * dx + dy * dy;

                    if (distanceSquared <= rSquared)
                    {
                        double falloff = 1.0 - (distanceSquared / (double)rSquared);
                        double finalOpacity = opacity * falloff;

                        int index = (y * _bitmapWidth + x) * 4;

                        byte r = (byte)(color.R * finalOpacity);
                        byte g = (byte)(color.G * finalOpacity);
                        byte b = (byte)(color.B * finalOpacity);

                        _pixelBuffer[index + 0] = b;
                        _pixelBuffer[index + 1] = g;
                        _pixelBuffer[index + 2] = r;
                        _pixelBuffer[index + 3] = 255;
                    }
                }
            }
        }



        private void RenderStars(object sender, EventArgs e)
        {
            using var dc = _starVisual.RenderOpen();
            foreach (var star in _starInfos)
            {
                // Update position
                star.Position += star.Velocity;

                // Recycle if offscreen
                if (star.Position.X < 0 || star.Position.X > ActualWidth || star.Position.Y < 0 || star.Position.Y > ActualHeight)
                {
                    double angle = _random.NextDouble() * Math.PI * 2;
                    double speed = 0.5 + _random.NextDouble() * 2.5;
                    double radius = 3 + _random.NextDouble() * 2;

                    star.Position = _screenCenter;
                    star.Velocity = new Vector(Math.Cos(angle) * speed, Math.Sin(angle) * speed);
                    star.Radius = radius;
                    star.Color = _starColors[_random.Next(_starColors.Length)];
                }

                // Draw star
                var brush = new SolidColorBrush(star.Color) { Opacity = 0.85 };
                dc.DrawEllipse(brush, null, star.Position, star.Radius, star.Radius);
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

            _visibilityTimer?.Dispose();
            _visibilityTimer = null;

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(ForceHidden));
                return;
            }

            RootGrid.Visibility = Visibility.Collapsed;

            if (_isRendering)
            {
             
                _isRendering = false;
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

                        // Stop all star animations
                        foreach (var anim in _starAnimations)
                        {
                            anim.Stop();
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

                    // Stop all star animations
                    foreach (var anim in _starAnimations)
                    {
                        anim.Stop();
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
    public class StarInfo
    {
        public Point Position;
        public Point PreviousPosition;
        public Vector Velocity;
        public double Radius;
        public Color Color;
    }

    public class VisualHost : FrameworkElement
    {
        private readonly VisualCollection _children;

        public VisualHost(Visual visual)
        {
            _children = new VisualCollection(this) { visual };
        }

        protected override int VisualChildrenCount => _children.Count;

        protected override Visual GetVisualChild(int index) => _children[index];
    }


}