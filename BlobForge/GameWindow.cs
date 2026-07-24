using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using BlobForge.Audio;
using BlobForge.Physics;
using BlobForge.Rendering;
using BlobForge.World;

namespace BlobForge;

public sealed class GameWindow : Form
{
    private const float FixedDt = 1f / 120f;
    private const int MaxCatchUpStepsPerPump = 4;
    private const double TargetRenderSeconds = 1d / 60d;
    private const uint TimerResolutionMs = 1;
    private const int VerticalRefreshDeviceCapability = 116;
    private const int ColorOnColorStretchMode = 3;
    private const uint UncompressedRgbBitmap = 0;
    private const uint RgbColorTable = 0;
    private const uint SourceCopyRasterOperation = 0x00CC0020;
    private const int WorldWidth = 1280;
    private const int WorldHeight = 720;
    private static readonly Size LogicalViewport = new(WorldWidth, WorldHeight);
    private static readonly BlobArchetype StationUnit = BlobArchetype.ProcessingUnit;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly InputState _input = new();
    private readonly GameRenderer _renderer = new();
    private readonly SoundEffectMixer _audio = new();
    private readonly FixtureLayoutSettings _fixtureLayout = FixtureLayoutSettings.Load();
    private readonly GameSurface _surface;
    private readonly Button _spawnButton;
    private readonly Button _conveyorButton;
    private readonly Button _lightButton;
    private readonly Button _fullscreenButton;
    private readonly Panel _pausePanel;
    private readonly Panel _settingsPanel;
    private readonly CheckBox _settingsFullscreen;
    private readonly CheckBox _settingsDebug;
    private readonly CheckBox _settingsGravity;
    private readonly Bitmap _frameBuffer;
    private readonly Graphics _frameGraphics;
    private BlobWorld _world = null!;
    private SoftBody? _grabbed;
    private Vector2 _rightGestureStart;
    private bool _rightDragging;
    private readonly List<Vector2> _pendingSlice = new(64);
    private SoftBody? _sliceTarget;
    private bool _sliceWasInside;
    private float _sliceInsideDistance;
    private double _accumulator;
    private double _lastTime;
    private double _nextRenderTime;
    private double _lastPumpTime;
    private double _lastPaintTime;
    private double _lastSimulationStepTime;
    private double _maximumPumpGapSincePaint;
    private double _maximumSimulationGapSincePaint;
    private double _maximumRenderLatenessSincePaint;
    private double _fixedUpdateMsSinceRender;
    private int _stepsSinceRender;
    private int _maximumStepBatchSinceRender;
    private int _missedRenderDeadlines;
    private int _paintSpikeCount;
    private int _renderRequestCount;
    private int _paintCount;
    private int _paintWindowSamples;
    private double _paintWindowStart;
    private double _paintWindowSumMs;
    private double _paintWindowSumSquaredMs;
    private double _paintWindowMaximumMs;
    private long _simulationAllocatedBytesSinceRender;
    private double _fpsSmoothing = 60;
    private double _audioUpdateMsThisFrame;
    private bool _gravityEnabled = true;
    private ConveyorBelt? _selectedConveyor;
    private ConveyorEditHandle _conveyorEditHandle;
    private Vector2 _conveyorEditLast;
    private Rectangle _windowedBounds;
    private bool _paused;
    private bool _isFullscreen;
    private ChamberFeedController? _chamberFeed;
    private bool _draggingChamberLever;
    private bool _draggingBreakerLever;
    private bool _holdingCrusherButton;
    private bool _holdingDrillLever;
    private bool _draggingFilterKnob;
    private bool _draggingVacuumNozzle;
    private bool _draggingDrumWheel;
    private IndustrialLight? _selectedLight;
    private LightEditHandle _lightEditHandle;
    private Vector2 _lightEditLast;
    private bool _observedFactoryPower;
    private float _factoryStartupDelay = -1f;
    private FixtureDragTarget _fixtureDragTarget;
    private Vector2 _fixtureDragOffset;
    private Vector2 _fixtureDragStart;
    private bool _fixtureDragMoved;
    private bool _toolPrimaryHeld;
    private bool _toolRotationHeld;
    private bool _toolEquipKeyHeld;
    private bool _arsenalMenuConsumedClick;
    private bool _highResolutionTimerActive;
    private int _resizeEventCount;
    private int _fullscreenToggleCount;

    [DllImport("winmm.dll", ExactSpelling = true)]
    private static extern uint timeBeginPeriod(uint periodMilliseconds);

