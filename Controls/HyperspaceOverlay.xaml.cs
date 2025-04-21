using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using EliteInfoPanel.Core;
using Serilog;

namespace EliteInfoPanel.Controls
{
    public partial class HyperspaceOverlay : UserControl
    {
        // P/Invoke for high-performance timer
        [DllImport("kernel32.dll")]
        private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

        [DllImport("kernel32.dll")]
        private static extern bool QueryPerformanceFrequency(out long lpFrequency);

        // Force rendering mode to be software
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        private GameStateService _gameState;

        private readonly Random _random = new Random();
        private readonly int _numStars = 200; // Number of stars
        private bool _starfieldInitialized = false;
        private Point _screenCenter;
        private readonly List<StarInfo> _starInfos = new();
        private WriteableBitmap _bitmap;
        private Image _bitmapImageControl;
        private int _bitmapWidth, _bitmapHeight;
        private byte[] _pixelBuffer;
        private int _pixelBufferStride;
        private bool _isRendering = false;
        private Thread _renderThread;
        private bool _stopRenderThread = false;
        private readonly object _renderLock = new object();
        private long _ticksPerSecond;
        private const double TargetFrameTimeMs = 1000.0 / 60;
        private double _fadeAmount = 0.92;
        private IntPtr _hwnd;

        // Star color options - blueish-white colors for a realistic space look
        private readonly Color[] _starColors = new[]
        {
            Color.FromRgb(255, 255, 255),    // Pure white
            Color.FromRgb(230, 240, 255),    // Light blue-white
            Color.FromRgb(220, 225, 255),    // Pale blue
            Color.FromRgb(200, 200, 255),    // Light lavender
            Color.FromRgb(160, 180, 255),    // Blue - added for more depth
            Color.FromRgb(100, 150, 255),    // Deeper blue - added for more depth
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

            // Force software rendering mode for better performance
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

            // Get high-performance timer frequency
            QueryPerformanceFrequency(out _ticksPerSecond);

            // Force the overlay to be hidden initially
            RootGrid.Visibility = Visibility.Collapsed;

            // Setup event handlers
            this.Loaded += HyperspaceOverlay_Loaded;
            this.SizeChanged += HyperspaceOverlay_SizeChanged;
            this.Unloaded += HyperspaceOverlay_Unloaded;

            Log.Information("🚀 HyperspaceOverlay created - initially hidden");
        }

        private void HyperspaceOverlay_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get the HWND for this window
                WindowInteropHelper helper = new WindowInteropHelper(Window.GetWindow(this));
                _hwnd = helper.Handle;

