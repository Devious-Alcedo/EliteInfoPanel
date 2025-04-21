// Updated CarrierJumpOverlay with realistic starfield and lightning forks
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
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using EliteInfoPanel.Core;
using Serilog;

namespace EliteInfoPanel.Controls
{
    public partial class CarrierJumpOverlay : UserControl
    {
        [DllImport("kernel32.dll")] private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);
        [DllImport("kernel32.dll")] private static extern bool QueryPerformanceFrequency(out long lpFrequency);

        private GameStateService _gameState;
        private WriteableBitmap _bitmap;
        private byte[] _pixelBuffer;
        private int _bitmapWidth, _bitmapHeight, _pixelBufferStride;
        private Thread _renderThread;
        private bool _stopRenderThread;
        private long _ticksPerSecond;
        private const double TargetFrameTimeMs = 1000.0 / 60;
        private readonly Random _random = new();
        private readonly object _renderLock = new();
        private readonly List<ForkSegment> _lightningForks = new();
        private readonly List<StarInfo> _stars = new();
        private Point _center;
        private bool _isRendering = false;
        private DispatcherTimer _countdownMonitorTimer;

        private readonly Color[] _starColors = new[]
        {
            Color.FromRgb(255, 0, 128),
            Color.FromRgb(200, 0, 200),
            Color.FromRgb(255, 100, 255),
            Color.FromRgb(180, 80, 180)
        };

        public CarrierJumpOverlay()
        {
            InitializeComponent();
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += OnSizeChanged;
            QueryPerformanceFrequency(out _ticksPerSecond);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
          
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            StopRenderThread();
            _countdownMonitorTimer?.Stop();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            InitBitmap();
            InitStarfield();
        }

        private void InitBitmap()
        {
            if (ActualWidth <= 0 || ActualHeight <= 0)
                return;

            _bitmapWidth = (int)ActualWidth;
            _bitmapHeight = (int)ActualHeight;
            _pixelBufferStride = _bitmapWidth * 4;
            _pixelBuffer = new byte[_bitmapHeight * _pixelBufferStride];
            _bitmap = new WriteableBitmap(_bitmapWidth, _bitmapHeight, 96, 96, PixelFormats.Bgra32, null);
            RenderImage.Source = _bitmap;
            _center = new Point(_bitmapWidth / 2, _bitmapHeight / 2);
        }

        private void InitStarfield()
        {
            _stars.Clear();
            int count = 200;
            for (int i = 0; i < count; i++)
            {
                double angle = _random.NextDouble() * Math.PI * 2;
                double dist = _random.NextDouble() * 20;
                var startX = _center.X + Math.Cos(angle) * dist;
                var startY = _center.Y + Math.Sin(angle) * dist;
                double speed = 0.5 + _random.NextDouble() * 3.0;
                double radius = 1.0 + _random.NextDouble() * 2.0;
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
        }

        private void StartRenderThread()
        {
            if (_renderThread != null && _renderThread.IsAlive)
            {
                Log.Warning("CarrierJumpOverlay render thread already running");
                return;
            }

            _stopRenderThread = false;
            _renderThread = new Thread(RenderLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };

            Log.Information("Starting CarrierJumpOverlay render thread");
            _renderThread.Start();
        }


        private void StopRenderThread()
        {
            if (_renderThread == null || !_renderThread.IsAlive) return;

            Log.Information("Stopping CarrierJumpOverlay render thread");

            _stopRenderThread = true;

            if (!_renderThread.Join(500))
            {
                try
                {
                    _renderThread.Interrupt();
                    Log.Warning("Render thread join timeout — forcibly interrupting");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error interrupting render thread");
                }
            }

            _renderThread = null;
        }




        private void RenderLoop()
        {
            QueryPerformanceCounter(out long lastTicks);

            while (!_stopRenderThread)
            {
                QueryPerformanceCounter(out long nowTicks);
                double elapsed = (nowTicks - lastTicks) * 1000.0 / _ticksPerSecond;
                if (elapsed < TargetFrameTimeMs)
                {
                    Thread.Sleep((int)(TargetFrameTimeMs - elapsed));
                    continue;
                }

                lastTicks = nowTicks;

                lock (_renderLock)
                {
                    for (int i = 0; i < _pixelBuffer.Length; i += 4)
                    {
                        _pixelBuffer[i] = (byte)(_pixelBuffer[i] * 0.92);
                        _pixelBuffer[i + 1] = (byte)(_pixelBuffer[i + 1] * 0.92);
                        _pixelBuffer[i + 2] = (byte)(_pixelBuffer[i + 2] * 0.92);
                        _pixelBuffer[i + 3] = 255;
                    }

                    ProcessStars();

                    if (_random.NextDouble() < 0.03)
                        GenerateFork();

                    DrawForks();

                    Dispatcher.Invoke(() =>
                    {
                        _bitmap.WritePixels(new Int32Rect(0, 0, _bitmapWidth, _bitmapHeight), _pixelBuffer, _pixelBufferStride, 0);
                    });
                }
            }
        }

        private void ProcessStars()
        {
            foreach (var star in _stars)
            {
                star.PreviousPosition = star.Position;
                Vector toCenter = star.Position - _center;
                double dist = toCenter.Length;
                double factor = Math.Min(1.0, dist / (_bitmapWidth * 0.4));
                star.Position += star.Velocity * (1.0 + factor * 4.0);

                if (star.Position.X < 0 || star.Position.X >= _bitmapWidth || star.Position.Y < 0 || star.Position.Y >= _bitmapHeight)
                {
                    double angle = _random.NextDouble() * Math.PI * 2;
                    double speed = 0.5 + _random.NextDouble() * 3.0;
                    double radius = 1.0 + _random.NextDouble() * 2.0;
                    var newPos = new Point(
                        _center.X + Math.Cos(angle) * (_random.NextDouble() * 10),
                        _center.Y + Math.Sin(angle) * (_random.NextDouble() * 10));
                    star.Position = newPos;
                    star.PreviousPosition = newPos;
                    star.Velocity = new Vector(Math.Cos(angle) * speed, Math.Sin(angle) * speed);
                    star.Radius = radius;
                    int colorIndex = (int)(_random.NextDouble() * _random.NextDouble() * _starColors.Length);
                    star.Color = _starColors[colorIndex];
                }

                DrawLineWithFade((int)star.PreviousPosition.X, (int)star.PreviousPosition.Y,
                                 (int)star.Position.X, (int)star.Position.Y, star.Color);
            }
        }

        private void DrawLineWithFade(int x0, int y0, int x1, int y1, Color color)
        {
            int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx - dy, e2, index;
            double alpha = 0.25;

            while (true)
            {
                if (x0 >= 0 && x0 < _bitmapWidth && y0 >= 0 && y0 < _bitmapHeight)
                {
                    index = (y0 * _bitmapWidth + x0) * 4;
                    _pixelBuffer[index + 0] = Blend(_pixelBuffer[index + 0], color.B, alpha);
                    _pixelBuffer[index + 1] = Blend(_pixelBuffer[index + 1], color.G, alpha);
                    _pixelBuffer[index + 2] = Blend(_pixelBuffer[index + 2], color.R, alpha);
                    _pixelBuffer[index + 3] = 255;
                }
                if (x0 == x1 && y0 == y1) break;
                e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx) { err += dx; y0 += sy; }
            }
        }

