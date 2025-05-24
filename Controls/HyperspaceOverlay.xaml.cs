// Optimized HyperspaceOverlay with improved performance matching CarrierJumpOverlay
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using EliteInfoPanel.Core;
using Serilog;

namespace EliteInfoPanel.Controls
{
    public partial class HyperspaceOverlay : UserControl
    {
        #region Private Fields

        // P/Invoke for high-performance timer
        [DllImport("kernel32.dll")] private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);
        [DllImport("kernel32.dll")] private static extern bool QueryPerformanceFrequency(out long lpFrequency);

        private const double TargetFrameTimeMs = 1000.0 / 60; // Match CarrierJumpOverlay exactly

        private GameStateService _gameState;
        private readonly Random _random = new Random();
        private readonly int _numStars = 150; // Slightly more than carrier overlay for effect
        private bool _starfieldInitialized = false;
        private Point _screenCenter;
        private readonly List<StarInfo> _stars = new();
        private WriteableBitmap _bitmap;
        private int _bitmapWidth, _bitmapHeight;
        private byte[] _pixelBuffer;
        private int _pixelBufferStride;
        private bool _isRendering = false;
        private Thread _renderThread;
        private bool _stopRenderThread = false;
        private readonly object _renderLock = new object();
        private long _ticksPerSecond;

        // Star color options - optimized array for better cache performance
        private readonly Color[] _starColors = new[]
        {
            Color.FromRgb(255, 255, 255),    // Pure white
            Color.FromRgb(230, 240, 255),    // Light blue-white
            Color.FromRgb(220, 225, 255),    // Pale blue
            Color.FromRgb(200, 200, 255)     // Light lavender
        };

        // Legal state colors - cached for performance
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

        #endregion Private Fields

        #region Constructor

        public HyperspaceOverlay()
        {
            InitializeComponent();

            // Match CarrierJumpOverlay settings exactly
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

        #endregion Constructor

        #region Event Handlers

        private void HyperspaceOverlay_Loaded(object sender, RoutedEventArgs e)
        {
            // Minimal work here - just log
            Log.Debug("HyperspaceOverlay loaded");
        }

        private void HyperspaceOverlay_Unloaded(object sender, RoutedEventArgs e)
        {
            StopRendering();
        }

        private void HyperspaceOverlay_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                // Only re-initialize if visible and size is valid
                if (RootGrid.Visibility == Visibility.Visible &&
                    e.NewSize.Width > 0 && e.NewSize.Height > 0)
                {
                    InitializeStarfield();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in HyperspaceOverlay_SizeChanged");
            }
        }

        #endregion Event Handlers

        #region Initialization

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

                // Only clear if we need to rebuild
                if (_starfieldInitialized)
                {
                    StopRendering();
                }

                // Calculate dimensions - match CarrierJumpOverlay approach
                _bitmapWidth = (int)ActualWidth;
                _bitmapHeight = (int)ActualHeight;
                _screenCenter = new Point(_bitmapWidth / 2, _bitmapHeight / 2);

                // Create the bitmap and pixel buffer in one go
                lock (_renderLock)
                {
                    _bitmap = new WriteableBitmap(_bitmapWidth, _bitmapHeight, 96, 96, PixelFormats.Bgra32, null);
                    _pixelBufferStride = _bitmapWidth * 4;
                    _pixelBuffer = new byte[_bitmapHeight * _pixelBufferStride];

                    // Initialize buffer to black with full alpha - optimized loop
                    for (int i = 3; i < _pixelBuffer.Length; i += 4)
                    {
                        _pixelBuffer[i] = 255; // Set alpha to 255
                    }
                }

                // Create and add the image control
                Dispatcher.Invoke(() =>
                {
                    var bitmapImageControl = new Image
                    {
                        Source = _bitmap,
                        Stretch = Stretch.Fill,
                        Width = ActualWidth,
                        Height = ActualHeight
                    };

                    RenderOptions.SetBitmapScalingMode(bitmapImageControl, BitmapScalingMode.NearestNeighbor);

                    StarfieldCanvas.Children.Clear();
                    StarfieldCanvas.Children.Add(bitmapImageControl);
                });