    [DllImport("winmm.dll", ExactSpelling = true)]
    private static extern uint timeEndPeriod(uint periodMilliseconds);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    private static extern int GetDeviceCaps(nint deviceContext, int index);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    private static extern int StretchDIBits(
        nint destinationDeviceContext,
        int destinationX,
        int destinationY,
        int destinationWidth,
        int destinationHeight,
        int sourceX,
        int sourceY,
        int sourceWidth,
        int sourceHeight,
        nint sourceBits,
        ref BitmapInfo bitmapInfo,
        uint colorUsage,
        uint rasterOperation);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    private static extern int SetStretchBltMode(nint deviceContext, int stretchMode);

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPixelsPerMeter;
        public int YPixelsPerMeter;
        public uint ColorsUsed;
        public uint ColorsImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
    }

    public GameWindow()
    {
        Text = "BlobForge — Custom Soft-Body Engine";
        ClientSize = new Size(1280, 720);
        MinimumSize = new Size(980, 620);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        BackColor = Color.FromArgb(12, 16, 24);

        // Render raster art into a normal GDI+ bitmap. A Bitmap constructed over a
        // raw DIB pointer silently drops some DrawImage calls on the fullscreen path,
        // which made Pixel Forge sprites disappear while vector primitives survived.
        _frameBuffer = new Bitmap(WorldWidth, WorldHeight, PixelFormat.Format32bppPArgb);
        _frameGraphics = Graphics.FromImage(_frameBuffer);

        _surface = new GameSurface
        {
            Dock = DockStyle.Fill,
            TabStop = true,
            BackColor = Color.FromArgb(5, 7, 10)
        };
        _surface.Paint += RenderSurface;
        _surface.MouseMove += SurfaceOnMouseMove;
        _surface.MouseDown += SurfaceOnMouseDown;
        _surface.MouseUp += SurfaceOnMouseUp;
        _surface.MouseWheel += SurfaceOnMouseWheel;
        _surface.MouseDoubleClick += SurfaceOnMouseDoubleClick;
        Controls.Add(_surface);

        _spawnButton = new Button
        {
            Text = "+  REQUEST NEXT   [B]",
            Size = new Size(166, 34),
            Location = new Point(ClientSize.Width - 190, 74),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(24, 99, 85),
            ForeColor = Color.FromArgb(224, 255, 244),
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        _spawnButton.FlatAppearance.BorderColor = Color.FromArgb(105, 255, 213);
        _spawnButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(31, 132, 110);
        _spawnButton.Click += (_, _) => SpawnBlob();
        Controls.Add(_spawnButton);
        _spawnButton.BringToFront();

        _conveyorButton = new Button
        {
            Text = "+  SPAWN CONVEYOR   [C]",
            Size = new Size(190, 34),
            Location = new Point(ClientSize.Width - 214, 116),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(70, 67, 32),
            ForeColor = Color.FromArgb(255, 238, 175),
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        _conveyorButton.FlatAppearance.BorderColor = Color.FromArgb(255, 203, 76);
        _conveyorButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(104, 96, 40);
        _conveyorButton.Click += (_, _) => SpawnConveyor();
        Controls.Add(_conveyorButton);
        _conveyorButton.BringToFront();

        _lightButton = new Button
        {
            Text = "+  HANG LANTERN   [L]",
            Size = new Size(190, 34),
            Location = new Point(ClientSize.Width - 214, 158),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(61, 61, 50),
            ForeColor = Color.FromArgb(255, 234, 171),
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        _lightButton.FlatAppearance.BorderColor = Color.FromArgb(226, 190, 95);
        _lightButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(92, 87, 61);
        _lightButton.Click += (_, _) => SpawnLantern();
        Controls.Add(_lightButton);
        _lightButton.BringToFront();

        _fullscreenButton = new Button
        {
            Text = "FULLSCREEN  [F11]",
            Size = new Size(154, 34),
            Location = new Point(ClientSize.Width - 178, 24),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(42, 51, 64),
            ForeColor = Color.FromArgb(224, 233, 241),
            Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        _fullscreenButton.FlatAppearance.BorderColor = Color.FromArgb(126, 148, 166);
        _fullscreenButton.Click += (_, _) => ToggleFullscreen();
        Controls.Add(_fullscreenButton);
        _fullscreenButton.BringToFront();

        _pausePanel = CreateMenuPanel("PAUSED");
        var resumeButton = CreateMenuButton("RESUME", 76);
        var settingsButton = CreateMenuButton("SETTINGS", 120);
        var pauseFullscreenButton = CreateMenuButton("TOGGLE FULLSCREEN", 164);
        var quitButton = CreateMenuButton("QUIT", 208);
        resumeButton.Click += (_, _) => SetPaused(false);
        pauseFullscreenButton.Click += (_, _) => ToggleFullscreen();
        quitButton.Click += (_, _) => Close();
        _pausePanel.Controls.AddRange([resumeButton, settingsButton, pauseFullscreenButton, quitButton]);
        Controls.Add(_pausePanel);

        _settingsPanel = CreateMenuPanel("SETTINGS");
        _settingsPanel.Size = new Size(500, 430);
        _settingsFullscreen = CreateMenuCheckBox("Fullscreen", 78);
        _settingsDebug = CreateMenuCheckBox("Debug metrics", 118);
        _settingsGravity = CreateMenuCheckBox("Gravity simulation", 158);
        var settingsBackButton = CreateMenuButton("BACK", 378);
        settingsBackButton.Left = 134;
        _settingsFullscreen.CheckedChanged += (_, _) =>
        {
            if (_settingsFullscreen.Checked != _isFullscreen) ToggleFullscreen();
        };
        _settingsDebug.CheckedChanged += (_, _) => _renderer.DebugDraw = _settingsDebug.Checked;
        _settingsGravity.CheckedChanged += (_, _) =>
        {
            _gravityEnabled = _settingsGravity.Checked;
            if (_world is not null)
                foreach (var body in _world.Bodies) body.Wake();
        };
        settingsBackButton.Click += (_, _) => ShowPauseMenu();
        settingsButton.Click += (_, _) => ShowSettingsMenu();
        var audioPanel = CreateAudioSettingsPanel();
        _settingsPanel.Controls.AddRange([
            _settingsFullscreen, _settingsDebug, _settingsGravity, audioPanel, settingsBackButton]);
        Controls.Add(_settingsPanel);

        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;
        Resize += (_, _) =>
        {
            _resizeEventCount++;
            LayoutOverlays();
            UpdateDisplayDiagnostics();
            _surface.Invalidate();
        };
        FormClosed += (_, _) =>
        {
            SaveFixtureLayout();
            _audio.Dispose();
            _frameGraphics.Dispose();
            _frameBuffer.Dispose();
            if (_highResolutionTimerActive)
            {
                timeEndPeriod(TimerResolutionMs);
                _highResolutionTimerActive = false;
            }
        };
        ResetScene();
        _settingsGravity.Checked = true;
        LayoutOverlays();
        Shown += (_, _) =>
        {
            _highResolutionTimerActive = timeBeginPeriod(TimerResolutionMs) == 0;
            UpdateDisplayDiagnostics();
            BeginLoop();
        };
    }

    private void RenderSurface(object? sender, PaintEventArgs e)
    {
        if (_world is null) return;
        var paintAllocationStart = GC.GetAllocatedBytesForCurrentThread();
        var viewport = WorldViewport;
        if (viewport.IsEmpty) return;
        _renderer.PaintClipWidth = e.ClipRectangle.Width;
        _renderer.PaintClipHeight = e.ClipRectangle.Height;
        var paintNow = _clock.Elapsed.TotalSeconds;
        _paintCount++;
        if (_lastPaintTime > 0d)
        {
            var paintInterval = paintNow - _lastPaintTime;
            var frameMs = Math.Max(0.001d, paintInterval * 1000d);
            _fpsSmoothing = _fpsSmoothing * 0.92d + (1000d / frameMs) * 0.08d;
            _renderer.FrameMs = frameMs;
            _renderer.Fps = _fpsSmoothing;
            _renderer.PaintJitterMs = Math.Abs(paintInterval - TargetRenderSeconds) * 1000d;
            if (paintInterval > TargetRenderSeconds * 1.5d) _paintSpikeCount++;
            _paintWindowSamples++;
            _paintWindowSumMs += frameMs;
            _paintWindowSumSquaredMs += frameMs * frameMs;
            _paintWindowMaximumMs = Math.Max(_paintWindowMaximumMs, frameMs);
            if (_paintWindowStart <= 0d) _paintWindowStart = paintNow;
            if (paintNow - _paintWindowStart >= 1d)
            {
                var mean = _paintWindowSumMs / Math.Max(1, _paintWindowSamples);
                var variance = _paintWindowSumSquaredMs / Math.Max(1, _paintWindowSamples) - mean * mean;
                _renderer.PaintMeanMs = mean;
                _renderer.PaintDeviationMs = Math.Sqrt(Math.Max(0d, variance));
                _renderer.PaintMaximumMs = _paintWindowMaximumMs;
                _paintWindowStart = paintNow;
                _paintWindowSamples = 0;
                _paintWindowSumMs = 0d;
                _paintWindowSumSquaredMs = 0d;
                _paintWindowMaximumMs = 0d;
            }
        }
        _lastPaintTime = paintNow;
        _renderer.PaintSpikeCount = _paintSpikeCount;
        _renderer.PaintCount = _paintCount;
        _renderer.RenderRequestCount = _renderRequestCount;
        var renderStart = Stopwatch.GetTimestamp();
        var tool = _world.Knife;
        var showPickupPrompt = _world.ProcessingLine?.Powered == true &&
                               tool is { Visible: true, IsGrabbed: false } &&
                               tool.HitTest(_input.MousePosition);

        // GameSurface already owns a double buffer. At the normal fixed logical
        // size, render straight into it and avoid drawing the whole world into a
        // second bitmap only to copy those same 921,600 pixels again.
        if (viewport.Size == LogicalViewport)
        {
            var state = e.Graphics.Save();
            e.Graphics.SetClip(viewport, CombineMode.Intersect);
            e.Graphics.TranslateTransform(viewport.Left, viewport.Top);
            var shake = tool?.ScreenShakeOffset ?? Vector2.Zero;
            e.Graphics.TranslateTransform(MathF.Round(shake.X), MathF.Round(shake.Y));
            e.Graphics.CompositingQuality = CompositingQuality.HighSpeed;
            e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            _renderer.Draw(e.Graphics, LogicalViewport, _world, _grabbed, _pendingSlice,
                showPickupPrompt ? tool!.Position : null);
            e.Graphics.Restore(state);
            _renderer.RenderMs = Stopwatch.GetElapsedTime(renderStart).TotalMilliseconds;
            _renderer.PresentMs = 0d;
            _renderer.UiAllocatedBytesPerPaint = Math.Max(0L,
                GC.GetAllocatedBytesForCurrentThread() - paintAllocationStart);
            return;
        }

        _frameGraphics.ResetTransform();
        _frameGraphics.Clear(Color.FromArgb(255, 9, 16, 21));
        var frameShake = tool?.ScreenShakeOffset ?? Vector2.Zero;
        _frameGraphics.TranslateTransform(MathF.Round(frameShake.X), MathF.Round(frameShake.Y));
        _renderer.Draw(_frameGraphics, LogicalViewport, _world, _grabbed, _pendingSlice,
            showPickupPrompt ? tool!.Position : null);
        var presentStart = Stopwatch.GetTimestamp();
        if (viewport.Size == _frameBuffer.Size)
        {
            e.Graphics.DrawImageUnscaled(_frameBuffer, viewport.Location);
        }
        else if (!TryPresentScaledFrame(e.Graphics, viewport))
        {
            // Keep a safe GDI+ fallback for unusual printer/remote device contexts.
            // Normal display presentation uses StretchBlt and never reaches here.
            _renderer.PresentationMode = "scaled GDI+ fallback";
            e.Graphics.CompositingMode = CompositingMode.SourceCopy;
            e.Graphics.CompositingQuality = CompositingQuality.HighSpeed;
            e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            e.Graphics.DrawImage(
                _frameBuffer,
                viewport,
                0,
                0,
                _frameBuffer.Width,
                _frameBuffer.Height,
                GraphicsUnit.Pixel);
        }
        var presentEnd = Stopwatch.GetTimestamp();
        _renderer.RenderMs = Stopwatch.GetElapsedTime(renderStart, presentStart).TotalMilliseconds;
        _renderer.PresentMs = Stopwatch.GetElapsedTime(presentStart, presentEnd).TotalMilliseconds;
        _renderer.UiAllocatedBytesPerPaint = Math.Max(0L,
            GC.GetAllocatedBytesForCurrentThread() - paintAllocationStart);
    }

    private bool TryPresentScaledFrame(Graphics destination, Rectangle viewport)
    {
        nint destinationDeviceContext = 0;
        BitmapData? frameData = null;
        try
        {
            // GDI+ DrawImage performs a costly per-pixel software resample here.
            // StretchDIBits reads the already-rendered managed bitmap directly,
            // retaining every raster layer while keeping fullscreen nearest-neighbor
            // presentation in native GDI.
            _frameGraphics.Flush(FlushIntention.Sync);
            frameData = _frameBuffer.LockBits(
                new Rectangle(0, 0, _frameBuffer.Width, _frameBuffer.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppPArgb);
            var bitmapInfo = new BitmapInfo
            {
                Header = new BitmapInfoHeader
                {
                    Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                    Width = _frameBuffer.Width,
                    Height = -_frameBuffer.Height,
                    Planes = 1,
                    BitCount = 32,
                    Compression = UncompressedRgbBitmap,
                    SizeImage = (uint)(Math.Abs(frameData.Stride) * _frameBuffer.Height)
                }
            };
            destinationDeviceContext = destination.GetHdc();
            SetStretchBltMode(destinationDeviceContext, ColorOnColorStretchMode);
            var copiedScanlines = StretchDIBits(
                destinationDeviceContext,
                viewport.Left,
                viewport.Top,
                viewport.Width,
                viewport.Height,
                0,
                0,
                _frameBuffer.Width,
                _frameBuffer.Height,
                frameData.Scan0,
                ref bitmapInfo,
                RgbColorTable,
                SourceCopyRasterOperation);
            if (copiedScanlines > 0) _renderer.PresentationMode = "scaled fast DIB";
            return copiedScanlines > 0;
        }
        finally
        {
            if (destinationDeviceContext != 0) destination.ReleaseHdc(destinationDeviceContext);
            if (frameData is not null) _frameBuffer.UnlockBits(frameData);
        }
    }

    private static Panel CreateMenuPanel(string title)
    {
        var panel = new Panel
        {
            Size = new Size(342, 286),
            BackColor = Color.FromArgb(20, 27, 35),
            BorderStyle = BorderStyle.FixedSingle,
            Visible = false
        };
        panel.Controls.Add(new Label
        {
            Text = title,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Bounds = new Rectangle(20, 18, 300, 40),
            ForeColor = Color.FromArgb(232, 239, 244),
            Font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        });
        return panel;
    }

    private static Button CreateMenuButton(string text, int top)
    {
        var button = new Button
        {
            Text = text,
            Bounds = new Rectangle(54, top, 232, 34),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(48, 59, 70),
            ForeColor = Color.FromArgb(232, 239, 244),
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(120, 141, 157);
        return button;
    }

    private static CheckBox CreateMenuCheckBox(string text, int top) => new()
    {
        Text = text,
        Bounds = new Rectangle(58, top, 226, 30),
        ForeColor = Color.FromArgb(225, 233, 239),
        Font = new Font("Segoe UI", 10f, FontStyle.Regular),
        FlatStyle = FlatStyle.Flat,
        Cursor = Cursors.Hand,
        TabStop = false
    };

    private Panel CreateAudioSettingsPanel()
    {
        var panel = new Panel
        {
            Bounds = new Rectangle(36, 198, 428, 156),
            BackColor = Color.FromArgb(15, 21, 27),
            BorderStyle = BorderStyle.FixedSingle
        };
        panel.Controls.Add(new Label
        {
            Text = "AUDIO MIX",
            Bounds = new Rectangle(14, 8, 280, 22),
            ForeColor = Color.FromArgb(205, 230, 232),
            Font = new Font("Consolas", 9f, FontStyle.Bold)
        });

        AddVolumeRow("MASTER", 31, _audio.MasterVolume, value => _audio.MasterVolume = value);
        AddVolumeRow("SFX", 70, _audio.SfxVolume, value => _audio.SfxVolume = value);
        AddVolumeRow("MUSIC", 109, _audio.MusicVolume, value => _audio.MusicVolume = value);
        return panel;

        void AddVolumeRow(string name, int top, int currentValue, Action<int> setVolume)
        {
            var label = new Label
            {
                Text = $"{name,-6} {currentValue:000}",
                Bounds = new Rectangle(14, top + 2, 108, 29),
                ForeColor = name == "MASTER"
                    ? Color.FromArgb(240, 205, 118)
                    : Color.FromArgb(101, 230, 223),
                Font = new Font("Consolas", 9f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            var slider = CreateVolumeSlider(currentValue, new Rectangle(124, top - 2, 286, 36));
            slider.Name = $"{name.ToLowerInvariant()}VolumeSlider";
            slider.AccessibleName = $"{name} volume";
            slider.ValueChanged += (_, _) =>
            {
                label.Text = $"{name,-6} {slider.Value:000}";
                setVolume(slider.Value);
            };
            panel.Controls.Add(label);
            panel.Controls.Add(slider);
        }
    }

    private static TrackBar CreateVolumeSlider(int value, Rectangle bounds) => new()
    {
        Bounds = bounds,
        Minimum = 0,
        Maximum = 100,
        TickFrequency = 10,
        SmallChange = 2,
        LargeChange = 10,
        Value = Math.Clamp(value, 0, 100),
        TabStop = false
    };

    private Rectangle WorldViewport => ViewportLayout.Fit(_surface.ClientSize, LogicalViewport);

    private void LayoutOverlays()
    {
        _pausePanel.Location = CenterOverlay(_pausePanel);
        _settingsPanel.Location = CenterOverlay(_settingsPanel);
        _fullscreenButton.BringToFront();
        _spawnButton.BringToFront();
        _conveyorButton.BringToFront();
        _lightButton.BringToFront();
        if (_pausePanel.Visible) _pausePanel.BringToFront();
        if (_settingsPanel.Visible) _settingsPanel.BringToFront();
    }

    private Point CenterOverlay(Control control) => new(
        Math.Max(0, (ClientSize.Width - control.Width) / 2),
        Math.Max(0, (ClientSize.Height - control.Height) / 2));

    private void ShowSettingsMenu()
    {
        _pausePanel.Visible = false;
        _settingsPanel.Visible = true;
        LayoutOverlays();
        _settingsPanel.BringToFront();
    }

    private void ShowPauseMenu()
    {
        _settingsPanel.Visible = false;
        _pausePanel.Visible = true;
        LayoutOverlays();
        _pausePanel.BringToFront();
    }

    private void SetPaused(bool paused)
    {
        _paused = paused;
        _accumulator = 0;
        _lastSimulationStepTime = 0d;
        if (paused)
        {
            if (_fixtureDragTarget != FixtureDragTarget.None) SaveFixtureLayout();
            _fixtureDragTarget = FixtureDragTarget.None;
            _fixtureDragMoved = false;
            _input.SetLeft(false);
            _input.SetRight(false);
            _grabbed?.EndGrab(Vector2.Zero, FixedDt);
            _grabbed = null;
            if (_world.Knife?.IsGrabbed == true)
                _world.Knife.EndGrab(Vector2.Zero, FixedDt);
            _toolPrimaryHeld = false;
            if (_toolRotationHeld) _world.Knife?.EndRotationAdjust();
            _toolRotationHeld = false;
            _rightDragging = false;
            _pendingSlice.Clear();
            _sliceTarget = null;
            _world.HoldingChamber?.EndLeverDrag();
            _draggingChamberLever = false;
            _world.ProcessingLine?.EndBreakerLeverDrag();
            _draggingBreakerLever = false;
            _world.ProcessingLine?.SetCrusherButtonHeld(false);
            _holdingCrusherButton = false;
            _world.ProcessingLine?.SetDrillLeverHeld(false);
            _holdingDrillLever = false;
            _world.ProcessingLine?.EndFilterDrag();
            _draggingFilterKnob = false;
            _world.ProcessingLine?.EndVacuumDrag();
            _draggingVacuumNozzle = false;
            _world.ProcessingLine?.EndDrumWheelDrag();
            _draggingDrumWheel = false;
            _lightEditHandle = LightEditHandle.None;
        }
        _settingsPanel.Visible = false;
        _pausePanel.Visible = paused;
        _spawnButton.Visible = !paused;
        _conveyorButton.Visible = !paused;
        _lightButton.Visible = !paused;
        _fullscreenButton.Visible = !paused;
        LayoutOverlays();
        if (!paused) _surface.Focus();
    }

    private void ToggleFullscreen()
    {
        _fullscreenToggleCount++;
        SuspendLayout();
        if (!_isFullscreen)
        {
            _windowedBounds = Bounds;
            WindowState = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.None;
            Bounds = Screen.FromControl(this).Bounds;
            _isFullscreen = true;
        }
        else
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            WindowState = FormWindowState.Normal;
            if (!_windowedBounds.IsEmpty) Bounds = _windowedBounds;
            _isFullscreen = false;
        }
        _fullscreenButton.Text = _isFullscreen ? "WINDOWED  [F11]" : "FULLSCREEN  [F11]";
        _settingsFullscreen.Checked = _isFullscreen;
        ResumeLayout(true);
        LayoutOverlays();
        UpdateDisplayDiagnostics();
        _surface.Invalidate();
    }

    private void UpdateDisplayDiagnostics()
    {
        if (_surface is null) return;
        var screen = Screen.FromControl(this);
        var viewport = WorldViewport;
        var refreshRate = 0;
        if (IsHandleCreated)
        {
            using var graphics = CreateGraphics();
            var deviceContext = graphics.GetHdc();
            try
            {
                refreshRate = GetDeviceCaps(deviceContext, VerticalRefreshDeviceCapability);
            }
            finally
            {
                graphics.ReleaseHdc(deviceContext);
            }
        }

        _renderer.DisplayWidth = screen.Bounds.Width;
        _renderer.DisplayHeight = screen.Bounds.Height;
        _renderer.DisplayRefreshHz = refreshRate;
        _renderer.DisplayDpi = DeviceDpi;
        _renderer.ClientWidth = ClientSize.Width;
        _renderer.ClientHeight = ClientSize.Height;
        _renderer.SurfaceWidth = _surface.ClientSize.Width;
        _renderer.SurfaceHeight = _surface.ClientSize.Height;
        _renderer.InternalRenderWidth = WorldWidth;
        _renderer.InternalRenderHeight = WorldHeight;
        _renderer.ViewportWidth = viewport.Width;
        _renderer.ViewportHeight = viewport.Height;
        _renderer.FullscreenMode = _isFullscreen ? "borderless" : "windowed";
        _renderer.PresentationMode = viewport.Size == LogicalViewport ? "native direct" : "scaled fast blit";
        _renderer.ResizeEventCount = _resizeEventCount;
        _renderer.FullscreenToggleCount = _fullscreenToggleCount;
    }

    private void ResetScene()
    {
        _audio.StopAll();
        var cellSize = 32;
        var grid = new DestructibleGrid(WorldWidth / cellSize, WorldHeight / cellSize, cellSize);
        grid.BuildProcessingStation();
        _world = new BlobWorld(grid);
        _world.Lighting.ConfigureProcessingStation();
        _world.Lighting.SetFactoryPower(false);
        _world.HoldingChamber = null;
        _chamberFeed = null;
        _world.ProcessingLine = new ProcessingLine(
            DestructibleGrid.ProcessingDeckRow * cellSize,
            powered: false,
            breakerPosition: _fixtureLayout.BreakerBoxPosition,
            continuousFlow: true);
        _world.TubeFeed = new OverheadTubeFeed(_world.ProcessingLine.DeckY);
        grid.OpenContinuousConveyorPortals();
        _world.Knife = new PhysicalKnife(_world.ProcessingLine.ContinuousToolRackCenter);
        _world.ProcessingLine.SetBreakerPosition(
            new Vector2(_world.ProcessingLine.BreakerBounds.X, _world.ProcessingLine.BreakerBounds.Y),
            WorldWidth, WorldHeight);
        _world.Conveyors.AddRange(_world.ProcessingLine.Belts);
        _selectedConveyor = null;
        _selectedLight = null;
        _lightEditHandle = LightEditHandle.None;
        _grabbed = null;
        _draggingChamberLever = false;
        _draggingBreakerLever = false;
        _holdingCrusherButton = false;
        _holdingDrillLever = false;
        _draggingFilterKnob = false;
        _draggingVacuumNozzle = false;
        _draggingDrumWheel = false;
        _rightDragging = false;
        _pendingSlice.Clear();
        _sliceTarget = null;
        _accumulator = 0;
        _observedFactoryPower = false;
        _factoryStartupDelay = -1f;
        _fixtureDragTarget = FixtureDragTarget.None;
        _fixtureDragMoved = false;
        _toolPrimaryHeld = false;
        _toolRotationHeld = false;
        _toolEquipKeyHeld = false;
        _arsenalMenuConsumedClick = false;
        _renderer.ArsenalMenuOpen = false;
        _spawnButton.Enabled = false;
        _spawnButton.Visible = false;
    }

    private async void BeginLoop()
    {
        _lastTime = _clock.Elapsed.TotalSeconds;
        _lastPumpTime = _lastTime;
        _nextRenderTime = _lastTime;
        while (!IsDisposed && Visible)
        {
            var now = _clock.Elapsed.TotalSeconds;
            _maximumPumpGapSincePaint = Math.Max(_maximumPumpGapSincePaint, now - _lastPumpTime);
            _lastPumpTime = now;
            var frame = Math.Min(now - _lastTime, 0.1);
            _lastTime = now;
            var fixedUpdateStart = Stopwatch.GetTimestamp();
            var fixedUpdateAllocationStart = GC.GetAllocatedBytesForCurrentThread();
            var stepsThisPump = 0;

            if (!_paused) _accumulator += frame;
            else _accumulator = 0;

            while (!_paused && _accumulator >= FixedDt && stepsThisPump < MaxCatchUpStepsPerPump)
            {
                var simulationStepTime = _clock.Elapsed.TotalSeconds;
                if (_lastSimulationStepTime > 0d)
                    _maximumSimulationGapSincePaint = Math.Max(
                        _maximumSimulationGapSincePaint,
                        simulationStepTime - _lastSimulationStepTime);
                _lastSimulationStepTime = simulationStepTime;
                FixedUpdate(FixedDt);
                _accumulator -= FixedDt;
                stepsThisPump++;
            }

            if (_accumulator >= FixedDt)
            {
                var skipped = (int)(_accumulator / FixedDt);
                _world.SkippedSteps += skipped;
                _accumulator %= FixedDt;
            }

            _fixedUpdateMsSinceRender += Stopwatch.GetElapsedTime(fixedUpdateStart).TotalMilliseconds;
            _simulationAllocatedBytesSinceRender += Math.Max(0L,
                GC.GetAllocatedBytesForCurrentThread() - fixedUpdateAllocationStart);
            _stepsSinceRender += stepsThisPump;
            _maximumStepBatchSinceRender = Math.Max(_maximumStepBatchSinceRender, stepsThisPump);

            var renderNow = _clock.Elapsed.TotalSeconds;
            if (renderNow >= _nextRenderTime)
            {
                var lateness = renderNow - _nextRenderTime;
                _maximumRenderLatenessSincePaint = Math.Max(_maximumRenderLatenessSincePaint, lateness);
                var deadlinesAdvanced = 0;
                do
                {
                    _nextRenderTime += TargetRenderSeconds;
                    deadlinesAdvanced++;
                } while (_nextRenderTime <= renderNow);
                _missedRenderDeadlines += Math.Max(0, deadlinesAdvanced - 1);

                _world.StepsThisFrame = _stepsSinceRender;
                _renderer.FixedUpdateMs = _fixedUpdateMsSinceRender;
                _renderer.SimulationAllocatedBytesPerFrame = _simulationAllocatedBytesSinceRender;
                _renderer.AudioUpdateMs = _audioUpdateMsThisFrame;
                _renderer.HostPumpGapMs = _maximumPumpGapSincePaint * 1000d;
                _renderer.SimulationGapMs = _maximumSimulationGapSincePaint * 1000d;
                _renderer.RenderDeadlineLateMs = _maximumRenderLatenessSincePaint * 1000d;
                _renderer.MaxStepBatch = _maximumStepBatchSinceRender;
                _renderer.RenderDeadlineMisses = _missedRenderDeadlines;

                _stepsSinceRender = 0;
                _maximumStepBatchSinceRender = 0;
                _fixedUpdateMsSinceRender = 0d;
                _simulationAllocatedBytesSinceRender = 0L;
                _audioUpdateMsThisFrame = 0d;
                _maximumPumpGapSincePaint = 0d;
                _maximumSimulationGapSincePaint = 0d;
                _maximumRenderLatenessSincePaint = 0d;
                _renderRequestCount++;
                _surface.Invalidate(WorldViewport);
            }

            // Keep simulation/input sampling independent from the 60 Hz paint cap.
            // A short UI-thread yield lets 120 Hz fixed ticks land near 8.33 ms
            // instead of batching two of them after one coarse 16.67 ms delay.
            await Task.Delay(1);
        }
    }

    private void FixedUpdate(float dt)
    {
        _world.Gravity = _gravityEnabled ? new Vector2(0f, 980f) : Vector2.Zero;
        var line = _world.ProcessingLine;
        if (line?.Powered == true && _observedFactoryPower)
        {
            if (_factoryStartupDelay > 0f)
            {
                _factoryStartupDelay -= dt;
                if (_factoryStartupDelay <= 0f) _factoryStartupDelay = 0f;
            }
            else
            {
                var spawned = _world.TubeFeed?.Update(_world.Bodies, dt, StationUnit.Create);
                if (spawned is not null) _audio.Play(SoundCue.BlobDrop);
            }
        }
        if (_grabbed is not null)
        {
            var target = _world.ConstrainGrabTarget(_grabbed, _input.MousePosition);
            _grabbed.UpdateGrabTarget(target, dt);
        }
        _world.Step(dt);
        if (_grabbed is { IsGrabbed: false }) _grabbed = null;
        if (line?.Powered == true && !_observedFactoryPower)
        {
            _observedFactoryPower = true;
            _factoryStartupDelay = 0.82f;
            _world.Lighting.SetFactoryPower(true);
            _audio.SetLooping(SoundCue.FactoryHum, true);
            _audio.SetLooping(SoundCue.Conveyor, true);
            _spawnButton.Enabled = false;
        }
        var audioStart = Stopwatch.GetTimestamp();
        UpdateMachineAudio();
        _audioUpdateMsThisFrame += Stopwatch.GetElapsedTime(audioStart).TotalMilliseconds;
    }

    private void UpdateMachineAudio()
    {
        var line = _world.ProcessingLine;
        var powered = line?.Powered == true;
        var machineryAvailable = powered && line!.MachineryLockedByStorage == false;
        _audio.SetLooping(SoundCue.Crusher,
            machineryAvailable && line!.CrusherButtonHeld && line.LockedBody is not null);
        _audio.SetLooping(SoundCue.Drill,
            machineryAvailable && line!.DrillLeverHeld && line.DrillLockedBody is not null);
        _audio.SetLooping(SoundCue.Vacuum,
            machineryAvailable && line!.VacuumHose.IsDragging && line.VacuumLockedBody is not null);
        _audio.SetLooping(SoundCue.Filter,
            machineryAvailable && line!.FilterDragging && line.FilterLockedBody is not null);
        _audio.SetLooping(SoundCue.Press,
            machineryAvailable && line!.DrumLockedBody is not null && MathF.Abs(line.DrumAngularSpeed) > 0.4f);
    }

    private void SurfaceOnMouseMove(object? sender, MouseEventArgs e)
    {
        if (_paused) return;
        var point = ToWorld(e.Location);
        _input.SetMouse(point);
        if (_renderer.ArsenalMenuOpen)
        {
            var hovered = _renderer.HitTestArsenalMenu(point);
            if (hovered >= 0) _renderer.ArsenalMenuSelection = hovered;
            _surface.Cursor = hovered >= 0 ? Cursors.Hand : Cursors.Default;
            return;
        }
        if (_toolRotationHeld && _world.Knife is { } rotatingTool)
        {
            rotatingTool.UpdateRotationAdjust(point);
            _surface.Cursor = Cursors.Cross;
            return;
        }
        if (_toolPrimaryHeld && _world.Knife?.IsDeployed == true)
        {
            _world.Knife.SetGrabTarget(point);
            _surface.Cursor = Cursors.Hand;
            return;
        }
        if (_world.Knife?.IsGrabbed == true)
        {
            _world.Knife.SetGrabTarget(point);
            _surface.Cursor = Cursors.Hand;
            return;
        }
        if (_draggingBreakerLever && _world.ProcessingLine is { } breakerLine)
        {
            if (breakerLine.DragBreakerLever(point)) _audio.Play(SoundCue.Breaker);
            _surface.Cursor = Cursors.Hand;
            return;
        }
        if (_input.LeftDown && _fixtureDragTarget != FixtureDragTarget.None)
        {
            _fixtureDragMoved |= Vector2.DistanceSquared(point, _fixtureDragStart) >= 4f * 4f;
            var position = point - _fixtureDragOffset;
            if (_fixtureDragTarget == FixtureDragTarget.BlobCounter)
                _world.HoldingChamber?.SetCounterPosition(position, WorldWidth, WorldHeight);
            else
                _world.ProcessingLine?.SetBreakerPosition(position, WorldWidth, WorldHeight);
            _surface.Cursor = Cursors.SizeAll;
            return;
        }
        _surface.Cursor = _world.ProcessingLine?.HitBreakerLever(point) == true ||
                          _world.ProcessingLine?.HitDrumWheel(point) == true ||
                          _world.ProcessingLine?.HitBloodShop(point) == true ||
                          _world.Knife?.HitTest(point) == true
            ? Cursors.Hand
            : _world.ProcessingLine?.HitBreaker(point) == true ||
              _world.HoldingChamber?.HitCounter(point) == true
                ? Cursors.SizeAll
                : Cursors.Default;
        if (_draggingChamberLever && _world.HoldingChamber is not null)
        {
            _world.HoldingChamber.UpdateLeverDrag(point);
            return;
        }
        if (_draggingFilterKnob && _world.ProcessingLine is not null)
        {
            _world.ProcessingLine.DragFilterKnob(point.X);
            return;
        }
        if (_draggingDrumWheel && _world.ProcessingLine is not null)
        {
            _world.ProcessingLine.DragDrumWheel(point);
            _surface.Cursor = Cursors.Hand;
            return;
        }
        if (_draggingVacuumNozzle && _world.ProcessingLine is not null)
        {
            _world.ProcessingLine.DragVacuumNozzle(point);
            return;
        }
        if (_input.LeftDown && _selectedLight is not null && _lightEditHandle != LightEditHandle.None)
        {
            var delta = point - _lightEditLast;
            if (_lightEditHandle == LightEditHandle.Move)
                _selectedLight.Move(delta, WorldWidth);
            else if (_lightEditHandle == LightEditHandle.CableLength)
                _selectedLight.SetCableFromPointer(point);
            else if (_lightEditHandle == LightEditHandle.Range)
                _selectedLight.SetRangeFromPointer(point);
            _lightEditLast = point;
            _world.Lighting.NotifyEdited();
            return;
        }
        if (_input.LeftDown && _grabbed is null && _selectedConveyor is not null &&
            _conveyorEditHandle != ConveyorEditHandle.None)
        {
            var delta = point - _conveyorEditLast;
            if (_conveyorEditHandle == ConveyorEditHandle.Move)
                _selectedConveyor.Move(delta, WorldWidth, WorldHeight);
            else if (_conveyorEditHandle == ConveyorEditHandle.Length)
                _selectedConveyor.Resize(delta.X, 0f, WorldWidth, WorldHeight);
            else if (_conveyorEditHandle == ConveyorEditHandle.Height)
                _selectedConveyor.Resize(0f, delta.Y, WorldWidth, WorldHeight);
            _conveyorEditLast = point;
            foreach (var body in _world.Bodies) body.Wake();
            return;
        }
        if (!_input.RightDown) return;
        TrackSliceGesture(point);
    }

    private void SurfaceOnMouseDown(object? sender, MouseEventArgs e)
    {
        if (_paused || !WorldViewport.Contains(e.Location)) return;
        _input.SetMouse(ToWorld(e.Location));
        if (_renderer.ArsenalMenuOpen)
        {
            _arsenalMenuConsumedClick = true;
            if (e.Button == MouseButtons.Left)
            {
                var selected = _renderer.HitTestArsenalMenu(_input.MousePosition);
                if (selected >= 0)
                {
                    _renderer.ArsenalMenuSelection = selected;
                    ConfirmArsenalSelection();
                }
            }
            _surface.Focus();
            return;
        }
        if (e.Button == MouseButtons.Left)
        {
            _input.SetLeft(true);
            if (_toolRotationHeld)
            {
                _surface.Focus();
                return;
            }
            if (_world.ProcessingLine?.Powered == true &&
                _world.Knife is { IsDeployed: true, ArsenalVisualVariant: 8 } deployedSling &&
                deployedSling.CanBeginSlingshotPull(_input.MousePosition))
            {
                deployedSling.SetGrabTarget(_input.MousePosition);
                _toolPrimaryHeld = deployedSling.BeginPrimaryAction();
                _surface.Cursor = Cursors.Hand;
                _surface.Focus();
                return;
            }
            if (_world.ProcessingLine?.Powered == true && _world.Knife?.IsEquipped == true)
            {
                if (_world.Knife.PlaceAtPreview())
                {
                    _toolPrimaryHeld = false;
                    _surface.Cursor = Cursors.Default;
                    _surface.Focus();
                    return;
                }
                _toolPrimaryHeld = _world.Knife.BeginPrimaryAction();
                _surface.Cursor = Cursors.Hand;
                _surface.Focus();
                return;
            }
            if (_world.ProcessingLine is { } breakerLine &&
                breakerLine.BeginBreakerLeverDrag(_input.MousePosition))
            {
                _draggingBreakerLever = true;
                _grabbed = null;
                _conveyorEditHandle = ConveyorEditHandle.None;
                _surface.Cursor = Cursors.Hand;
                _surface.Focus();
                return;
            }
            if (_world.ProcessingLine is { } powerLine && powerLine.HitBreaker(_input.MousePosition))
            {
                BeginFixtureDrag(FixtureDragTarget.BreakerBox, powerLine.BreakerBounds);
                return;
            }
            if (_world.HoldingChamber is { } chamber && chamber.HitCounter(_input.MousePosition))
            {
                BeginFixtureDrag(FixtureDragTarget.BlobCounter, chamber.CounterBounds);
                return;
            }
            ClearFixtureSelection();
            if (_world.ProcessingLine?.Powered != true)
            {
                _surface.Focus();
                return;
            }
            if (_world.Knife?.BeginGrab(_input.MousePosition) == true)
            {
                _grabbed = null;
                _conveyorEditHandle = ConveyorEditHandle.None;
                _surface.Cursor = Cursors.Hand;
                _surface.Focus();
                return;
            }
            if (_world.ProcessingLine is { } shopLine && shopLine.HitBloodShop(_input.MousePosition))
            {
                shopLine.TryActivateBloodShop(_input.MousePosition);
                _grabbed = null;
                _conveyorEditHandle = ConveyorEditHandle.None;
                _surface.Cursor = Cursors.Hand;
                _surface.Focus();
                return;
            }
            if (_world.ProcessingLine is not null && _world.ProcessingLine.HitCart(_input.MousePosition))
            {
                if (_world.ProcessingLine.TryDispatchCart(_world.Bodies))
                    _audio.Play(SoundCue.Cart);
                _grabbed = null;
                _conveyorEditHandle = ConveyorEditHandle.None;
                _surface.Focus();
                return;
            }
            if (_world.ProcessingLine is not null && _world.ProcessingLine.HitCrusherButton(_input.MousePosition))
            {
                _holdingCrusherButton = true;
                _world.ProcessingLine.SetCrusherButtonHeld(true);
                _grabbed = null;
                _conveyorEditHandle = ConveyorEditHandle.None;
                _surface.Focus();
                return;
            }
            if (_world.ProcessingLine is not null && _world.ProcessingLine.HitDrillLever(_input.MousePosition))
            {
                _holdingDrillLever = true;
                _world.ProcessingLine.SetDrillLeverHeld(true);
                _grabbed = null;
                _conveyorEditHandle = ConveyorEditHandle.None;
                _surface.Focus();
                return;
            }
            if (_world.ProcessingLine is not null && _world.ProcessingLine.BeginDrumWheelDrag(_input.MousePosition))
            {
                _draggingDrumWheel = true;
                _grabbed = null;
                _conveyorEditHandle = ConveyorEditHandle.None;
                _surface.Cursor = Cursors.Hand;
                _surface.Focus();
                return;
            }
            if (_world.ProcessingLine is not null && _world.ProcessingLine.BeginVacuumDrag(_input.MousePosition))
            {
                _draggingVacuumNozzle = true;
                _grabbed = null;
                _conveyorEditHandle = ConveyorEditHandle.None;
                _surface.Focus();
                return;
            }
            if (_world.ProcessingLine is not null && _world.ProcessingLine.BeginFilterDrag(_input.MousePosition))
            {
                _draggingFilterKnob = true;
                _grabbed = null;
                _conveyorEditHandle = ConveyorEditHandle.None;
                _surface.Focus();
                return;
            }
            if (_world.HoldingChamber is not null && _world.HoldingChamber.HitLever(_input.MousePosition))
            {
                _draggingChamberLever = true;
                _world.HoldingChamber.BeginLeverDrag(_input.MousePosition);
                _audio.Play(SoundCue.Chamber);
                _grabbed = null;
                _conveyorEditHandle = ConveyorEditHandle.None;
                _surface.Focus();
                return;
            }
            var light = _world.Lighting.HitTest(_input.MousePosition);
            if (light is not null)
            {
                SelectLight(light);
                _lightEditHandle = light.HitEditHandle(_input.MousePosition);
                if (_lightEditHandle == LightEditHandle.None) _lightEditHandle = LightEditHandle.Move;
                _lightEditLast = _input.MousePosition;
                _grabbed = null;
                _conveyorEditHandle = ConveyorEditHandle.None;
                _surface.Focus();
                return;
            }
            _grabbed = _world.PickBody(_input.MousePosition);
            if (_grabbed is not null)
            {
                DeselectLights();
                _grabbed.BeginGrab(_input.MousePosition);
                _conveyorEditHandle = ConveyorEditHandle.None;
            }
            else
            {
                var conveyor = _world.Conveyors.LastOrDefault(candidate =>
                    !candidate.IsSystemControlled && candidate.ContainsPoint(_input.MousePosition, 10f));
                if (conveyor is not null)
                {
                    DeselectLights();
                    foreach (var existing in _world.Conveyors) existing.IsSelected = false;
                    _selectedConveyor = conveyor;
                    conveyor.IsSelected = true;
                    _conveyorEditHandle = conveyor.HitEditHandle(_input.MousePosition);
                    _conveyorEditLast = _input.MousePosition;
                }
                else
                {
                    DeselectLights();
                }
            }
        }
        else if (e.Button == MouseButtons.Right)
        {
            if (_world.ProcessingLine?.Powered == true && _world.Knife is { IsEquipped: true } tool &&
                tool.BeginRotationAdjust(_input.MousePosition))
            {
                _toolPrimaryHeld = false;
                _input.SetRight(true);
                _toolRotationHeld = true;
                _surface.Cursor = Cursors.Cross;
                _surface.Focus();
                return;
            }
            if (_world.ProcessingLine?.ContinuousFlowMode == true) return;
            _input.SetRight(true);
            _rightGestureStart = _input.MousePosition;
            _rightDragging = false;
            _pendingSlice.Clear();
            _sliceTarget = _world.Bodies.FirstOrDefault(body => body.ContainsVisiblePoint(_rightGestureStart));
            _sliceWasInside = _sliceTarget is not null;
            _sliceInsideDistance = 0f;
        }
        _surface.Focus();
    }

    private void SurfaceOnMouseUp(object? sender, MouseEventArgs e)
    {
        if (_paused) return;
        _input.SetMouse(ToWorld(e.Location));
        if (_arsenalMenuConsumedClick)
        {
            if (e.Button == MouseButtons.Left) _input.SetLeft(false);
            _arsenalMenuConsumedClick = false;
            return;
        }
        if (_renderer.ArsenalMenuOpen) return;
        if (e.Button == MouseButtons.Left)
        {
            _input.SetLeft(false);
            if (_toolPrimaryHeld && _world.Knife is { } activeTool)
            {
                activeTool.EndPrimaryAction();
                _toolPrimaryHeld = false;
                _surface.Cursor = Cursors.Hand;
                return;
            }
            if (_world.Knife?.IsEquipped == true)
            {
                _toolPrimaryHeld = false;
                _surface.Cursor = Cursors.Hand;
                return;
            }
            if (_world.Knife?.IsGrabbed == true)
            {
                _world.Knife.EndGrab(_input.GetMouseVelocity(), FixedDt);
                _surface.Cursor = Cursors.Default;
                return;
            }
            if (_draggingBreakerLever)
            {
                if (_world.ProcessingLine?.DragBreakerLever(_input.MousePosition) == true)
                    _audio.Play(SoundCue.Breaker);
                _world.ProcessingLine?.EndBreakerLeverDrag();
                _draggingBreakerLever = false;
                _surface.Cursor = Cursors.Default;
                return;
            }
            if (_fixtureDragTarget != FixtureDragTarget.None)
            {
                _fixtureDragTarget = FixtureDragTarget.None;
                _surface.Cursor = Cursors.Default;
                SaveFixtureLayout();
                _fixtureDragMoved = false;
                return;
            }
            if (_holdingCrusherButton)
            {
                _world.ProcessingLine?.SetCrusherButtonHeld(false);
                _holdingCrusherButton = false;
                return;
            }
            if (_holdingDrillLever)
            {
                _world.ProcessingLine?.SetDrillLeverHeld(false);
                _holdingDrillLever = false;
                return;
            }
            if (_draggingDrumWheel)
            {
                _world.ProcessingLine?.DragDrumWheel(_input.MousePosition);
                _world.ProcessingLine?.EndDrumWheelDrag();
                _draggingDrumWheel = false;
                _surface.Cursor = Cursors.Default;
                return;
            }
            if (_draggingFilterKnob)
            {
                _world.ProcessingLine?.DragFilterKnob(_input.MousePosition.X);
                _world.ProcessingLine?.EndFilterDrag();
                _draggingFilterKnob = false;
                return;
            }
            if (_draggingVacuumNozzle)
            {
                _world.ProcessingLine?.DragVacuumNozzle(_input.MousePosition);
                _world.ProcessingLine?.EndVacuumDrag(_input.MousePosition);
                _draggingVacuumNozzle = false;
                return;
            }
            if (_draggingChamberLever)
            {
                _world.HoldingChamber?.EndLeverDrag();
                _draggingChamberLever = false;
                return;
            }
            _grabbed?.EndGrab(_input.GetMouseVelocity(), FixedDt);
            _grabbed = null;
            _conveyorEditHandle = ConveyorEditHandle.None;
            _lightEditHandle = LightEditHandle.None;
        }
        else if (e.Button == MouseButtons.Right)
        {
            if (_toolRotationHeld && _world.Knife is { } tool)
            {
                tool.UpdateRotationAdjust(_input.MousePosition);
                tool.EndRotationAdjust();
                _toolRotationHeld = false;
                _input.SetRight(false);
                _surface.Cursor = Cursors.Hand;
                return;
            }
            if (_world.ProcessingLine?.ContinuousFlowMode == true) return;
            var thresholdSq = DamageGestureProfile.DragThreshold * DamageGestureProfile.DragThreshold;
            var dragged = _rightDragging ||
                          Vector2.DistanceSquared(_rightGestureStart, _input.MousePosition) >= thresholdSq;
            if (dragged)
            {
                TrackSliceGesture(_input.MousePosition);
                CommitPendingSlice();
            }
            else
            {
                ApplyBite(_input.MousePosition);
            }
            _input.SetRight(false);
            _rightDragging = false;
            _pendingSlice.Clear();
            _sliceTarget = null;
        }
    }

    private void SurfaceOnMouseWheel(object? sender, MouseEventArgs e)
    {
        if (_paused || !WorldViewport.Contains(e.Location)) return;
        var point = ToWorld(e.Location);
        var light = _world.Lighting.HitTest(point);
        if (light is not null)
        {
            SelectLight(light);
            if ((ModifierKeys & Keys.Shift) != 0) light.AdjustStrength(MathF.Sign(e.Delta) * 0.04f);
            else light.AdjustCable(-MathF.Sign(e.Delta) * 8f);
            _world.Lighting.NotifyEdited();
            _surface.Focus();
            return;
        }
        var conveyor = _world.Conveyors.LastOrDefault(candidate =>
            !candidate.IsSystemControlled && candidate.ContainsPoint(point, 8f));
        if (conveyor is null) return;
        foreach (var existing in _world.Conveyors) existing.IsSelected = false;
        _selectedConveyor = conveyor;
        conveyor.IsSelected = true;
        conveyor.ChangeSpeed(MathF.Sign(e.Delta) * 30f);
        _surface.Focus();
    }

    private void SurfaceOnMouseDoubleClick(object? sender, MouseEventArgs e)
    {
        if (_paused || e.Button != MouseButtons.Left || !WorldViewport.Contains(e.Location)) return;
        var conveyor = _world.Conveyors.LastOrDefault(candidate =>
            !candidate.IsSystemControlled && candidate.ContainsPoint(ToWorld(e.Location), 8f));
        if (conveyor is null) return;
        conveyor.Reverse();
    }

    private void TrackSliceGesture(Vector2 point)
    {
        var thresholdSq = DamageGestureProfile.DragThreshold * DamageGestureProfile.DragThreshold;
        if (!_rightDragging)
        {
            if (Vector2.DistanceSquared(_rightGestureStart, point) < thresholdSq) return;
            _rightDragging = true;
            _pendingSlice.Add(_rightGestureStart);
        }
        if (_pendingSlice.Count == 0) _pendingSlice.Add(_rightGestureStart);
        var previous = _pendingSlice[^1];
        if (Vector2.DistanceSquared(previous, point) < 0.25f) return;
        _pendingSlice.Add(point);

        if (_sliceTarget is null)
        {
            _sliceTarget = _world.Bodies.FirstOrDefault(body =>
                body.ContainsVisiblePoint(point) || SegmentTouchesBody(body, previous, point));
            if (_sliceTarget is not null) _sliceWasInside = true;
            return;
        }

        var inside = _sliceTarget.ContainsVisiblePoint(point);
        if (_sliceWasInside) _sliceInsideDistance += Vector2.Distance(previous, point);
        var crossedAnotherEdge = _sliceWasInside && !inside &&
                                 _sliceInsideDistance >= _sliceTarget.ParticleSpacing * 1.1f;
        _sliceWasInside = inside;
        if (!crossedAnotherEdge) return;
        CommitPendingSlice();
        _pendingSlice.Add(point);
        _sliceTarget = null;
        _sliceInsideDistance = 0f;
    }

    private static bool SegmentTouchesBody(SoftBody body, Vector2 start, Vector2 end)
    {
        var length = Vector2.Distance(start, end);
        var samples = Math.Max(2, (int)MathF.Ceiling(length / MathF.Max(2f, body.ParticleSpacing * 0.35f)));
        for (var i = 0; i <= samples; i++)
            if (body.ContainsVisiblePoint(Vector2.Lerp(start, end, i / (float)samples))) return true;
        return false;
    }

    private void CommitPendingSlice()
    {
        if (_pendingSlice.Count < 2) return;
        foreach (var body in _world.Bodies) DamageGestureProfile.SlicePath(body, _pendingSlice);
        for (var i = 1; i < _pendingSlice.Count; i++)
            _world.Grid.CarveCircle(_pendingSlice[i], DamageGestureProfile.SliceTerrainRadius, 1.4f);
        _pendingSlice.Clear();
    }

    private void ApplySlice(Vector2 start, Vector2 end)
    {
        if (Vector2.DistanceSquared(start, end) < 0.25f) return;
        foreach (var body in _world.Bodies) DamageGestureProfile.Slice(body, start, end);
        _world.Grid.CarveCircle(end, DamageGestureProfile.SliceTerrainRadius, 1.4f);
    }

    private void ApplyBite(Vector2 point)
    {
        foreach (var body in _world.Bodies) DamageGestureProfile.Bite(body, point);
        _world.Grid.CarveCircle(point, DamageGestureProfile.BiteTerrainRadius, 2.2f);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_renderer.ArsenalMenuOpen && e.KeyCode == Keys.Escape)
        {
            CloseArsenalMenu();
            e.SuppressKeyPress = true;
            return;
        }
        if (e.KeyCode == Keys.Escape)
        {
            if (_settingsPanel.Visible)
            {
                ShowPauseMenu();
            }
            else
            {
                SetPaused(!_paused);
            }
            e.SuppressKeyPress = true;
            return;
        }
        if (e.KeyCode == Keys.F11 || e.KeyCode == Keys.Enter && e.Alt)
        {
            ToggleFullscreen();
            e.SuppressKeyPress = true;
            return;
        }
        if (_paused) return;

        if (e.KeyCode == Keys.I)
        {
            if (_renderer.ArsenalMenuOpen) CloseArsenalMenu();
            else OpenArsenalMenu();
            e.SuppressKeyPress = true;
            return;
        }

        if (_renderer.ArsenalMenuOpen)
        {
            switch (e.KeyCode)
            {
                case Keys.Left:
                    MoveArsenalSelection(-1);
                    break;
                case Keys.Right:
                    MoveArsenalSelection(1);
                    break;
                case Keys.Up:
                    MoveArsenalSelection(-5);
                    break;
                case Keys.Down:
                    MoveArsenalSelection(5);
                    break;
                case Keys.Enter:
                    ConfirmArsenalSelection();
                    break;
            }
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode == Keys.E)
        {
            if (!_toolEquipKeyHeld)
            {
                _toolEquipKeyHeld = true;
                if (_world.Knife?.TryPickupBaseball(_input.MousePosition) != true)
                    ToggleEquippedTool();
            }
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode == Keys.Z && _world.Knife?.DeigniteSaber() == true)
        {
            _toolPrimaryHeld = false;
            e.SuppressKeyPress = true;
            return;
        }

        switch (e.KeyCode)
        {
            case Keys.R:
                ResetScene();
                break;
            case Keys.G:
                _gravityEnabled = !_gravityEnabled;
                _settingsGravity.Checked = _gravityEnabled;
                foreach (var body in _world.Bodies) body.Wake();
                break;
            case Keys.M:
                _renderer.DebugDraw = !_renderer.DebugDraw;
                _settingsDebug.Checked = _renderer.DebugDraw;
                break;
            case Keys.A:
                _world.Knife?.RotateBaseBy(-MathF.PI / 24f);
                break;
            case Keys.D:
                _world.Knife?.RotateBaseBy(MathF.PI / 24f);
                break;
            case Keys.B:
                SpawnBlob();
                break;
            case Keys.C:
                SpawnConveyor();
                break;
            case Keys.L:
                SpawnLantern();
                break;
            case Keys.Delete:
                DeleteSelectedLantern();
                break;
            case Keys.Tab:
                SelectNextConveyor();
                break;
            case Keys.Left:
                if (_selectedLight is not null) EditLantern(-8f, 0f, e.Shift);
                else EditConveyor(e.Shift ? -12f : -8f, 0f, e.Shift);
                break;
            case Keys.Right:
                if (_selectedLight is not null) EditLantern(8f, 0f, e.Shift);
                else EditConveyor(e.Shift ? 12f : 8f, 0f, e.Shift);
                break;
            case Keys.Up:
                if (_selectedLight is not null) EditLantern(0f, -8f, e.Shift);
                else EditConveyor(0f, e.Shift ? -6f : -8f, e.Shift);
                break;
            case Keys.Down:
                if (_selectedLight is not null) EditLantern(0f, 8f, e.Shift);
                else EditConveyor(0f, e.Shift ? 6f : 8f, e.Shift);
                break;
            case Keys.Oemplus:
            case Keys.Add:
                if (_selectedLight is not null)
                {
                    _selectedLight.AdjustRange(24f);
                    _world.Lighting.NotifyEdited();
                }
                else _selectedConveyor?.ChangeSpeed(30f);
                break;
            case Keys.OemMinus:
            case Keys.Subtract:
                if (_selectedLight is not null)
                {
                    _selectedLight.AdjustRange(-24f);
                    _world.Lighting.NotifyEdited();
                }
                else _selectedConveyor?.ChangeSpeed(-30f);
                break;
            case Keys.V:
                _selectedConveyor?.Reverse();
                break;
            case Keys.Space:
                foreach (var body in _world.Bodies) body.AddImpulse(new Vector2(0, -720), FixedDt);
                break;
            case Keys.F9:
                ApplyDiagnosticSlice(new Vector2(1f, 0f));
                break;
            case Keys.F10:
                ApplyDiagnosticSlice(Vector2.Normalize(new Vector2(1f, 0.55f)));
                break;
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.E) _toolEquipKeyHeld = false;
    }

    private void ToggleEquippedTool()
    {
        if (_world.ProcessingLine?.Powered != true || _world.Knife is not { } tool) return;
        if (tool.IsEquipped)
        {
            if (_toolRotationHeld)
            {
                tool.EndRotationAdjust();
                _toolRotationHeld = false;
                _input.SetRight(false);
            }
            if (_toolPrimaryHeld) tool.EndPrimaryAction();
            _toolPrimaryHeld = false;
            tool.EndGrab(_input.GetMouseVelocity(), FixedDt);
            _surface.Cursor = Cursors.Default;
            return;
        }
        if (tool.IsGrabbed || !tool.HitTest(_input.MousePosition)) return;
        if (!tool.Equip(_input.MousePosition, _input.MousePosition)) return;
        _grabbed = null;
        _conveyorEditHandle = ConveyorEditHandle.None;
        _surface.Cursor = Cursors.Hand;
        _surface.Focus();
    }

    private void OpenArsenalMenu()
    {
        _renderer.ArsenalMenuSelection = Math.Clamp(
            (_world.Knife?.ArsenalVisualVariant ?? -1) + 1,
            0,
            GameRenderer.ArsenalItemCount - 1);
        _renderer.ArsenalMenuOpen = true;
        _arsenalMenuConsumedClick = false;
        _surface.Cursor = Cursors.Default;
        _surface.Invalidate();
    }

    private void CloseArsenalMenu()
    {
        _renderer.ArsenalMenuOpen = false;
        _arsenalMenuConsumedClick = false;
        _surface.Cursor = Cursors.Default;
        _surface.Invalidate();
    }

    private void MoveArsenalSelection(int delta)
    {
        var count = GameRenderer.ArsenalItemCount;
        _renderer.ArsenalMenuSelection = (_renderer.ArsenalMenuSelection + delta % count + count) % count;
        _surface.Invalidate();
    }

    private void ConfirmArsenalSelection()
    {
        var menuSelection = Math.Clamp(
            _renderer.ArsenalMenuSelection, 0, GameRenderer.ArsenalItemCount - 1);
        _world.Knife?.SelectArsenalVisual(menuSelection - 1);
        CloseArsenalMenu();
    }

    private void SelectNextConveyor()
    {
        var editable = _world.Conveyors.Where(conveyor => !conveyor.IsSystemControlled).ToList();
        if (editable.Count == 0) return;
        DeselectLights();
        var current = _selectedConveyor is null ? -1 : editable.IndexOf(_selectedConveyor);
        foreach (var existingConveyor in _world.Conveyors) existingConveyor.IsSelected = false;
        _selectedConveyor = editable[(current + 1) % editable.Count];
        _selectedConveyor.IsSelected = true;
    }

    private void SpawnConveyor()
    {
        var editableCount = _world.Conveyors.Count(conveyor => !conveyor.IsSystemControlled);
        if (editableCount >= 8) return;
        DeselectLights();
        foreach (var existingConveyor in _world.Conveyors) existingConveyor.IsSelected = false;
        var index = editableCount;
        var conveyor = new ConveyorBelt(
            new Vector2(150f + index * 46f, MathF.Max(160f, WorldHeight * 0.48f + index * 22f)),
            220f,
            38f,
            120f);
        conveyor.IsSelected = true;
        _world.Conveyors.Add(conveyor);
        _selectedConveyor = conveyor;
        _surface.Focus();
    }

    private void SpawnLantern()
    {
        if (_world.Lighting.Lights.Count >= LightingRig.MaximumLights) return;
        var index = _world.Lighting.Lights.Count;
        var x = 350f + index % 7 * 120f;
        var cableLength = 92f + index % 3 * 18f;
        var color = (index % 3) switch
        {
            0 => Color.FromArgb(255, 220, 132),
            1 => Color.FromArgb(255, 235, 175),
            _ => Color.FromArgb(205, 235, 238)
        };
        var light = IndustrialLight.CreateHanging(
            new Vector2(x, 0f), cableLength, 390f, 124f, 0.40f, color);
        if (_world.Lighting.AddIndustrialLight(light) is null) return;
        SelectLight(light);
        _surface.Focus();
    }

    private void SelectLight(IndustrialLight light)
    {
        foreach (var existing in _world.Lighting.Lights) existing.IsSelected = existing == light;
        foreach (var conveyor in _world.Conveyors) conveyor.IsSelected = false;
        _selectedConveyor = null;
        _selectedLight = light;
    }

    private void DeselectLights()
    {
        foreach (var light in _world.Lighting.Lights) light.IsSelected = false;
        _selectedLight = null;
        _lightEditHandle = LightEditHandle.None;
    }

    private void DeleteSelectedLantern()
    {
        if (_selectedLight is null) return;
        _world.Lighting.RemoveLight(_selectedLight);
        _selectedLight = null;
        _lightEditHandle = LightEditHandle.None;
    }

    private void EditLantern(float x, float y, bool adjustRange)
    {
        if (_selectedLight is null) return;
        if (adjustRange) _selectedLight.AdjustRange((x + y) * 3f);
        else
        {
            if (x != 0f) _selectedLight.Move(new Vector2(x, 0f), WorldWidth);
            if (y != 0f) _selectedLight.AdjustCable(y);
        }
        _world.Lighting.NotifyEdited();
    }

    private void EditConveyor(float x, float y, bool resize)
    {
        if (_selectedConveyor is null) return;
        if (resize) _selectedConveyor.Resize(x, y, WorldWidth, WorldHeight);
        else _selectedConveyor.Move(new Vector2(x, y), WorldWidth, WorldHeight);
        foreach (var body in _world.Bodies) body.Wake();
    }

    private void ApplyDiagnosticSlice(Vector2 direction)
    {
        var body = _world.Bodies.FirstOrDefault(candidate => !candidate.IsDetachedDebris && candidate.IsPickable);
        if (body is null) return;
        var extent = body.Radius * 1.35f;
        ApplySlice(body.Center - direction * extent, body.Center + direction * extent);
    }

    private void SpawnBlob()
    {
        if (_world.ProcessingLine?.Powered != true) return;
        if (_world.Bodies.Count >= 24) return;
        _chamberFeed?.RequestNext();
        _surface.Focus();
    }

    private void BeginFixtureDrag(FixtureDragTarget target, RectangleF bounds)
    {
        ClearFixtureSelection();
        _fixtureDragTarget = target;
        _fixtureDragStart = _input.MousePosition;
        _fixtureDragOffset = _input.MousePosition - new Vector2(bounds.X, bounds.Y);
        _fixtureDragMoved = false;
        if (target == FixtureDragTarget.BlobCounter && _world.HoldingChamber is not null)
            _world.HoldingChamber.CounterSelected = true;
        else if (target == FixtureDragTarget.BreakerBox && _world.ProcessingLine is not null)
            _world.ProcessingLine.BreakerSelected = true;
        _grabbed = null;
        _conveyorEditHandle = ConveyorEditHandle.None;
        _surface.Cursor = Cursors.SizeAll;
        _surface.Focus();
    }

    private void ClearFixtureSelection()
    {
        if (_world.HoldingChamber is not null) _world.HoldingChamber.CounterSelected = false;
        if (_world.ProcessingLine is not null) _world.ProcessingLine.BreakerSelected = false;
    }

    private void SaveFixtureLayout()
    {
        if (_world is null) return;
        _fixtureLayout.Capture(_world.HoldingChamber, _world.ProcessingLine);
        _fixtureLayout.Save();
    }

    private Vector2 ToWorld(Point point) =>
        ViewportLayout.ToWorld(point, WorldViewport, LogicalViewport, true);

    private sealed class GameSurface : Control
    {
        public GameSurface()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.StandardDoubleClick |
                     ControlStyles.ResizeRedraw, true);
        }
    }

    private enum FixtureDragTarget : byte
    {
        None,
        BlobCounter,
        BreakerBox
    }
}