        private byte Blend(byte background, byte foreground, double alpha)
        {
            return (byte)(background * (1 - alpha) + foreground * alpha);
        }

        private void GenerateFork()
        {
            Point start = GetRandomEdgePoint();
            List<Point> segments = new() { start };
            Point current = start;
            Vector direction = _center - start;
            direction.Normalize();

            for (int i = 0; i < 20; i++)
            {
                double variance = (_random.NextDouble() - 0.5) * 100;
                Vector offset = new Vector(-direction.Y, direction.X) * variance;
                Vector step = direction * (_bitmapWidth / 20);
                current += step + offset;
                segments.Add(current);
                if ((current - _center).Length < 50) break;
            }

            _lightningForks.Add(new ForkSegment
            {
                Points = segments,
                CreatedAt = DateTime.UtcNow
            });
        }

        private Point GetRandomEdgePoint()
        {
            double edge = _random.NextDouble();
            if (edge < 0.25) return new Point(_random.Next(_bitmapWidth), 0);
            if (edge < 0.5) return new Point(_bitmapWidth, _random.Next(_bitmapHeight));
            if (edge < 0.75) return new Point(_random.Next(_bitmapWidth), _bitmapHeight);
            return new Point(0, _random.Next(_bitmapHeight));
        }

        private void DrawForks()
        {
            DateTime now = DateTime.UtcNow;
            _lightningForks.RemoveAll(f => (now - f.CreatedAt).TotalMilliseconds > 150);

            foreach (var fork in _lightningForks)
            {
                for (int i = 0; i < fork.Points.Count - 1; i++)
                {
                    DrawLineWithFade((int)fork.Points[i].X, (int)fork.Points[i].Y,
                                     (int)fork.Points[i + 1].X, (int)fork.Points[i + 1].Y,
                                     Colors.White);
                }
            }
        }

        public void SetGameState(GameStateService gameState)
        {
            _gameState = gameState;
            _gameState.PropertyChanged += GameState_PropertyChanged;

            _countdownMonitorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _countdownMonitorTimer.Tick += (s, e) =>
            {
                if (_gameState.ShowCarrierJumpOverlay)
                    UpdateVisibility(); // This re-checks countdown
            };
            _countdownMonitorTimer.Start();

            UpdateVisibility();
        }


        private void GameState_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GameStateService.ShowCarrierJumpOverlay))
                Dispatcher.Invoke(UpdateVisibility);
        }

        private void UpdateVisibility()
        {
            Log.Information("UpdateVisibility: ShowCarrierJumpOverlay={Show}, CountdownSeconds={Countdown}",
                   _gameState.ShowCarrierJumpOverlay, _gameState.CarrierJumpCountdownSeconds);
            if (_gameState?.ShowCarrierJumpOverlay == true && _gameState.CarrierJumpCountdownSeconds <= 0)
            {
                Log.Information("Overlay becoming VISIBLE (conditions met)");
                OverlayGrid.Visibility = Visibility.Visible;

                if (!_isRendering)
                {
                    InitBitmap();
                    InitStarfield();
                    StartRenderThread();
                }

                DestinationText.Text = string.IsNullOrEmpty(_gameState.CarrierJumpDestinationSystem)
                    ? "???"
                    : _gameState.CarrierJumpDestinationSystem;
            }
            else
            {
                Log.Information("Overlay HIDDEN — conditions not met");
                OverlayGrid.Visibility = Visibility.Collapsed;

                if (_isRendering)
                {
                    StopRenderThread();
                }
            }
        }

        public void ForceHidden()
        {
            OverlayGrid.Visibility = Visibility.Collapsed;
            StopRenderThread();
        }

        private class ForkSegment
        {
            public List<Point> Points;
            public DateTime CreatedAt;
        }

        private class StarInfo
        {
            public Point Position;
            public Point PreviousPosition;
            public Vector Velocity;
            public double Radius;
            public Color Color;
        }
    }
}