                // Initialize stars - match CarrierJumpOverlay pattern exactly
                _stars.Clear();
                for (int i = 0; i < _numStars; i++)
                {
                    double angle = _random.NextDouble() * Math.PI * 2;
                    double dist = _random.NextDouble() * 20; // Start near center

                    var startX = _screenCenter.X + Math.Cos(angle) * dist;
                    var startY = _screenCenter.Y + Math.Sin(angle) * dist;

                    double speed = 0.5 + _random.NextDouble() * 3.0;
                    double radius = 1.0 + _random.NextDouble() * 2.0;

                    // Optimized color selection
                    int colorIndex = (int)(_random.NextDouble() * _random.NextDouble() * _starColors.Length);
                    var color = _starColors[colorIndex];

                    _stars.Add(new StarInfo
                    {
                        Position = new Point(startX, startY),
                        PreviousPosition = new Point(startX, startY),
                        Velocity = new Vector(Math.Cos(angle) * speed, Math.Sin(angle) * speed),
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

        #endregion Initialization

        #region Rendering

        private void StartRenderThread()
        {
            if (_isRendering) return;

            _stopRenderThread = false;
            _isRendering = true;

            _renderThread = new Thread(RenderLoop)
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

            if (_renderThread != null && _renderThread.IsAlive)
            {
                try
                {
                    if (!_renderThread.Join(500))
                    {
                        Log.Warning("Render thread join timeout — forcibly interrupting");
                        _renderThread.Interrupt();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error stopping render thread");
                }
            }

            _renderThread = null;
            _isRendering = false;
            _starfieldInitialized = false;
        }

        private void RenderLoop()
        {
            // Initialize timing - match CarrierJumpOverlay exactly
            QueryPerformanceCounter(out long lastTicks);

            while (!_stopRenderThread)
            {
                try
                {
                    // Get current high-resolution time
                    QueryPerformanceCounter(out long nowTicks);
                    double elapsed = (nowTicks - lastTicks) * 1000.0 / _ticksPerSecond;

                    // Throttle frame rate - match CarrierJumpOverlay
                    if (elapsed < TargetFrameTimeMs)
                    {
                        int sleepTime = (int)(TargetFrameTimeMs - elapsed);
                        if (sleepTime > 1)
                        {
                            Thread.Sleep(sleepTime);
                        }
                        continue;
                    }

                    lastTicks = nowTicks;

                    // Process everything in a single locked block
                    lock (_renderLock)
                    {
                        // Apply fade to create trails - match CarrierJumpOverlay exactly
                        ApplyFade();

                        // Update and render all stars
                        ProcessStars();
                    }

                    // Update the UI - match CarrierJumpOverlay pattern
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (_bitmap != null && RootGrid.Visibility == Visibility.Visible)
                            {
                                lock (_renderLock)
                                {
                                    _bitmap.WritePixels(new Int32Rect(0, 0, _bitmapWidth, _bitmapHeight),
                                        _pixelBuffer, _pixelBufferStride, 0);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Error updating bitmap");
                        }
                    }));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error in render thread");
                }
            }
        }

        private void ApplyFade()
        {
            // Simple fade effect - match CarrierJumpOverlay exactly
            for (int i = 0; i < _pixelBuffer.Length; i += 4)
            {
                _pixelBuffer[i] = (byte)(_pixelBuffer[i] * 0.92);         // B
                _pixelBuffer[i + 1] = (byte)(_pixelBuffer[i + 1] * 0.92); // G
                _pixelBuffer[i + 2] = (byte)(_pixelBuffer[i + 2] * 0.92); // R
                // Alpha is always 255
            }
        }

        private void ProcessStars()
        {
            foreach (var star in _stars)
            {
                // Save previous position
                star.PreviousPosition = star.Position;

                // Calculate vector to center and apply acceleration
                Vector toCenter = star.Position - _screenCenter;
                double dist = toCenter.Length;
                double factor = Math.Min(1.0, dist / (_bitmapWidth * 0.4));
                star.Position += star.Velocity * (1.0 + factor * 4.0);

                // Reset star if offscreen - match CarrierJumpOverlay logic exactly
                if (star.Position.X < 0 || star.Position.X >= _bitmapWidth ||
                    star.Position.Y < 0 || star.Position.Y >= _bitmapHeight)
                {
                    double angle = _random.NextDouble() * Math.PI * 2;
                    double speed = 0.5 + _random.NextDouble() * 3.0;
                    double radius = 1.0 + _random.NextDouble() * 2.0;

                    var newPos = new Point(
                        _screenCenter.X + Math.Cos(angle) * (_random.NextDouble() * 10),
                        _screenCenter.Y + Math.Sin(angle) * (_random.NextDouble() * 10)
                    );

                    star.Position = newPos;
                    star.PreviousPosition = newPos;
                    star.Velocity = new Vector(Math.Cos(angle) * speed, Math.Sin(angle) * speed);
                    star.Radius = radius;

                    int colorIndex = (int)(_random.NextDouble() * _random.NextDouble() * _starColors.Length);
                    star.Color = _starColors[colorIndex];
                }
                else
                {
                    // Draw trail line - use optimized version
                    DrawLineWithFade((int)star.PreviousPosition.X, (int)star.PreviousPosition.Y,
                                    (int)star.Position.X, (int)star.Position.Y, star.Color);
                }
            }
        }

        private void DrawLineWithFade(int x0, int y0, int x1, int y1, Color color)
        {
            // Optimized Bresenham's line algorithm - match CarrierJumpOverlay
            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;
            int e2;

            const double alpha = 0.6; // Match CarrierJumpOverlay

            while (true)
            {
                // Check bounds and draw pixel
                if (x0 >= 0 && x0 < _bitmapWidth && y0 >= 0 && y0 < _bitmapHeight)
                {
                    int index = (y0 * _bitmapWidth + x0) * 4;

                    // Use the same blending formula as CarrierJumpOverlay
                    _pixelBuffer[index] = Blend(_pixelBuffer[index], color.B, alpha);
                    _pixelBuffer[index + 1] = Blend(_pixelBuffer[index + 1], color.G, alpha);
                    _pixelBuffer[index + 2] = Blend(_pixelBuffer[index + 2], color.R, alpha);
                    _pixelBuffer[index + 3] = 255;
                }

                if (x0 == x1 && y0 == y1) break;

                e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx) { err += dx; y0 += sy; }
            }

            // Add brighter center pixel at current position
            if (x1 >= 0 && x1 < _bitmapWidth && y1 >= 0 && y1 < _bitmapHeight)
            {
                int index = (y1 * _bitmapWidth + x1) * 4;
                _pixelBuffer[index] = color.B;
                _pixelBuffer[index + 1] = color.G;
                _pixelBuffer[index + 2] = color.R;
                _pixelBuffer[index + 3] = 255;
            }
        }

        private byte Blend(byte background, byte foreground, double alpha)
        {
            return (byte)(background * (1 - alpha) + foreground * alpha);
        }

        #endregion Rendering

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
                    // Initialize starfield if needed
                    if (!_starfieldInitialized)
                    {
                        InitializeStarfield();
                    }
                    else if (!_isRendering)
                    {
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
                    StopRendering();
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

    public class StarInfo
    {
        public Point Position;
        public Point PreviousPosition;
        public Vector Velocity;
        public double Radius;
        public Color Color;
    }
}