                // Initialize starfield if not already done
              
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in HyperspaceOverlay_Loaded");
            }
        }

        private void HyperspaceOverlay_Unloaded(object sender, RoutedEventArgs e)
        {
            StopRendering();
        }

        private void HyperspaceOverlay_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                // Update screen center
                _screenCenter = new Point(e.NewSize.Width / 2, e.NewSize.Height / 2);

                // Re-initialize the starfield when the size changes and visible
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
                // Stop rendering thread
                StopRendering();

                // Clear all resources
                StarfieldCanvas.Children.Clear();
                _starInfos.Clear();

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
                // Check if we have valid dimensions
                if (ActualWidth <= 0 || ActualHeight <= 0)
                {
                    Log.Warning("Cannot initialize starfield - invalid dimensions");
                    return;
                }

                // Reduce bitmap size for better performance (scaling factor)
                double scaleFactor = 1.0;
                _bitmapWidth = Math.Max(1, (int)(ActualWidth / scaleFactor));
                _bitmapHeight = Math.Max(1, (int)(ActualHeight / scaleFactor));

                // Create the bitmap and pixel buffer
                lock (_renderLock)
                {
                    _bitmap = new WriteableBitmap(_bitmapWidth, _bitmapHeight, 96, 96, PixelFormats.Bgra32, null);
                    _pixelBufferStride = _bitmapWidth * 4;
                    _pixelBuffer = new byte[_bitmapHeight * _pixelBufferStride];

                    // Clear buffer to black
                    for (int i = 0; i < _pixelBuffer.Length; i += 4)
                    {
                        _pixelBuffer[i] = 0;     // B
                        _pixelBuffer[i + 1] = 0; // G
                        _pixelBuffer[i + 2] = 0; // R
                        _pixelBuffer[i + 3] = 255; // A
                    }
                }

                // Create and add the image control
                Dispatcher.Invoke(() =>
                {
                    _bitmapImageControl = new Image
                    {
                        Source = _bitmap,
                        Stretch = Stretch.Fill,
                        Width = ActualWidth,
                        Height = ActualHeight
                    };

                    // Set bitmap scaling mode
                    RenderOptions.SetBitmapScalingMode(_bitmapImageControl, BitmapScalingMode.NearestNeighbor);

                    StarfieldCanvas.Children.Clear();
                    StarfieldCanvas.Children.Add(_bitmapImageControl);
                });

                // Setup our stars
                _starInfos.Clear();
                _screenCenter = new Point(_bitmapWidth / 2, _bitmapHeight / 2);

                // Create stars with varying characteristics
                for (int i = 0; i < _numStars; i++)
                {
                    // Randomized starting position (with some stars already in transit)
                    double angle = _random.NextDouble() * Math.PI * 2;
                    double initialDist = _random.NextDouble() * (_bitmapWidth / 3);

                    // Vary the speed based on star brightness (brighter stars = faster)
                    double speed = 0.5 + _random.NextDouble() * 3.0;
                    double radius = 1.0 + _random.NextDouble() * 2.0;

                    // Select a color, with bias toward the first few (brighter) options
                    int colorIndex = (int)Math.Floor(_random.NextDouble() * _random.NextDouble() * _starColors.Length);
                    var color = _starColors[colorIndex];

                    // Calculate starting position
                    var startX = _screenCenter.X + Math.Cos(angle) * initialDist;
                    var startY = _screenCenter.Y + Math.Sin(angle) * initialDist;

                    // Setup velocity vector
                    var velocity = new Vector(Math.Cos(angle) * speed, Math.Sin(angle) * speed);

                    // Add to star collection
                    _starInfos.Add(new StarInfo
                    {
                        Position = new Point(startX, startY),
                        PreviousPosition = new Point(startX, startY), // Initialize previous to current
                        Velocity = velocity,
                        Radius = radius,
                        Color = color
                    });
                }

                // Start the rendering thread 
                StartRenderThread();

                _starfieldInitialized = true;
                Log.Information("Starfield initialized with {0} stars at {1}x{2}", _numStars, _bitmapWidth, _bitmapHeight);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in InitializeStarfield");
            }
        }

        private void StartRenderThread()
        {
            if (_isRendering) return;

            _stopRenderThread = false;
            _isRendering = true;

            _renderThread = new Thread(RenderThreadProc)
            {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };
            _renderThread.Start();
        }

        private void StopRendering()
        {
            if (!_isRendering) return;

            _stopRenderThread = true;
            _renderThread?.Join();
            _renderThread = null;
            _isRendering = false;
        }


        private void RenderThreadProc()
        {
            long lastTickCount = 0;
            QueryPerformanceCounter(out lastTickCount);

            double frameTimeMs = 0;

            while (!_stopRenderThread)
            {
                // Get current high-resolution time
                long currentTickCount;
                QueryPerformanceCounter(out currentTickCount);

                // Calculate elapsed time in milliseconds
                double elapsedMs = (currentTickCount - lastTickCount) * 1000.0 / _ticksPerSecond;

                // Throttle frame rate
                if (elapsedMs < TargetFrameTimeMs)
                {
                    int sleepTime = (int)(TargetFrameTimeMs - elapsedMs);
                    if (sleepTime > 1)
                    {
                        Thread.Sleep(sleepTime - 1);
                    }
                    continue;
                }

                // Update last time
                lastTickCount = currentTickCount;

                // Calculate frame time for smooth animation adjustments
                frameTimeMs = Math.Min(elapsedMs, 100); // Cap at 100ms (10fps) to avoid huge jumps
                double timeFactor = frameTimeMs / 16.667; // Scale factor for time-based movement (16.667ms = 60fps)

                try
                {
                    // Process stars and update buffer
                    ProcessStars(timeFactor);

                    // Update bitmap on UI thread
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (_bitmap != null && RootGrid.Visibility == Visibility.Visible)
                            {
                                // Lock for thread safety
                                lock (_renderLock)
                                {
                                    _bitmap.WritePixels(new Int32Rect(0, 0, _bitmapWidth, _bitmapHeight),
                                        _pixelBuffer, _pixelBufferStride, 0);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Error updating bitmap");
                        }
                    }), DispatcherPriority.Render);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error in render thread");
                }
            }
        }

        private void ProcessStars(double timeFactor)
        {
            // Lock for thread safety
            lock (_renderLock)
            {
                // Apply fade effect for trail/motion blur
                for (int i = 0; i < _pixelBuffer.Length; i += 4)
                {
                    if (_pixelBuffer[i] > 0 || _pixelBuffer[i + 1] > 0 || _pixelBuffer[i + 2] > 0)
                    {
                        _pixelBuffer[i] = (byte)(_pixelBuffer[i] * _fadeAmount);
                        _pixelBuffer[i + 1] = (byte)(_pixelBuffer[i + 1] * _fadeAmount);
                        _pixelBuffer[i + 2] = (byte)(_pixelBuffer[i + 2] * _fadeAmount);
                    }
                    _pixelBuffer[i + 3] = 255; // Alpha always 255
                }

                // Update and draw each star
                foreach (var star in _starInfos)
                {
                    // Save previous position
                    star.PreviousPosition = star.Position;

                    // Calculate acceleration factor from center (for tunnel effect)
                    Vector toCenter = star.Position - _screenCenter;
                    double distFromCenter = toCenter.Length;
                    double depthFactor = Math.Min(1.0, distFromCenter / (_bitmapWidth * 0.4));

                    // Apply position change with perspective speed boost - adjusted for frame timing
                    star.Position += star.Velocity * (1.0 + depthFactor * 4.0) * timeFactor;

                    // Reset star if offscreen
                    if (star.Position.X < 0 || star.Position.X >= _bitmapWidth ||
                        star.Position.Y < 0 || star.Position.Y >= _bitmapHeight)
                    {
                        // Choose a new starting angle
                        double angle = _random.NextDouble() * Math.PI * 2;

                        // Vary the speed and size
                        double speed = 0.5 + _random.NextDouble() * 3.0;
                        double radius = 1.0 + _random.NextDouble() * 2.0;

                        // Start near center
                        var newPos = new Point(
                            _screenCenter.X + Math.Cos(angle) * (_random.NextDouble() * 10),
                            _screenCenter.Y + Math.Sin(angle) * (_random.NextDouble() * 10)
                        );

                        // Update star properties
                        star.Position = newPos;
                        star.PreviousPosition = newPos;
                        star.Velocity = new Vector(Math.Cos(angle) * speed, Math.Sin(angle) * speed);
                        star.Radius = radius;

                        // Randomize color - bias toward brighter stars
                        int colorIndex = (int)Math.Floor(_random.NextDouble() * _random.NextDouble() * _starColors.Length);
                        star.Color = _starColors[colorIndex];
                    }

                    // Draw trail line if star has moved significantly
                    Vector movement = star.Position - star.PreviousPosition;
                    if (movement.LengthSquared > 1.0) // Only draw meaningful trails
                    {
                        Point trailStart = star.PreviousPosition;
                        DrawLineWithFade(
                            (int)trailStart.X, (int)trailStart.Y,
                            (int)star.Position.X, (int)star.Position.Y,
                            star.Color);
                    }

                    // Draw star as a glowing point
                    int px = (int)star.Position.X;
                    int py = (int)star.Position.Y;

                    // Draw the core and glow based on brightness and speed
                    double brightness = 0.9 + (star.Velocity.Length * 0.05);
                    DrawGlowPixel(px, py, star.Color, Math.Min(1.0, brightness));     // center pixel
                    DrawGlowPixel(px + 1, py, star.Color, 0.5); // neighbors
                    DrawGlowPixel(px - 1, py, star.Color, 0.5);
                    DrawGlowPixel(px, py + 1, star.Color, 0.5);
                    DrawGlowPixel(px, py - 1, star.Color, 0.5);

                    // Diagonal neighbors for larger stars
                    if (star.Radius > 1.5)
                    {
                        DrawGlowPixel(px + 1, py + 1, star.Color, 0.3);
                        DrawGlowPixel(px - 1, py - 1, star.Color, 0.3);
                        DrawGlowPixel(px + 1, py - 1, star.Color, 0.3);
                        DrawGlowPixel(px - 1, py + 1, star.Color, 0.3);
                    }
                }
            }
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

            int index = (y * _bitmapWidth + x) * 4;

            _pixelBuffer[index + 0] = Blend(_pixelBuffer[index + 0], color.B, alpha);
            _pixelBuffer[index + 1] = Blend(_pixelBuffer[index + 1], color.G, alpha);
            _pixelBuffer[index + 2] = Blend(_pixelBuffer[index + 2], color.R, alpha);
            _pixelBuffer[index + 3] = 255;
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

           

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(ForceHidden));
                return;
            }

            RootGrid.Visibility = Visibility.Collapsed;

            // Stop rendering thread
            StopRendering();
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
                    else if (!_isRendering)
                    {
                        // Restart rendering if needed
                        StartRenderThread();
                    }

                    RootGrid.Visibility = Visibility.Visible;
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
                    Log.Information("🚀 HyperspaceOverlay now HIDDEN");

                    // Stop rendering to save resources
                    StopRendering();
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
}