using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Numerics;
using BlobForge.Physics;
using BlobForge.World;

namespace BlobForge.Rendering;

public sealed class GameRenderer
{
    private const int FactoryTileSize = 32;
    private const int BasinFluidVisualScaleY = 3;
    internal const float BasinBubbleThreshold = 0.35f;
    private static readonly Color VacuumHoseShadowColor = Color.FromArgb(255, 8, 13, 17);
    private static readonly Color VacuumHoseBodyColor = Color.FromArgb(255, 53, 70, 78);
    private static readonly Color VacuumHoseHighlightColor = Color.FromArgb(210, 101, 230, 223);
    private static readonly Lazy<Bitmap?> FactoryTileset = new(LoadFactoryTileset);
    private static readonly Lazy<Bitmap?> FactoryBackdropTileset = new(LoadFactoryBackdropTileset);
    private static readonly Lazy<Bitmap?> HoldingChamberSprite = new(LoadHoldingChamberSprite);
    private static readonly Lazy<Bitmap?> CrusherHeadSprite = new(() => LoadAsset("CrusherHead.png"));
    private static readonly Lazy<Bitmap?> MachineBaySprite = new(() => LoadAsset("MachineBay.png"));
    private static readonly Lazy<Bitmap?> OutputCartSprite = new(() => LoadAsset("OutputCart.png"));
    private static readonly Lazy<Bitmap?> CrusherButtonSprite = new(() => LoadAsset("CrusherButton.png"));
    private static readonly Lazy<Bitmap?> DrillHeadSprite = new(() => LoadAsset("DrillHead.png"));
    private static readonly Lazy<Bitmap?> DrillLeverIdleSprite = new(() => LoadAsset("DrillLeverIdle.png"));
    private static readonly Lazy<Bitmap?> DrillLeverHeldSprite = new(() => LoadAsset("DrillLeverHeld.png"));
    private static readonly Lazy<Bitmap?> DrumRotorSprite = new(() => LoadAsset("DrumRotor.png"));
    private static readonly Lazy<Bitmap?> DrumHandwheelSprite = new(() => LoadAsset("DrumHandwheel.png"));
    private static readonly Lazy<Bitmap?> VacuumNozzleSprite = new(() => LoadAsset("VacuumNozzle.png"));
    private static readonly Lazy<Bitmap?> VacuumCouplerSprite = new(() => LoadAsset("VacuumCoupler.png"));
    private static readonly Lazy<Bitmap?> RustyDrainPipeSprite = new(() => LoadAsset("RustyDrainPipe.png"));
    private static readonly Lazy<Bitmap?> VacuumHolsterSprite = new(() => LoadAsset("VacuumHolster.png"));
    private static readonly Lazy<Bitmap?> MachineStatusSprite = new(() => LoadAsset("MachineStatus.png"));
    private static readonly Lazy<Bitmap?> BasinMonitorSprite = new(() => LoadAsset("BasinMonitor.png"));
    private static readonly Lazy<Bitmap?> DiegoSpriteSheet = new(() => LoadAsset("Diego.png"));
    private static readonly Lazy<Bitmap?> FilterKnobSprite = new(() => LoadAsset("FilterKnob.png"));
    private static readonly Lazy<Bitmap?> BreakerBoxSprite = new(() => LoadAsset("BreakerBox.png"));
    private static readonly Lazy<Bitmap?> ReceivingTubSprite = new(() => LoadAsset("ReceivingTub.png"));
    // GDI+ scaling is surprisingly expensive even for tiny transparent PNGs. These
    // display-sized caches preserve nearest-neighbor pixels and eliminate dozens of
    // per-frame resampling operations across the machinery and basin layers.
    private static readonly Lazy<Bitmap?> CrusherFrameDisplay = new(() => LoadScaledAsset("CrusherFrame.png", 120, 168));
    private static readonly Lazy<Bitmap?> DrillFrameDisplay = new(() => LoadScaledAsset("DrillFrame.png", 108, 151));
    private static readonly Lazy<Bitmap?> DrumHousingDisplay = new(() => LoadAsset("DrumHousing.png"));
    private static readonly Lazy<Bitmap?> VacuumFrameDisplay = new(() => LoadScaledAsset("VacuumFrame.png", 108, 151));
    private static readonly Lazy<Bitmap?> FilterFrameDisplay = new(() => LoadScaledAsset("FilterFrame.png", 108, 151));
    private static readonly Lazy<Bitmap?> BasinEndcapDisplay = new(() => LoadScaledAsset("BasinEndcap.png", 24, 105));
    private readonly Font _hudFont = new("Consolas", 10.5f, FontStyle.Regular, GraphicsUnit.Point);
    private readonly Font _titleFont = new("Segoe UI Semibold", 17f, FontStyle.Bold, GraphicsUnit.Point);
    private readonly Font _shopFont = new("Consolas", 7f, FontStyle.Bold, GraphicsUnit.Point);
    private readonly Font _shopSmallFont = new("Consolas", 6f, FontStyle.Bold, GraphicsUnit.Point);
    private readonly Pen _constraintPen = new(Color.FromArgb(65, 255, 255, 255), 1f);
    private readonly SolidBrush _debugSupportedParticleBrush = new(Color.Gold);
    private readonly SolidBrush _debugParticleBrush = new(Color.FromArgb(155, 255, 255, 255));
    private readonly SolidBrush _debugPanelBackgroundBrush = new(Color.FromArgb(190, 5, 9, 15));
    private readonly SolidBrush _debugPanelTextBrush = new(Color.FromArgb(235, 161, 235, 205));
    private readonly GraphicsPath _debugConstraintPath = new();
    private readonly GraphicsPath _debugParticlePath = new(FillMode.Winding);
    private readonly GraphicsPath _debugSupportedParticlePath = new(FillMode.Winding);
    private readonly GraphicsPath _bloodGranularPath = new(FillMode.Winding);
    private readonly GraphicsPath _bloodGranularHighlightPath = new(FillMode.Winding);
    private readonly GraphicsPath _tissueGranularPath = new(FillMode.Winding);
    private readonly GraphicsPath _tissueGranularDarkPath = new(FillMode.Winding);
    private readonly GraphicsPath _tissueGranularCorePath = new(FillMode.Winding);
    private readonly Bitmap _debugPanel = new(286, 426);
    private long _nextDebugPanelRefresh;
    private readonly SolidBrush[] _wetStainBrushes =
    {
        new(Color.FromArgb(140, 148, 4, 14)),
        new(Color.FromArgb(190, 188, 3, 15)),
        new(Color.FromArgb(235, 226, 7, 16))
    };
    private readonly SolidBrush[] _dryStainBrushes =
    {
        new(Color.FromArgb(105, 58, 20, 26)),
        new(Color.FromArgb(155, 70, 22, 29)),
        new(Color.FromArgb(195, 86, 25, 32))
    };
    private readonly SolidBrush _wetStainShine = new(Color.FromArgb(120, 255, 35, 22));
    private readonly SolidBrush _steelBrush = new(Color.FromArgb(62, 73, 84));
    private readonly SolidBrush _concreteBrush = new(Color.FromArgb(105, 114, 121));
    private readonly SolidBrush _glassBrush = new(Color.FromArgb(51, 69, 78));
    private readonly Pen _steelPen = new(Color.FromArgb(104, 118, 129), 1f);
    private readonly Pen _concretePen = new(Color.FromArgb(139, 150, 157), 1f);
    private readonly Pen _glassPen = new(Color.FromArgb(85, 113, 123), 1f);
    private readonly Pen _crackPen = new(Color.FromArgb(175, 22, 18, 24), 1.5f);
    private readonly SolidBrush _conveyorFrameBrush = new(Color.FromArgb(42, 48, 59));
    private readonly SolidBrush _conveyorBeltBrush = new(Color.FromArgb(64, 76, 88));
    private readonly SolidBrush _conveyorRollerBrush = new(Color.FromArgb(25, 30, 38));
    private readonly SolidBrush _conveyorHubBrush = new(Color.FromArgb(102, 121, 137));
    private readonly SolidBrush _conveyorHandleBrush = new(Color.FromArgb(255, 203, 76));
    private readonly Pen _conveyorEdgePen = new(Color.FromArgb(210, 130, 154, 174), 2f);
    private readonly Pen _selectedConveyorEdgePen = new(Color.FromArgb(245, 255, 203, 76), 3f);
    private readonly Pen _conveyorMotionPen = new(Color.FromArgb(190, 103, 232, 201), 2f)
    {
        StartCap = LineCap.Round,
        EndCap = LineCap.Round
    };
    private readonly SolidBrush _conveyorLabelBrush = new(Color.FromArgb(235, 255, 232, 145));
    private readonly SolidBrush _chamberDoorBrush = new(Color.FromArgb(255, 52, 67, 75));
    private readonly SolidBrush _chamberDoorEdgeBrush = new(Color.FromArgb(255, 128, 148, 157));
    private readonly SolidBrush _chamberWarningBrush = new(Color.FromArgb(255, 242, 193, 78));
    private readonly SolidBrush _chamberGlassFallbackBrush = new(Color.FromArgb(72, 55, 112, 124));
    private readonly Pen _chamberLeverShadowPen = new(Color.FromArgb(255, 20, 27, 31), 10f)
    {
        StartCap = LineCap.Round,
        EndCap = LineCap.Round
    };
    private readonly Pen _chamberLeverPen = new(Color.FromArgb(255, 112, 132, 141), 6f)
    {
        StartCap = LineCap.Round,
        EndCap = LineCap.Round
    };
    private readonly Pen _conveyorTrackPen = new(Color.FromArgb(225, 157, 181, 198), 3f);
    private readonly Pen _conveyorTreadPen = new(Color.FromArgb(205, 30, 37, 46), 2f);
    private readonly Pen _conveyorSpokePen = new(Color.FromArgb(190, 123, 145, 160), 1.5f);
    private readonly SolidBrush _blobDarkBrush = new(Color.FromArgb(255, 4, 6, 8));
    private readonly SolidBrush _blobDebrisDarkBrush = new(Color.FromArgb(255, 72, 7, 18));
    private readonly SolidBrush _blobGrabbedDarkBrush = new(Color.FromArgb(255, 13, 3, 8));
    private readonly SolidBrush _blobMachineLitDarkBrush = new(Color.FromArgb(255, 45, 5, 14));
    private readonly SolidBrush _blobPixelRedBrush = new(Color.FromArgb(255, 238, 18, 30));
    private readonly SolidBrush _blobGrabbedPixelBrush = new(Color.FromArgb(255, 255, 65, 93));
    private readonly SolidBrush _blobMachineLitPixelBrush = new(Color.FromArgb(255, 255, 35, 47));
    private readonly SolidBrush _bloodPixelBrush = new(Color.FromArgb(245, 176, 3, 15));
    private readonly SolidBrush _bloodPixelHighlightBrush = new(Color.FromArgb(245, 255, 18, 16));
    private readonly SolidBrush _tissuePixelCoreBrush = new(Color.FromArgb(255, 4, 6, 8));
    private readonly SolidBrush _tissuePixelRimBrush = new(Color.FromArgb(255, 238, 18, 30));
    private readonly SolidBrush _tissuePixelRimDarkBrush = new(Color.FromArgb(245, 150, 13, 34));
    private Rectangle[] _blobPixelOutlineRectangles = new Rectangle[256];
    private int _blobPixelOutlineRectangleCount;
    private DestructibleGrid? _bloodSurfaceClipGrid;
    private int _bloodSurfaceClipRevision = -1;
    private Region? _bloodSurfaceClip;
    private DestructibleGrid? _environmentCacheGrid;
    private ProcessingLine? _environmentCacheLine;
    private int _environmentCacheRevision = -1;
    private Size _environmentCacheSize;
    private Bitmap? _environmentCache;
    private LightingRig? _lightingCacheRig;
    private int _lightingCacheRevision = -1;
    private Size _lightingCacheSize;
    private Bitmap? _lightingCache;
    private int _dynamicLightingRevision = -1;
    private int _dynamicLightingAmbientRevision = -1;
    private int _dynamicLightingGridRevision = -1;
    private Size _dynamicLightingCacheSize;
    private Bitmap? _dynamicLightingCache;
    private BloodBasin? _basinFluidCacheSource;
    private int _basinFluidCacheRevision = -1;
    private Bitmap? _basinFluidCache;
    private byte[] _basinFluidPixels = Array.Empty<byte>();
    private readonly float[] _basinFluidDepths = new float[BloodBasin.FluidGridWidth];
    private readonly float[] _lightHitDistances = new float[73];
    private readonly PointF[] _lightSidePolygon = new PointF[22];
    private readonly PointF[] _lightCenterPolygon = new PointF[66];
    private readonly PointF[] _shopPanelPolygon = new PointF[7];

    public bool DebugDraw { get; set; }
    public double FrameMs { get; set; }
    public double Fps { get; set; }
    public double RenderMs { get; set; }
    public double PresentMs { get; set; }
    public double FixedUpdateMs { get; set; }
    public double AudioUpdateMs { get; set; }
    public int LightingCacheBuildCount { get; private set; }
    public int DynamicLightingBuildCount { get; private set; }
    public bool ProfileStages { get; set; }
    public double EnvironmentStageMs { get; private set; }
    public double MachineryBackStageMs { get; private set; }
    public double MatterStageMs { get; private set; }
    public double MachineryFrontStageMs { get; private set; }
    public double LightingStageMs { get; private set; }
    public double UiStageMs { get; private set; }

    public void Draw(Graphics g, Size viewport, BlobWorld world, SoftBody? grabbed, IReadOnlyList<Vector2>? pendingSlice = null)
    {
        var stageStart = ProfileStages ? Stopwatch.GetTimestamp() : 0L;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        DrawEnvironment(g, viewport, world.Grid, world.ProcessingLine);
        if (ProfileStages)
        {
            EnvironmentStageMs = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;
            stageStart = Stopwatch.GetTimestamp();
        }
        DrawConveyors(g, world.Conveyors);
        DrawProcessingLineBack(g, world.ProcessingLine);
        if (ProfileStages)
        {
            MachineryBackStageMs = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;
            stageStart = Stopwatch.GetTimestamp();
        }
        DrawBloodSurfaceStains(g, world.Grid, world.Conveyors);
        DrawGranular(g, world.Granular, GranularKind.Blood);
        foreach (var body in world.Bodies)
            if (!ReferenceEquals(body, world.ProcessingLine?.DrumLockedBody))
                DrawBlob(g, body, body == grabbed);
        DrawHoldingChamber(g, world.HoldingChamber);
        DrawGranular(g, world.Granular, GranularKind.Tissue);
        if (ProfileStages)
        {
            MatterStageMs = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;
            stageStart = Stopwatch.GetTimestamp();
        }
        // The cart is the final physical foreground object. All simulated
        // matter—including blood, chunks and intact blobs—must remain behind
        // its shell. Lighting and UI intentionally render afterward.
        DrawProcessingLineFront(g, world.ProcessingLine);
        DrawBreakerBox(g, world.ProcessingLine);
        if (ProfileStages)
        {
            MachineryFrontStageMs = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;
            stageStart = Stopwatch.GetTimestamp();
        }
        var factoryBlackout = world.ProcessingLine?.Powered == false;
        if (factoryBlackout)
        {
            // Authoring/debug overlays are part of the scene and must disappear with
            // everything else until the breaker supplies power.
            DrawFixtureEditHandles(g, world.HoldingChamber, world.ProcessingLine);
            DrawSlicePreview(g, pendingSlice);
            DrawInstructions(g, viewport);
            if (DebugDraw) DrawDebug(g, world);
        }
        DrawLighting(g, viewport, world);
        DrawBreakerLamp(g, world.ProcessingLine);
        if (ProfileStages)
        {
            LightingStageMs = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;
            stageStart = Stopwatch.GetTimestamp();
        }
        if (!factoryBlackout)
        {
            DrawFixtureEditHandles(g, world.HoldingChamber, world.ProcessingLine);
            DrawSlicePreview(g, pendingSlice);
            DrawInstructions(g, viewport);
            if (DebugDraw) DrawDebug(g, world);
        }
        if (ProfileStages)
            UiStageMs = Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds;
    }

    private void DrawLighting(Graphics g, Size viewport, BlobWorld world)
    {
        if (viewport.Width <= 0 || viewport.Height <= 0) return;
        var rig = world.Lighting;
        if (!rig.FactoryPowered)
        {
            DrawPowerOffBlackout(g, viewport, world.ProcessingLine);
            return;
        }
        if (_lightingCache is null || _lightingCacheRig != rig ||
            _lightingCacheRevision != rig.Revision || _lightingCacheSize != viewport)
        {
            _lightingCache?.Dispose();
            _lightingCache = BuildAmbientLightingOverlay(viewport, rig);
            _lightingCacheRig = rig;
            _lightingCacheRevision = rig.Revision;
            _lightingCacheSize = viewport;
            LightingCacheBuildCount++;
        }
        if (_dynamicLightingCache is null || _dynamicLightingRevision != rig.DynamicRevision ||
            _dynamicLightingAmbientRevision != rig.Revision ||
            _dynamicLightingGridRevision != world.Grid.SurfaceRevision ||
            _dynamicLightingCacheSize != viewport)
        {
            if (_dynamicLightingCache is null || _dynamicLightingCacheSize != viewport)
            {
                _dynamicLightingCache?.Dispose();
                _dynamicLightingCache = new Bitmap(
                    viewport.Width, viewport.Height, PixelFormat.Format32bppPArgb);
            }
            RenderDynamicLightingOverlay(_dynamicLightingCache, world);
            _dynamicLightingRevision = rig.DynamicRevision;
            _dynamicLightingAmbientRevision = rig.Revision;
            _dynamicLightingGridRevision = world.Grid.SurfaceRevision;
            _dynamicLightingCacheSize = viewport;
            DynamicLightingBuildCount++;
        }
        g.DrawImageUnscaled(_dynamicLightingCache, 0, 0);
        foreach (var light in rig.Lights) DrawIndustrialLantern(g, light, rig.FactoryPowered);
    }

    private static void DrawPowerOffBlackout(Graphics g, Size viewport, ProcessingLine? line)
    {
        // Before the main breaker is pulled, the factory has no ambient light at all.
        // The emergency bulb above the breaker is the sole exception, revealed through
        // a hard-edged pixel pool while every pixel outside it is true opaque black.
        var bounds = line?.BreakerBounds ?? new RectangleF(viewport.Width - 150f, 360f, 96f, 128f);
        var lamp = new Vector2(bounds.Left + bounds.Width * 0.5f, bounds.Top + 7f);
        var pool = new[]
        {
            new PointF(lamp.X - 64f, lamp.Y - 42f),
            new PointF(lamp.X + 64f, lamp.Y - 42f),
            new PointF(lamp.X + 92f, lamp.Y - 14f),
            new PointF(lamp.X + 92f, lamp.Y + 88f),
            new PointF(lamp.X + 66f, lamp.Y + 150f),
            new PointF(lamp.X - 66f, lamp.Y + 150f),
            new PointF(lamp.X - 92f, lamp.Y + 88f),
            new PointF(lamp.X - 92f, lamp.Y - 14f)
        };

        using var blackoutPath = new GraphicsPath(FillMode.Alternate);
        blackoutPath.AddRectangle(new Rectangle(0, 0, viewport.Width, viewport.Height));
        blackoutPath.AddPolygon(pool);
        using var poolPath = new GraphicsPath();
        poolPath.AddPolygon(pool);
        using var black = new SolidBrush(Color.Black);
        g.FillPath(black, blackoutPath);

        // Stepped shading keeps the pool small, yellow, and deliberately pixel-art-like.
        using var edgeShade = new SolidBrush(Color.FromArgb(150, 0, 0, 0));
        using var middleShade = new SolidBrush(Color.FromArgb(72, 0, 0, 0));
        using var yellowWash = new SolidBrush(Color.FromArgb(34, 240, 195, 75));
        using var edgeRegion = new Region(poolPath);
        using var middlePath = new GraphicsPath();
        middlePath.AddPolygon(new[]
        {
            new PointF(lamp.X - 66f, lamp.Y - 23f), new PointF(lamp.X + 66f, lamp.Y - 23f),
            new PointF(lamp.X + 72f, lamp.Y - 7f), new PointF(lamp.X + 72f, lamp.Y + 77f),
            new PointF(lamp.X + 52f, lamp.Y + 98f), new PointF(lamp.X - 52f, lamp.Y + 98f),
            new PointF(lamp.X - 72f, lamp.Y + 77f), new PointF(lamp.X - 72f, lamp.Y - 7f)
        });
        edgeRegion.Exclude(middlePath);
        g.FillRegion(edgeShade, edgeRegion);
        using var corePath = new GraphicsPath();
        corePath.AddPolygon(new[]
        {
            new PointF(lamp.X - 45f, lamp.Y - 12f), new PointF(lamp.X + 45f, lamp.Y - 12f),
            new PointF(lamp.X + 54f, lamp.Y + 4f), new PointF(lamp.X + 54f, lamp.Y + 62f),
            new PointF(lamp.X + 38f, lamp.Y + 79f), new PointF(lamp.X - 38f, lamp.Y + 79f),
            new PointF(lamp.X - 54f, lamp.Y + 62f), new PointF(lamp.X - 54f, lamp.Y + 4f)
        });
        using var middleRegion = new Region(middlePath);
        middleRegion.Exclude(corePath);
        g.FillRegion(middleShade, middleRegion);
        g.FillPath(yellowWash, corePath);
    }

    private static Bitmap BuildAmbientLightingOverlay(Size viewport, LightingRig rig)
    {
        var overlay = new Bitmap(viewport.Width, viewport.Height, PixelFormat.Format32bppPArgb);
        using var lightGraphics = Graphics.FromImage(overlay);
        lightGraphics.Clear(Color.Transparent);
        lightGraphics.SmoothingMode = SmoothingMode.None;
        var ambientAlpha = (int)MathF.Round((1f - rig.AmbientLevel) * 176f);
        if (ambientAlpha > 0)
        {
            using var ambient = new SolidBrush(Color.FromArgb(
                ambientAlpha, rig.AmbientColor.R, rig.AmbientColor.G, rig.AmbientColor.B));
            lightGraphics.FillRectangle(ambient, 0, 0, viewport.Width, viewport.Height);
        }
        DrawDirectionalPixelWash(lightGraphics, viewport, rig);
        return overlay;
    }

    private static void DrawDirectionalPixelWash(Graphics g, Size viewport, LightingRig rig)
    {
        if (rig.DirectionalStrength <= 0.001f) return;
        var direction = rig.DirectionalDirection;
        var alpha = Math.Clamp((int)MathF.Round(rig.DirectionalStrength * 30f), 1, 12);
        using var pixelBrush = new SolidBrush(Color.FromArgb(
            alpha, rig.DirectionalColor.R, rig.DirectionalColor.G, rig.DirectionalColor.B));
        for (var y = 0; y < viewport.Height; y += 32)
        for (var x = 0; x < viewport.Width; x += 32)
        {
            var phase = (int)MathF.Floor((x * direction.X + y * direction.Y) / 32f);
            if (((x / 32 + y / 32 + phase) & 3) != 0) continue;
            g.FillRectangle(pixelBrush, x, y, 16, 16);
        }
    }

    private void RenderDynamicLightingOverlay(Bitmap overlay, BlobWorld world)
    {
        using var lightGraphics = Graphics.FromImage(overlay);
        lightGraphics.Clear(Color.Transparent);
        if (_lightingCache is not null)
            lightGraphics.DrawImageUnscaled(_lightingCache, 0, 0);
        lightGraphics.SmoothingMode = SmoothingMode.None;
        lightGraphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        lightGraphics.PixelOffsetMode = PixelOffsetMode.Half;
        if (!world.Lighting.FactoryPowered) return;
        foreach (var light in world.Lighting.Lights)
            DrawOccludedPixelLight(lightGraphics, world, light);
    }

    private void DrawOccludedPixelLight(Graphics g, BlobWorld world, IndustrialLight light)
    {
        const int rayCount = 72;
        const int radialBands = 11;
        const float halfAngle = 1.18f;
        var origin = light.Position + light.Direction * 12f;
        for (var ray = 0; ray <= rayCount; ray++)
        {
            var angle = -halfAngle + ray / (float)rayCount * halfAngle * 2f;
            var direction = Rotate(light.Direction, angle);
            _lightHitDistances[ray] = FindLightHitDistance(world, origin, direction, light.Range);
        }

        using var bandBrush = new SolidBrush(Color.Transparent);
        DrawSector(0, 10, 0.16f);
        DrawSector(10, 20, 0.44f);
        DrawSector(20, 52, 1f);
        DrawSector(52, 62, 0.44f);
        DrawSector(62, 72, 0.16f);

        void DrawSector(int firstRay, int lastRay, float angularStrength)
        {
            for (var band = 0; band < radialBands; band++)
            {
                var inner = 10f + light.Range * band / radialBands;
                var outer = 10f + light.Range * (band + 1f) / radialBands;
                var fade = 1f - (band + 0.35f) / radialBands;
                var alpha = Math.Clamp(
                    (int)MathF.Round(light.Strength * 72f * fade * fade * angularStrength), 1, 38);
                bandBrush.Color = Color.FromArgb(alpha, light.Color.R, light.Color.G, light.Color.B);
                var count = lastRay - firstRay + 1;
                var polygon = count > 11 ? _lightCenterPolygon : _lightSidePolygon;
                for (var ray = firstRay; ray <= lastRay; ray++)
                {
                    var angle = -halfAngle + ray / (float)rayCount * halfAngle * 2f;
                    var direction = Rotate(light.Direction, angle);
                    polygon[ray - firstRay] = PixelPoint4(
                        origin + direction * MathF.Min(outer, _lightHitDistances[ray]));
                }
                for (var ray = lastRay; ray >= firstRay; ray--)
                {
                    var angle = -halfAngle + ray / (float)rayCount * halfAngle * 2f;
                    var direction = Rotate(light.Direction, angle);
                    polygon[count + lastRay - ray] = PixelPoint4(
                        origin + direction * MathF.Min(inner, _lightHitDistances[ray]));
                }
                g.FillPolygon(bandBrush, polygon);
            }
        }
    }

    private static float FindLightHitDistance(
        BlobWorld world, Vector2 origin, Vector2 direction, float maximumDistance)
    {
        var closest = RaycastTerrain(world.Grid, origin, direction, maximumDistance);
        foreach (var conveyor in world.Conveyors)
            closest = MathF.Min(closest, RayRectangle(origin, direction,
                new RectangleF(conveyor.Position.X, conveyor.Position.Y, conveyor.Width, conveyor.Height), closest));
        if (world.HoldingChamber is { } chamber)
        {
            closest = MathF.Min(closest,
                RayCircle(origin, direction, chamber.Center, chamber.InnerRadius + 10f, closest));
            closest = MathF.Min(closest, RayRectangle(origin, direction, chamber.FeedTubeBounds, closest));
        }
        foreach (var body in world.Bodies)
        {
            if (body.PhysicalParticleCount < 3) continue;
            closest = MathF.Min(closest,
                RayCircle(origin, direction, body.Center, MathF.Max(6f, body.Radius * 0.88f), closest));
        }
        return MathF.Min(maximumDistance, closest + 7f);
    }

    internal static float TraceLightForDiagnostics(
        BlobWorld world, IndustrialLight light, Vector2 direction) =>
        FindLightHitDistance(
            world,
            light.Position + light.Direction * 12f,
            Vector2.Normalize(direction),
            light.Range);

    private static float RaycastTerrain(
        DestructibleGrid grid, Vector2 origin, Vector2 direction, float maximumDistance)
    {
        const float step = 6f;
        for (var distance = 14f; distance <= maximumDistance; distance += step)
        {
            var point = origin + direction * distance;
            var cellX = (int)MathF.Floor(point.X / grid.CellSize);
            var cellY = (int)MathF.Floor(point.Y / grid.CellSize);
            if (cellX < 0 || cellY < 0 || cellX >= grid.Columns || cellY >= grid.Rows) continue;
            if (grid.Cell(cellX, cellY).IsSolid) return distance;
        }
        return maximumDistance;
    }

    private static float RayCircle(
        Vector2 origin, Vector2 direction, Vector2 center, float radius, float maximumDistance)
    {
        var offset = center - origin;
        var projection = Vector2.Dot(offset, direction);
        if (projection <= 10f || projection >= maximumDistance) return maximumDistance;
        var perpendicularSq = offset.LengthSquared() - projection * projection;
        var radiusSq = radius * radius;
        if (perpendicularSq >= radiusSq) return maximumDistance;
        var hit = projection - MathF.Sqrt(MathF.Max(0f, radiusSq - perpendicularSq));
        return hit > 10f ? hit : maximumDistance;
    }

    private static float RayRectangle(
        Vector2 origin, Vector2 direction, RectangleF rectangle, float maximumDistance)
    {
        var minimum = new Vector2(rectangle.Left, rectangle.Top);
        var maximum = new Vector2(rectangle.Right, rectangle.Bottom);
        var tMin = 0f;
        var tMax = maximumDistance;
        for (var axis = 0; axis < 2; axis++)
        {
            var originAxis = axis == 0 ? origin.X : origin.Y;
            var directionAxis = axis == 0 ? direction.X : direction.Y;
            var minAxis = axis == 0 ? minimum.X : minimum.Y;
            var maxAxis = axis == 0 ? maximum.X : maximum.Y;
            if (MathF.Abs(directionAxis) < 0.0001f)
            {
                if (originAxis < minAxis || originAxis > maxAxis) return maximumDistance;
                continue;
            }
            var inverse = 1f / directionAxis;
            var near = (minAxis - originAxis) * inverse;
            var far = (maxAxis - originAxis) * inverse;
            if (near > far) (near, far) = (far, near);
            tMin = MathF.Max(tMin, near);
            tMax = MathF.Min(tMax, far);
            if (tMin > tMax) return maximumDistance;
        }
        return tMin > 10f && tMin < maximumDistance ? tMin : maximumDistance;
    }

    private static void DrawIndustrialLantern(Graphics g, IndustrialLight light, bool powered)
    {
        var previousSmoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.None;
        var direction = light.Direction;
        var tangent = light.Tangent;
        var anchor = PixelPoint4(light.Anchor);
        var position = light.Position;
        using var cableShadow = new Pen(Color.FromArgb(245, 8, 12, 17), 5f);
        using var cable = new Pen(Color.FromArgb(255, 76, 92, 101), 2f);
        using var dark = new SolidBrush(Color.FromArgb(255, 10, 15, 20));
        using var steel = new SolidBrush(Color.FromArgb(255, 57, 72, 82));
        using var trim = new Pen(Color.FromArgb(255, 135, 156, 165), 2f);
        using var glow = new SolidBrush(powered
            ? Color.FromArgb(255, light.Color.R, light.Color.G, light.Color.B)
            : Color.FromArgb(255, 35, 43, 47));

        g.FillRectangle(dark, anchor.X - 11f, -1f, 22f, 8f);
        g.FillRectangle(steel, anchor.X - 7f, 3f, 14f, 7f);
        g.DrawLine(cableShadow, light.Anchor.X, light.Anchor.Y + 7f, position.X, position.Y - 15f);
        g.DrawLine(cable, light.Anchor.X, light.Anchor.Y + 7f, position.X, position.Y - 15f);

        var hood = new[]
        {
            PixelPoint4(position - tangent * 23f - direction * 15f),
            PixelPoint4(position + tangent * 23f - direction * 15f),
            PixelPoint4(position + tangent * 18f - direction * 3f),
            PixelPoint4(position - tangent * 18f - direction * 3f)
        };
        g.FillPolygon(dark, hood);
        g.FillPolygon(steel, hood);
        g.DrawPolygon(trim, hood);
        var glass = new[]
        {
            PixelPoint4(position - tangent * 14f - direction * 2f),
            PixelPoint4(position + tangent * 14f - direction * 2f),
            PixelPoint4(position + tangent * 12f + direction * 19f),
            PixelPoint4(position - tangent * 12f + direction * 19f)
        };
        g.FillPolygon(glow, glass);
        g.DrawPolygon(trim, glass);
        for (var bar = -1; bar <= 1; bar++)
        {
            var top = position + tangent * (bar * 9f) - direction * 2f;
            var bottom = position + tangent * (bar * 8f) + direction * 19f;
            g.DrawLine(cable, top.X, top.Y, bottom.X, bottom.Y);
        }
        var bottomCenter = position + direction * 21f;
        g.DrawLine(trim,
            bottomCenter.X - tangent.X * 15f, bottomCenter.Y - tangent.Y * 15f,
            bottomCenter.X + tangent.X * 15f, bottomCenter.Y + tangent.Y * 15f);

        if (light.IsSelected)
        {
            using var selected = new Pen(Color.FromArgb(255, 255, 204, 73), 2f);
            using var handle = new SolidBrush(Color.FromArgb(255, 255, 204, 73));
            var cableHandle = PixelPoint4(light.CableHandle);
            var rangeHandle = PixelPoint4(light.RangeHandle);
            g.DrawRectangle(selected, anchor.X - 8f, 1f, 16f, 13f);
            g.FillRectangle(handle, cableHandle.X - 4f, cableHandle.Y - 4f, 8f, 8f);
            g.DrawLine(selected, position.X, position.Y, rangeHandle.X, rangeHandle.Y);
            g.FillRectangle(handle, rangeHandle.X - 5f, rangeHandle.Y - 5f, 10f, 10f);
        }
        g.SmoothingMode = previousSmoothing;
    }

    private static Vector2 Rotate(Vector2 vector, float angle)
    {
        var cosine = MathF.Cos(angle);
        var sine = MathF.Sin(angle);
        return new Vector2(vector.X * cosine - vector.Y * sine, vector.X * sine + vector.Y * cosine);
    }

    private static PointF PixelPoint4(Vector2 point) =>
        new(MathF.Round(point.X / 4f) * 4f, MathF.Round(point.Y / 4f) * 4f);

    private static void DrawSlicePreview(Graphics g, IReadOnlyList<Vector2>? points)
    {
        if (points is null || points.Count < 2) return;
        using var shadow = new Pen(Color.FromArgb(150, 4, 15, 20), 6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var preview = new Pen(Color.FromArgb(235, 108, 245, 225), 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        var path = points.Select(point => new PointF(point.X, point.Y)).ToArray();
        g.DrawLines(shadow, path);
        g.DrawLines(preview, path);
    }

    private void DrawGranular(Graphics g, GranularMaterialSystem granular, GranularKind kind)
    {
        var previousSmoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.None;
        var ordinaryPath = kind == GranularKind.Blood ? _bloodGranularPath : _tissueGranularPath;
        var highlightPath = kind == GranularKind.Blood ? _bloodGranularHighlightPath : _tissueGranularDarkPath;
        ordinaryPath.Reset();
        highlightPath.Reset();
        if (kind == GranularKind.Tissue) _tissueGranularCorePath.Reset();
        for (var i = 0; i < granular.Particles.Count; i++)
        {
            var particle = granular.Particles[i];
            if (particle.Kind != kind) continue;
            var size = MathF.Max(2f, particle.Radius * 1.7f);
            var x = MathF.Round(particle.Position.X - size * 0.5f);
            var y = MathF.Round(particle.Position.Y - size * 0.5f);
            var pixelSize = MathF.Ceiling(size);
            var rectangle = new RectangleF(x, y, pixelSize, pixelSize);
            if (kind == GranularKind.Blood)
            {
                ((i & 3) == 0 ? highlightPath : ordinaryPath).AddRectangle(rectangle);
                continue;
            }

            // Most loose matter reads as a simple material pixel. A restrained
            // minority retains the blob's dark core/red shell relationship.
            var structuralFleck = i % 5 == 0 && pixelSize >= 4f;
            ((i & 3) == 0 ? ordinaryPath : highlightPath).AddRectangle(rectangle);
            if (structuralFleck)
                _tissueGranularCorePath.AddRectangle(
                    new RectangleF(x + 1f, y + 1f, pixelSize - 2f, pixelSize - 2f));
        }
        if (kind == GranularKind.Blood)
        {
            if (ordinaryPath.PointCount > 0) g.FillPath(_bloodPixelBrush, ordinaryPath);
            if (highlightPath.PointCount > 0) g.FillPath(_bloodPixelHighlightBrush, highlightPath);
        }
        else
        {
            if (ordinaryPath.PointCount > 0) g.FillPath(_tissuePixelRimBrush, ordinaryPath);
            if (highlightPath.PointCount > 0) g.FillPath(_tissuePixelRimDarkBrush, highlightPath);
            if (_tissueGranularCorePath.PointCount > 0) g.FillPath(_tissuePixelCoreBrush, _tissueGranularCorePath);
        }
        g.SmoothingMode = previousSmoothing;
    }

    private static void DrawBackdrop(Graphics g, Size viewport)
    {
        using var brush = new LinearGradientBrush(
            new Rectangle(0, 0, viewport.Width, viewport.Height),
            Color.FromArgb(28, 34, 39),
            Color.FromArgb(12, 16, 19),
            LinearGradientMode.Vertical);
        g.FillRectangle(brush, 0, 0, viewport.Width, viewport.Height);

        var tileset = FactoryBackdropTileset.Value;
        if (tileset is not null)
        {
            var previousSmoothing = g.SmoothingMode;
            var previousInterpolation = g.InterpolationMode;
            var previousPixelOffset = g.PixelOffsetMode;
            g.SmoothingMode = SmoothingMode.None;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            var columns = (viewport.Width + FactoryTileSize - 1) / FactoryTileSize;
            var rows = (viewport.Height + FactoryTileSize - 1) / FactoryTileSize;
            for (var y = 0; y < rows; y++)
            for (var x = 0; x < columns; x++)
            {
                var (row, column) = SelectFactoryBackgroundTile(x, y, rows);
                g.DrawImage(tileset,
                    new Rectangle(x * FactoryTileSize, y * FactoryTileSize, FactoryTileSize, FactoryTileSize),
                    new Rectangle(column * FactoryTileSize, row * FactoryTileSize,
                        FactoryTileSize, FactoryTileSize),
                    GraphicsUnit.Pixel);
            }
            g.SmoothingMode = previousSmoothing;
            g.InterpolationMode = previousInterpolation;
            g.PixelOffsetMode = previousPixelOffset;
            return;
        }

        using var gridPen = new Pen(Color.FromArgb(13, 164, 180, 190), 1f);
        for (var x = 0; x < viewport.Width; x += 32) g.DrawLine(gridPen, x, 0, x, viewport.Height);
        for (var y = 0; y < viewport.Height; y += 32) g.DrawLine(gridPen, 0, y, viewport.Width, y);
    }

    internal static (int Row, int Column) SelectFactoryBackgroundTile(
        int x, int y, int rows)
    {
        static (int Row, int Column) WallPanel(int panelBand)
        {
            // Broad three-tile clusters make each plate style read as a fitted wall
            // section instead of per-cell visual noise.
            return (Math.Abs(panelBand) % 5) switch
            {
                0 => (0, 0), // four-bolt plate
                1 => (0, 1), // patched plate
                2 => (0, 5), // inset plate
                3 => (0, 6), // narrow double plate
                _ => (0, 7)  // serial-marked plate
            };
        }

        // The basin and service trench sit against heavier, darker lower-wall cladding.
        if (y >= Math.Max(16, rows - 7))
        {
            if (x % 12 == 2) return (3, 2); // drain grille
            if (x % 12 == 0) return (3, 1); // structural seam
            if (x % 17 == 7) return (3, 5); // maintenance hatch
            if (x % 19 == 11) return (3, 6); // cross brace
            return (Math.Abs((x / 4) + (y / 2)) % 3) switch
            {
                0 => (3, 0), // banded cladding
                1 => (3, 4), // heavy striped plate
                _ => (3, 7)  // plain lower plate
            };
        }

        // A continuous utility chase crosses the upper wall. Periodic junctions and
        // drops align with the chamber/machinery region without becoming collision.
        if (y == 6)
        {
            if (x % 10 == 7) return (1, 3); // junction box
            if (x % 10 == 9) return (1, 1); // corner and cable drop
            if (x % 14 == 5) return (1, 5); // valve/gauge module
            if (x % 18 == 13) return (1, 7); // terminal plate
            return (1, 0); // continuous central conduit
        }
        if (y is 7 or 8 or 9 && x % 10 == 9) return (2, 2);

        // Behind the machinery, tall ribs and occasional access panels break up the
        // wall while keeping the silhouettes of interactive equipment readable.
        if (y >= 10)
        {
            if (y == 11 && x is 11 or 16 or 21 or 26 or 31)
                return x % 2 == 0 ? (2, 3) : (2, 4);
            if (x % 10 == 0) return (2, 0);
            if (x % 14 == 6) return (2, 5); // paired vertical pipes
            if (y == 14 && x % 13 == 4) return (2, 1);
            if (y == 12 && x % 17 == 8) return (2, 6); // extractor fan
            if (y == 13 && x % 19 == 12) return (2, 7); // deep service recess
            if (x % 11 == 0) return y % 2 == 0 ? (0, 2) : (0, 3);
            return WallPanel((x / 3) + ((y / 2) * 3));
        }

        // Upper-wall panels stay calm, with deliberate vents and service seams.
        if (y is 2 or 8 && x % 13 == 6) return (1, 2);
        if (y == 4 && x % 15 == 4) return (1, 3);
        if (y == 3) return (0, 4);
        if (x % 10 == 0) return (0, (y / 2) % 2 == 0 ? 2 : 3);
        return WallPanel((x / 3) + (y * 2));
    }

    private void DrawEnvironment(
        Graphics g,
        Size viewport,
        DestructibleGrid grid,
        ProcessingLine? processingLine)
    {
        if (!ReferenceEquals(_environmentCacheGrid, grid) ||
            !ReferenceEquals(_environmentCacheLine, processingLine) ||
            _environmentCacheRevision != grid.SurfaceRevision ||
            _environmentCacheSize != viewport ||
            _environmentCache is null)
        {
            _environmentCache?.Dispose();
            _environmentCache = new Bitmap(viewport.Width, viewport.Height, PixelFormat.Format32bppPArgb);
            using var cacheGraphics = Graphics.FromImage(_environmentCache);
            DrawBackdrop(cacheGraphics, viewport);
            DrawGridCells(cacheGraphics, grid);
            DrawProcessingLineStaticBack(cacheGraphics, processingLine);
            _environmentCacheGrid = grid;
            _environmentCacheLine = processingLine;
            _environmentCacheRevision = grid.SurfaceRevision;
            _environmentCacheSize = viewport;
        }
        g.DrawImageUnscaled(_environmentCache, 0, 0);
    }

    private void DrawGridCells(Graphics g, DestructibleGrid grid)
    {
        var previousSmoothing = g.SmoothingMode;
        var previousInterpolation = g.InterpolationMode;
        var previousPixelOffset = g.PixelOffsetMode;
        g.SmoothingMode = SmoothingMode.None;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        var tileset = FactoryTileset.Value;
        for (var y = 0; y < grid.Rows; y++)
        for (var x = 0; x < grid.Columns; x++)
        {
            var cell = grid.Cell(x, y);
            if (!cell.IsSolid) continue;
            var rect = new Rectangle(x * grid.CellSize, y * grid.CellSize, grid.CellSize, grid.CellSize);
            var (brush, pen) = cell.Material switch
            {
                CellMaterial.Steel => (_steelBrush, _steelPen),
                CellMaterial.Concrete => (_concreteBrush, _concretePen),
                CellMaterial.Glass => (_glassBrush, _glassPen),
                _ => (_steelBrush, _steelPen)
            };
            if (tileset is not null)
            {
                var (row, column) = SelectFactoryTile(grid, x, y, cell);
                var source = new Rectangle(
                    column * FactoryTileSize,
                    row * FactoryTileSize,
                    FactoryTileSize,
                    FactoryTileSize);
                g.DrawImage(tileset, rect, source, GraphicsUnit.Pixel);
            }
            else
            {
                g.FillRectangle(brush, rect);
            }
            g.DrawRectangle(pen, rect);

            if (cell.IsDestructible && cell.Health < (cell.Material == CellMaterial.Glass ? 28f : 70f) * 0.72f)
            {
                g.DrawLine(_crackPen, rect.Left + 5, rect.Top + 4, rect.Left + 18, rect.Top + 15);
                g.DrawLine(_crackPen, rect.Left + 18, rect.Top + 15, rect.Right - 4, rect.Bottom - 6);
            }
        }
        g.SmoothingMode = previousSmoothing;
        g.InterpolationMode = previousInterpolation;
        g.PixelOffsetMode = previousPixelOffset;
    }

    internal static (int Row, int Column) SelectFactoryTile(
        DestructibleGrid grid,
        int x,
        int y,
        MaterialCell cell)
    {
        bool SameMaterial(int checkX, int checkY) =>
            checkX >= 0 && checkY >= 0 && checkX < grid.Columns && checkY < grid.Rows &&
            grid.Cell(checkX, checkY).Material == cell.Material;

        var sameLeft = SameMaterial(x - 1, y);
        var sameRight = SameMaterial(x + 1, y);
        var sameUp = SameMaterial(x, y - 1);
        var sameDown = SameMaterial(x, y + 1);

        if (cell.Material == CellMaterial.Glass)
        {
            // A glass assembly uses framed windows throughout and diagonal
            // reinforcement only at its top/bottom caps.
            return (!sameUp || !sameDown) ? (2, 1) : (2, 0);
        }

        if (cell.Material == CellMaterial.Concrete)
        {
            // Concrete masses remain calm. Service hatches occur only in the
            // protected center of a wide column, never randomly on an edge.
            var interior = sameLeft && sameRight && sameUp && sameDown;
            if (interior && y % 5 == 0) return (1, 3);
            if (sameLeft && sameRight) return (1, 2);
            return (1, 0);
        }

        if (cell.Material == CellMaterial.Steel)
        {
            // The foundation is a continuous horizontal structural course.
            if (y == grid.Rows - 1) return (3, 2);

            // Arena walls mirror one another. Braces and vents repeat on a
            // deliberate ten-tile service bay instead of a coordinate hash.
            if (x == 0 || x == grid.Columns - 1)
            {
                var bayPosition = y % 10;
                if (bayPosition == 2) return (0, 2);
                if (bayPosition == 7) return (0, 3);
                return (0, 1);
            }

            var verticalMember = (sameUp || sameDown) && !sameLeft && !sameRight;
            if (verticalMember) return (0, 1);
            var horizontalMember = (sameLeft || sameRight) && !sameUp && !sameDown;
            if (horizontalMember) return (3, 2);
            if ((sameLeft || sameRight) && (sameUp || sameDown)) return (3, 3);
            return (0, 0);
        }

        return (0, 0);
    }

    private static Bitmap? LoadFactoryTileset()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "FactoryTileset.png");
            if (!File.Exists(path)) return null;
            using var source = new Bitmap(path);
            if (source.Width != FactoryTileSize * 4 || source.Height != FactoryTileSize * 4) return null;
            return new Bitmap(source);
        }
        catch
        {
            // Rendering retains a neutral-color fallback if an unpackaged
            // development build omits the optional art asset.
            return null;
        }
    }

    private static Bitmap? LoadFactoryBackdropTileset()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "FactoryBackdropTileset.png");
            if (!File.Exists(path)) return null;
            using var source = new Bitmap(path);
            if (source.Width != FactoryTileSize * 8 || source.Height != FactoryTileSize * 4) return null;
            return new Bitmap(source);
        }
        catch
        {
            return null;
        }
    }

    private static Bitmap? LoadHoldingChamberSprite()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "HoldingChamber.png");
            if (!File.Exists(path)) return null;
            using var source = new Bitmap(path);
            if (source.Width != 192 || source.Height != 192) return null;
            return new Bitmap(source);
        }
        catch
        {
            return null;
        }
    }

    private static Bitmap? LoadAsset(string fileName)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
            if (!File.Exists(path)) return null;
            using var source = new Bitmap(path);
            return new Bitmap(source);
        }
        catch
        {
            return null;
        }
    }

    private static Bitmap? LoadScaledAsset(string fileName, int width, int height)
    {
        using var source = LoadAsset(fileName);
        if (source is null) return null;
        var scaled = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(scaled);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.CompositingQuality = CompositingQuality.HighSpeed;
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.DrawImage(source,
            new Rectangle(0, 0, width, height),
            new Rectangle(0, 0, source.Width, source.Height),
            GraphicsUnit.Pixel);
        return scaled;
    }

    private void DrawProcessingLineBack(Graphics g, ProcessingLine? line)
    {
        if (line is null) return;
        var interpolation = g.InterpolationMode;
        var smoothing = g.SmoothingMode;
        var pixelOffset = g.PixelOffsetMode;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.SmoothingMode = SmoothingMode.None;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        DrawCartDock(g, line);
        DrawBasinBack(g, line);

        for (var i = 0; i < line.Bays.Count; i++)
        {
            var bay = line.Bays[i];
            DrawMachineStatusGlow(g, line, bay, i);
        }

        g.InterpolationMode = interpolation;
        g.SmoothingMode = smoothing;
        g.PixelOffsetMode = pixelOffset;
    }

    private static void DrawProcessingLineStaticBack(Graphics g, ProcessingLine? line)
    {
        if (line is null) return;
        var interpolation = g.InterpolationMode;
        var smoothing = g.SmoothingMode;
        var pixelOffset = g.PixelOffsetMode;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.SmoothingMode = SmoothingMode.None;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        DrawBasinStaticBack(g, line);
        for (var i = 0; i < line.Bays.Count; i++)
        {
            var bay = line.Bays[i];
            var sprite = i switch
            {
                0 => CrusherFrameDisplay.Value,
                1 => DrillFrameDisplay.Value,
                2 => DrumHousingDisplay.Value,
                3 => VacuumFrameDisplay.Value,
                4 => FilterFrameDisplay.Value,
                _ => MachineBaySprite.Value
            };
            var bounds = i switch
            {
                0 => new RectangleF(bay.CenterX - 60f, line.DeckY - 176f, 120f, 168f),
                2 => new RectangleF(bay.CenterX - 66f, line.DeckY - 164f, 132f, 164f),
                _ => new RectangleF(bay.CenterX - 54f, line.DeckY - 151f, 108f, 151f)
            };
            if (sprite is not null)
            {
                if (i == 2)
                    g.DrawImage(sprite, bounds,
                        new RectangleF(0f, 0f, sprite.Width, sprite.Height), GraphicsUnit.Pixel);
                else
                    g.DrawImageUnscaled(sprite, (int)MathF.Round(bounds.X), (int)MathF.Round(bounds.Y));
            }
            else
            {
                using var fallback = new Pen(Color.FromArgb(115, 135, 148), 5f);
                g.DrawRectangle(fallback, bounds.X, bounds.Y, bounds.Width, bounds.Height);
            }
            DrawMachinePlatform(g, bay, line.DeckY);
        }

        g.InterpolationMode = interpolation;
        g.SmoothingMode = smoothing;
        g.PixelOffsetMode = pixelOffset;
    }

    private static RectangleF DrainPipeBounds(ProcessingBay bay, ProcessingLine line)
    {
        var top = line.DeckY + 8f;
        var bottom = line.Basin.Top + 24f;
        return new RectangleF(bay.CenterX - 22f, top, 44f, MathF.Max(12f, bottom - top));
    }

    private static void DrawDrainPipeSprite(Graphics g, ProcessingBay bay, ProcessingLine line, int bayIndex)
    {
        var sprite = RustyDrainPipeSprite.Value;
        var bounds = DrainPipeBounds(bay, line);
        if (sprite is not null && sprite.Width >= 32 * 5)
        {
            const int frameWidth = 32;
            var frame = Math.Clamp(bayIndex, 0, 4);
            g.DrawImage(sprite, bounds,
                new RectangleF(frame * frameWidth, 0f, frameWidth, sprite.Height), GraphicsUnit.Pixel);
            return;
        }

        using var pipe = new SolidBrush(Color.FromArgb(255, 60, 77, 86));
        using var pipeEdge = new Pen(Color.FromArgb(255, 119, 145, 154), 2f);
        g.FillRectangle(pipe, bay.CenterX - 7f, bounds.Top, 14f, bounds.Height);
        g.DrawRectangle(pipeEdge, bay.CenterX - 7f, bounds.Top, 14f, bounds.Height);
    }

    private static void DrawDrainPipeExterior(Graphics g, ProcessingBay bay, ProcessingLine line, int bayIndex)
    {
        var state = g.Save();
        g.SetClip(new RectangleF(bay.CenterX - 24f, line.DeckY, 48f,
            MathF.Max(1f, line.Basin.Top - line.DeckY + 1f)), CombineMode.Intersect);
        DrawDrainPipeSprite(g, bay, line, bayIndex);
        g.Restore(state);
    }

    private static void DrawDrainPipeInterior(Graphics g, ProcessingBay bay, ProcessingLine line, int bayIndex)
    {
        var basin = line.Basin;
        var state = g.Save();
        g.SetClip(new RectangleF(basin.Left + 5f, basin.Top + 4f, basin.Width - 10f, 36f),
            CombineMode.Intersect);
        DrawDrainPipeSprite(g, bay, line, bayIndex);

        using var rim = new SolidBrush(Color.FromArgb(255, 91, 108, 113));
        using var outlet = new SolidBrush(Color.FromArgb(255, 5, 9, 12));
        using var submergedBlood = new SolidBrush(Color.FromArgb(132, 105, 3, 17));
        using var bloodDark = new SolidBrush(Color.FromArgb(215, 94, 2, 15));
        using var bloodWet = new SolidBrush(Color.FromArgb(235, 195, 5, 21));
        using var glass = new SolidBrush(Color.FromArgb(80, 51, 91, 100));
        var outletY = DrainPipeBounds(bay, line).Bottom;

        // Only the portion physically submerged by the conserved liquid surface
        // receives a broad coating. A low basin leaves the pipe body clean.
        var surfaceY = basin.SurfaceYAt(bay.CenterX);
        if (surfaceY <= outletY + 1f)
        {
            var submergedTop = Math.Clamp(surfaceY, basin.Top + 5f, outletY);
            var submergedHeight = MathF.Max(2f, outletY - submergedTop + 3f);
            g.FillRectangle(submergedBlood, bay.CenterX - 8f, submergedTop, 16f, submergedHeight);
            g.FillRectangle(bloodDark, bay.CenterX - 8f, submergedTop, 2f, submergedHeight);
            g.FillRectangle(bloodWet, bay.CenterX + 6f, submergedTop + 2f, 2f,
                MathF.Max(2f, submergedHeight - 3f));
        }

        g.FillRectangle(rim, bay.CenterX - 10f, outletY - 3f, 20f, 7f);
        g.FillRectangle(outlet, bay.CenterX - 7f, outletY, 14f, 5f);
        var stain = basin.PipeStainNear(bay.CenterX);
        if (stain is { } coating)
        {
            // Fresh discharge marks only the lip it actually crosses. Avoid the
            // old vertical red stripe that read as blood coming through pipe metal.
            var patch = 2f + MathF.Round(coating.Amount * 3f);
            g.FillRectangle(bloodDark, bay.CenterX - 10f, outletY - 2f, patch, 4f);
            g.FillRectangle(bloodWet, bay.CenterX - 9f, outletY, MathF.Max(2f, patch - 1f), 2f);
            g.FillRectangle(bloodDark, bay.CenterX + 10f - patch, outletY - 1f, patch, 4f);
            if ((coating.Variation & 1) != 0)
                g.FillRectangle(bloodWet, bay.CenterX + 7f, outletY + 1f, 2f, 3f);
        }
        // Reapply the glass tint after occluding in-pipe particles so the interior
        // segment remains visually behind the tank window.
        g.FillRectangle(glass, bay.CenterX - 23f, basin.Top + 4f, 46f, 36f);
        g.Restore(state);
    }

    private static void DrawBasinStaticBack(Graphics g, ProcessingLine line)
    {
        var basin = line.Basin;
        using var interior = new SolidBrush(Color.FromArgb(255, 12, 18, 23));
        g.FillRectangle(interior, basin.Left, basin.Top, basin.Width, basin.Height);

        var endcap = BasinEndcapDisplay.Value;
        if (endcap is null) return;
        g.DrawImage(endcap, new RectangleF(basin.Left - 20f, basin.Top - 2f, 24f, basin.Height + 4f),
            new RectangleF(0f, 0f, endcap.Width, endcap.Height), GraphicsUnit.Pixel);
        var state = g.Save();
        g.TranslateTransform(basin.Right + 20f, basin.Top - 2f);
        g.ScaleTransform(-1f, 1f);
        g.DrawImage(endcap, new RectangleF(0f, 0f, 24f, basin.Height + 4f),
            new RectangleF(0f, 0f, endcap.Width, endcap.Height), GraphicsUnit.Pixel);
        g.Restore(state);
    }

    private void DrawBasinBack(Graphics g, ProcessingLine line)
    {
        var basin = line.Basin;
        using var glass = new SolidBrush(Color.FromArgb(80, 51, 91, 100));
        using var bubble = new SolidBrush(Color.FromArgb(165, 255, 92, 87));

        // Diego is intentionally dormant for now. Keep the conditional here so
        // re-enabling the basin entity later restores the established layering.
        DrawBasinFluid(g, basin);
        DrawBasinSuspendedDrops(g, basin);
        if (BloodBasin.DiegoEnabled) DrawDiego(g, basin);

        var bubbleCount = BasinBubbleCountForLevel(basin.FluidLevel01);
        for (var i = 0; i < bubbleCount; i++)
        {
            var normalizedX = ((i * 37 + 13) % 97) / 97f;
            var x = basin.Left + 12f + normalizedX * (basin.Width - 24f);
            // Bubble paths use the conserved mean depth. Slosh and newly
            // dissolving drops may move the drawn surface, but cannot change the
            // modulo range and teleport every bubble at once.
            var localDepth = basin.AverageFluidHeight;
            if (localDepth < 7f) continue;
            var travel = (basin.BubblePhase * (7f + i % 4) + i * 11.3f) % MathF.Max(3f, localDepth - 3f);
            var y = basin.Bottom - 10f - travel;
            var size = 2f + i % 3;
            g.FillRectangle(bubble, MathF.Round(x - size * 0.5f), MathF.Round(y - size * 0.5f), size, size);
        }

        DrawBasinInteriorStains(g, basin);
        g.FillRectangle(glass, basin.Left + 5f, basin.Top + 6f, basin.Width - 10f, basin.Height - 12f);

    }

    private static void DrawBasinSuspendedDrops(Graphics g, BloodBasin basin)
    {
        if (basin.SuspendedDrops.Count == 0) return;
        var state = g.Save();
        g.SetClip(new RectangleF(basin.Left + 5f, basin.FluidTop, basin.Width - 10f,
            basin.FluidBottom - basin.FluidTop), CombineMode.Intersect);
        using var shadow = new SolidBrush(Color.FromArgb(225, 105, 3, 17));
        using var body = new SolidBrush(Color.FromArgb(238, 211, 9, 24));
        using var glint = new SolidBrush(Color.FromArgb(220, 255, 48, 45));
        foreach (var drop in basin.SuspendedDrops)
        {
            var remaining = drop.InitialVolume <= 0f ? 0f : drop.RemainingVolume / drop.InitialVolume;
            var radius = MathF.Max(1f, drop.Radius * MathF.Sqrt(Math.Clamp(remaining, 0.08f, 1f)));
            FillPixelOctagon(g, shadow, new Vector2(drop.X + 1f, drop.Y + 1f),
                radius * 2f + 2f, radius * 2f + 2f, 2f);
            FillPixelOctagon(g, body, new Vector2(drop.X, drop.Y), radius * 2f, radius * 2f, 1f);
            if (radius >= 2f) g.FillRectangle(glint, MathF.Round(drop.X - 1f), MathF.Round(drop.Y - 1f), 1f, 1f);
        }
        g.Restore(state);
    }

    private static void DrawBasinInteriorStains(Graphics g, BloodBasin basin)
    {
        if (basin.InteriorStains.Count == 0) return;
        var state = g.Save();
        g.SetClip(
            new RectangleF(basin.Left + 4f, basin.Top + 5f, basin.Width - 8f, basin.Height - 11f),
            CombineMode.Intersect);
        using var wet = new SolidBrush(Color.FromArgb(178, 194, 7, 22));
        using var drying = new SolidBrush(Color.FromArgb(142, 132, 5, 18));
        using var dry = new SolidBrush(Color.FromArgb(104, 82, 8, 17));
        using var shine = new SolidBrush(Color.FromArgb(135, 255, 38, 31));

        foreach (var stain in basin.InteriorStains)
        {
            var brush = stain.Wetness > 0.48f ? wet : stain.Wetness > 0.08f ? drying : dry;
            var width = MathF.Max(2f, MathF.Round(stain.Width * 0.5f) * 2f);
            var x = MathF.Round((stain.X - width * 0.5f) * 0.5f) * 2f;
            var cursorY = MathF.Round(stain.Y * 0.5f) * 2f;
            var remaining = MathF.Min(stain.Length, basin.Bottom - 7f - cursorY);
            var seed = (uint)(stain.Variation + 1) * 0x9E3779B9u;
            var segment = 0;
            while (remaining > 0.5f)
            {
                seed ^= seed << 13;
                seed ^= seed >> 17;
                seed ^= seed << 5;
                var height = MathF.Min(remaining, 3f + (seed & 3));
                var wander = stain.IsSide ? 0f : ((seed >> 4) % 3 - 1f) * 2f;
                var taperedWidth = MathF.Max(2f, width - segment / 4 * 2f);
                g.FillRectangle(brush, x + wander, cursorY, taperedWidth, height);
                cursorY += height + ((seed >> 8) & 1);
                remaining -= height + 1f;
                segment++;
            }
            if (stain.Wetness > 0.42f)
            {
                g.FillRectangle(shine, x, stain.Y, 2f, 2f);
                if ((stain.Variation & 3) == 0)
                    g.FillRectangle(shine, x + width + 2f, stain.Y + 2f, 2f, 2f);
            }
        }
        g.Restore(state);
    }

    private void DrawBasinFluid(Graphics g, BloodBasin basin)
    {
        if (_basinFluidCache is null ||
            _basinFluidCache.Width != BloodBasin.FluidGridWidth ||
            _basinFluidCache.Height != BloodBasin.FluidGridHeight * BasinFluidVisualScaleY)
        {
            _basinFluidCache?.Dispose();
            _basinFluidCache = new Bitmap(
                BloodBasin.FluidGridWidth,
                BloodBasin.FluidGridHeight * BasinFluidVisualScaleY,
                PixelFormat.Format32bppPArgb);
            _basinFluidCacheRevision = -1;
        }

        if (!ReferenceEquals(_basinFluidCacheSource, basin) ||
            _basinFluidCacheRevision != basin.FluidVisualRevision)
        {
            var bounds = new Rectangle(0, 0, _basinFluidCache.Width, _basinFluidCache.Height);
            var data = _basinFluidCache.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try
            {
                var required = Math.Abs(data.Stride) * data.Height;
                if (_basinFluidPixels.Length != required) _basinFluidPixels = new byte[required];
                else Array.Clear(_basinFluidPixels);
                for (var x = 0; x < BloodBasin.FluidGridWidth; x++)
                    _basinFluidDepths[x] = basin.VisualFluidDepthAt(x);
                for (var y = 0; y < _basinFluidCache.Height; y++)
                for (var x = 0; x < BloodBasin.FluidGridWidth; x++)
                {
                    var depthFromBottom = BloodBasin.FluidGridHeight -
                                          (y + 1f) / BasinFluidVisualScaleY;
                    var fill = Math.Clamp(
                        (_basinFluidDepths[x] - depthFromBottom) * BasinFluidVisualScaleY,
                        0f,
                        1f);
                    if (fill <= 0f) continue;
                    var previousDepthFromBottom = BloodBasin.FluidGridHeight -
                                                  y / (float)BasinFluidVisualScaleY;
                    var surface = y == 0 || _basinFluidDepths[x] <= previousDepthFromBottom;
                    var variation = (x * 19 + y * 37) & 15;
                    var alpha = (int)MathF.Round((surface ? 226f : 188f) * fill);
                    var red = surface ? 238 + variation / 4 : 166 + variation;
                    var green = surface ? 16 + variation / 3 : 3 + variation / 5;
                    var blue = surface ? 24 + variation / 4 : 16 + variation / 3;
                    var rowOffset = data.Stride >= 0
                        ? y * data.Stride
                        : (data.Height - 1 - y) * -data.Stride;
                    var offset = rowOffset + x * 4;
                    _basinFluidPixels[offset] = (byte)(blue * alpha / 255);
                    _basinFluidPixels[offset + 1] = (byte)(green * alpha / 255);
                    _basinFluidPixels[offset + 2] = (byte)(red * alpha / 255);
                    _basinFluidPixels[offset + 3] = (byte)alpha;
                }
                Marshal.Copy(_basinFluidPixels, 0, data.Scan0, required);
            }
            finally
            {
                _basinFluidCache.UnlockBits(data);
            }
            _basinFluidCacheSource = basin;
            _basinFluidCacheRevision = basin.FluidVisualRevision;
        }

        g.DrawImage(
            _basinFluidCache,
            new RectangleF(basin.Left, basin.FluidTop, basin.Width, basin.FluidBottom - basin.FluidTop),
            new RectangleF(0f, 0f, _basinFluidCache.Width, _basinFluidCache.Height),
            GraphicsUnit.Pixel);
    }

    internal static int BasinBubbleCountForLevel(float level01)
    {
        if (level01 < BasinBubbleThreshold) return 0;
        var postThreshold = Math.Clamp(
            (level01 - BasinBubbleThreshold) / (1f - BasinBubbleThreshold),
            0f,
            1f);
        return Math.Min(14, 1 + (int)MathF.Floor(postThreshold * 13f));
    }

    private static void DrawDiego(Graphics g, BloodBasin basin)
    {
        const int frameWidth = 80;
        const int frameHeight = 64;
        var sheet = DiegoSpriteSheet.Value;
        if (sheet is null || sheet.Width < frameWidth * 12 || sheet.Height < frameHeight) return;

        int frame;
        if (basin.CreatureIsFeeding)
        {
            frame = basin.CreatureDrinkTime < 1.12f
                ? 4 + Math.Clamp((int)(basin.CreatureDrinkTime / 0.16f), 0, 7)
                : 8 + (int)(basin.CreatureDrinkTime * 5f) % 3;
        }
        else
        {
            frame = (int)(basin.CreaturePhase / (MathF.PI * 2f) * 4f) & 3;
        }

        var growth = Math.Clamp((basin.CreatureScale - 0.48f) / (3.15f - 0.48f), 0f, 1f);
        var visualScale = 0.43f + growth * 0.85f;
        var width = frameWidth * visualScale;
        var height = frameHeight * visualScale;
        var bounds = new RectangleF(
            basin.CreatureX - width * 0.5f,
            basin.Bottom - height - 8f,
            width,
            height);
        var source = new RectangleF(frame * frameWidth, 0f, frameWidth, frameHeight);

        var state = g.Save();
        g.SetClip(new RectangleF(basin.Left + 7f, basin.Top + 5f, basin.Width - 14f, basin.Height - 12f));
        // Every authored Diego frame faces left. Preserve it while travelling
        // left and mirror only for rightward movement.
        if (DiegoMirrorsForDirection(basin.CreatureDirection))
        {
            g.TranslateTransform(bounds.Left + bounds.Right, 0f);
            g.ScaleTransform(-1f, 1f);
        }
        g.DrawImage(sheet, bounds, source, GraphicsUnit.Pixel);
        g.Restore(state);
    }

    internal static bool DiegoMirrorsForDirection(float direction) => direction > 0f;

    private static void DrawBasinForeground(Graphics g, ProcessingLine line)
    {
        var basin = line.Basin;
        using var rimDark = new SolidBrush(Color.FromArgb(255, 20, 29, 35));
        using var rim = new SolidBrush(Color.FromArgb(255, 95, 119, 128));
        using var highlight = new SolidBrush(Color.FromArgb(255, 156, 182, 188));
        using var hazard = new SolidBrush(Color.FromArgb(255, 230, 181, 58));
        g.FillRectangle(rimDark, basin.Left - 4f, basin.Top - 5f, basin.Width + 8f, 12f);
        g.FillRectangle(rim, basin.Left, basin.Top - 5f, basin.Width, 8f);
        g.FillRectangle(highlight, basin.Left, basin.Top - 5f, basin.Width, 2f);
        g.FillRectangle(rimDark, basin.Left - 4f, basin.Bottom - 8f, basin.Width + 8f, 12f);
        for (var x = basin.Left + 12f; x < basin.Right - 8f; x += 48f)
            g.FillRectangle(hazard, x, basin.Bottom - 6f, 18f, 3f);

        var monitor = BasinMonitorSprite.Value;
        var monitorBounds = new RectangleF(basin.Left + 9f, basin.Top + 15f, 32f, 64f);
        if (monitor is not null)
            g.DrawImage(monitor, monitorBounds,
                new RectangleF(0f, 0f, monitor.Width, monitor.Height), GraphicsUnit.Pixel);
        var fillHeight = 35f * basin.FluidLevel01;
        using var levelDark = new SolidBrush(Color.FromArgb(220, 103, 6, 20));
        using var levelBright = new SolidBrush(Color.FromArgb(225, 235, 18, 24));
        g.FillRectangle(levelDark, monitorBounds.Left + 9f, monitorBounds.Top + 45f - fillHeight, 14f, fillHeight);
        if (fillHeight > 1f)
            g.FillRectangle(levelBright, monitorBounds.Left + 9f, monitorBounds.Top + 45f - fillHeight, 14f, 1f);
        using var meterFont = new Font("Consolas", 7f, FontStyle.Bold, GraphicsUnit.Point);
        using var meterText = new SolidBrush(Color.FromArgb(245, 197, 231, 229));
        using var meterBack = new SolidBrush(Color.FromArgb(220, 12, 18, 22));
        var percent = Math.Clamp((int)MathF.Round(basin.FluidLevel01 * 100f), 0, 100);
        g.FillRectangle(meterBack, monitorBounds.Right + 2f, monitorBounds.Top + 21f, 39f, 25f);
        g.DrawString($"{percent:00}%\n{basin.CurrentFluidVolume:0000}", meterFont, meterText,
            monitorBounds.Right + 4f, monitorBounds.Top + 21f);
    }

    private static void DrawMachinePlatform(Graphics g, ProcessingBay bay, float deckY)
    {
        using var shadow = new SolidBrush(Color.FromArgb(255, 17, 23, 28));
        using var steel = new SolidBrush(Color.FromArgb(255, 69, 82, 91));
        using var edge = new SolidBrush(Color.FromArgb(255, 132, 151, 161));
        using var hazard = new SolidBrush(Color.FromArgb(255, 230, 181, 58));
        g.FillRectangle(shadow, bay.Left - 2f, deckY + 4f, bay.Width + 4f, 10f);
        g.FillRectangle(steel, bay.Left, deckY, bay.Width, 10f);
        g.FillRectangle(edge, bay.Left, deckY, bay.Width, 3f);
        g.FillRectangle(hazard, bay.Left + 4f, deckY + 5f, 7f, 3f);
        g.FillRectangle(hazard, bay.Right - 11f, deckY + 5f, 7f, 3f);
    }

    private static void DrawCartDock(Graphics g, ProcessingLine line)
    {
        var walk = line.WalkwayBounds;
        using var shadow = new SolidBrush(Color.FromArgb(255, 18, 25, 30));
        using var steel = new SolidBrush(Color.FromArgb(255, 71, 84, 93));
        using var edge = new SolidBrush(Color.FromArgb(255, 126, 145, 156));
        using var hazard = new SolidBrush(Color.FromArgb(255, 224, 176, 55));
        g.FillRectangle(shadow, walk.Left, walk.Top + 5f, walk.Width, walk.Height);
        g.FillRectangle(steel, walk);
        g.FillRectangle(edge, walk.Left, walk.Top, walk.Width, 3f);
        for (var x = walk.Left + 8f; x < walk.Right; x += 22f)
            g.FillRectangle(hazard, x, walk.Top + 5f, 10f, 3f);

        var door = line.DoorwayBounds;
        g.FillRectangle(shadow, door.Left - 7f, door.Top - 8f, door.Width + 7f, door.Height + 8f);
        g.FillRectangle(edge, door.Left - 5f, door.Top - 6f, 5f, door.Height + 6f);
        g.FillRectangle(edge, door.Left - 5f, door.Top - 6f, door.Width + 5f, 6f);
        var shutterHeight = (door.Height - 4f) * (1f - line.DoorOpenness);
        if (shutterHeight > 1f)
        {
            g.FillRectangle(steel, door.Left, door.Top, door.Width, shutterHeight);
            for (var y = door.Top + 7f; y < door.Top + shutterHeight; y += 9f)
                g.FillRectangle(edge, door.Left, y, door.Width, 2f);
        }
        g.FillRectangle(hazard, door.Left - 7f, walk.Top - 7f, 7f, 7f);
    }

    private void DrawBloodShop(Graphics g, ProcessingLine line)
    {
        var bounds = line.BloodShopBounds;
        var contentTop = line.BloodShopContentTop;
        using var shadow = new SolidBrush(Color.FromArgb(235, 3, 7, 10));
        using var cabinet = new SolidBrush(Color.FromArgb(255, 18, 31, 37));
        using var inset = new SolidBrush(Color.FromArgb(255, 8, 16, 20));
        using var steel = new Pen(Color.FromArgb(255, 73, 96, 105), 4f)
        {
            LineJoin = LineJoin.Miter
        };
        using var edge = new SolidBrush(Color.FromArgb(255, 124, 151, 155));
        using var cyan = new SolidBrush(Color.FromArgb(255, 92, 219, 205));
        using var amber = new SolidBrush(Color.FromArgb(255, 225, 178, 50));
        using var red = new SolidBrush(Color.FromArgb(255, 218, 42, 50));
        using var dim = new SolidBrush(Color.FromArgb(255, 65, 82, 87));
        using var text = new SolidBrush(Color.FromArgb(255, 191, 226, 215));
        using var offText = new SolidBrush(Color.FromArgb(255, 97, 116, 117));

        // Match the receiving tub's exact passive-collision profile. The cabinet
        // fills the corner below it without painting over the transparent tub art
        // or the basin's left endcap.
        var tub = line.ReceivingTubSurface;
        _shopPanelPolygon[0] = new PointF(tub[0].X, tub[0].Y);
        _shopPanelPolygon[1] = new PointF(tub[1].X, tub[1].Y);
        _shopPanelPolygon[2] = new PointF(tub[2].X, tub[2].Y);
        _shopPanelPolygon[3] = new PointF(tub[3].X, tub[3].Y);
        _shopPanelPolygon[4] = new PointF(bounds.Right, line.BloodShopTopAt(bounds.Right));
        _shopPanelPolygon[5] = new PointF(bounds.Right, bounds.Bottom);
        _shopPanelPolygon[6] = new PointF(bounds.Left, bounds.Bottom);
        var shadowState = g.Save();
        g.TranslateTransform(0f, 4f);
        g.FillPolygon(shadow, _shopPanelPolygon);
        g.Restore(shadowState);
        g.FillPolygon(cabinet, _shopPanelPolygon);
        g.DrawPolygon(steel, _shopPanelPolygon);
        g.FillRectangle(inset, bounds.Left + 9f, contentTop + 2f, bounds.Width - 18f, 31f);
        g.FillRectangle(cyan, bounds.Left + 9f, contentTop + 34f, bounds.Width - 18f, 2f);
        g.FillRectangle(edge, bounds.Left + 5f, contentTop + 5f, 3f, 3f);
        g.FillRectangle(edge, bounds.Right - 8f, contentTop + 5f, 3f, 3f);
        g.FillRectangle(edge, bounds.Left + 5f, bounds.Bottom - 8f, 3f, 3f);
        g.FillRectangle(edge, bounds.Right - 8f, bounds.Bottom - 8f, 3f, 3f);

        g.DrawString("BLOOD EXCHANGE", _shopFont, text, bounds.Left + 14f, contentTop + 4f);
        g.DrawString($"RESERVE {line.Basin.SpendableBlood:00000}", _shopSmallFont,
            line.MachineryLockedByStorage ? red : cyan, bounds.Left + 14f, contentTop + 20f);
        if (line.MachineryLockedByStorage)
        {
            g.FillRectangle(red, bounds.Right - 64f, contentTop + 7f, 48f, 10f);
            g.DrawString("LINE LOCK", _shopSmallFont, inset, bounds.Right - 61f, contentTop + 6f);
        }

        for (var i = 0; i < line.BloodShopItems.Count; i++)
        {
            var item = line.BloodShopItems[i];
            var itemBounds = line.BloodShopItemBounds(i);
            var affordable = line.Basin.SpendableBlood + 0.001f >= item.Cost;
            var accent = item.Purchased ? amber : affordable ? cyan : red;
            g.FillRectangle(inset, itemBounds);
            g.FillRectangle(dim, itemBounds.Left, itemBounds.Top, 3f, itemBounds.Height);
            g.FillRectangle(accent, itemBounds.Left + 3f, itemBounds.Top + 3f, 4f, itemBounds.Height - 6f);
            g.DrawString(item.Label, _shopSmallFont, item.Purchased ? offText : text,
                itemBounds.Left + 11f, itemBounds.Top + 2f);
            g.DrawString(item.Purchased ? "INSTALLED" : $"{item.Cost:0}", _shopSmallFont,
                item.Purchased ? amber : affordable ? cyan : offText,
                itemBounds.Right - 53f, itemBounds.Top + 11f);
        }

        var relief = line.BloodShopReliefBounds;
        var canVent = line.Basin.SpendableBlood + 0.001f >= ProcessingLine.ReliefValveCost;
        g.FillRectangle(inset, relief);
        g.FillRectangle(canVent ? amber : dim, relief.Left + 3f, relief.Top + 3f, 14f, relief.Height - 6f);
        g.FillRectangle(shadow, relief.Left + 7f, relief.Top + 6f, 6f, relief.Height - 12f);
        g.DrawString("PURGE", _shopFont, canVent ? amber : offText, relief.Left + 23f, relief.Top + 1f);
        g.DrawString($"-{ProcessingLine.ReliefValveCost:0}", _shopSmallFont,
            canVent ? cyan : offText, relief.Right - 50f, relief.Top + 11f);

    }

    private void DrawProcessingLineFront(Graphics g, ProcessingLine? line)
    {
        if (line is null) return;
        var interpolation = g.InterpolationMode;
        var smoothing = g.SmoothingMode;
        var pixelOffset = g.PixelOffsetMode;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.SmoothingMode = SmoothingMode.None;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        DrawBloodShop(g, line);
        DrawReceivingTub(g, line);
        DrawCartForeground(g, line);
        DrawCartStatus(g, line);
        for (var i = 0; i < line.Bays.Count; i++)
        {
            DrawDrainPipeExterior(g, line.Bays[i], line, i);
            DrawMachineStatusLight(g, line, line.Bays[i], i);
        }

        var crusher = line.Bays[0];
        DrawActuatorColumn(g, crusher.CenterX, line.DeckY - 150f, line.CrusherHeadTop + 10f, 17f);
        var head = CrusherHeadSprite.Value;
        var headBounds = new RectangleF(crusher.CenterX - 48f, line.CrusherHeadTop, 96f, 48f);
        if (head is not null)
            g.DrawImage(head, headBounds, new RectangleF(0f, 0f, head.Width, head.Height), GraphicsUnit.Pixel);

        var button = CrusherButtonSprite.Value;
        var buttonOffset = line.CrusherButtonHeld ? 2f : 0f;
        var buttonBounds = new RectangleF(
            line.CrusherButtonCenter.X - 16f + buttonOffset,
            line.CrusherButtonCenter.Y - 16f,
            32f, 32f);
        if (button is not null)
            g.DrawImage(button, buttonBounds, new RectangleF(0f, 0f, button.Width, button.Height), GraphicsUnit.Pixel);

        var drill = line.Bays[1];
        DrawActuatorColumn(g, drill.CenterX, line.DeckY - 151f, line.DrillHeadTop + 18f, 15f);
        var drillHead = DrillHeadSprite.Value;
        var spinOffset = MathF.Sin(line.DrillSpin) * 1.5f;
        var drillHeadBounds = new RectangleF(
            drill.CenterX - 24f + spinOffset,
            line.DrillHeadTop,
            48f,
            80f);
        if (drillHead is not null)
            g.DrawImage(drillHead, drillHeadBounds,
                new RectangleF(0f, 0f, drillHead.Width, drillHead.Height), GraphicsUnit.Pixel);

        var drillLever = line.DrillLeverHeld ? DrillLeverHeldSprite.Value : DrillLeverIdleSprite.Value;
        var drillLeverBounds = new RectangleF(
            line.DrillLeverCenter.X - 20f,
            line.DrillLeverCenter.Y - 30f,
            40f,
            60f);
        if (drillLever is not null)
            g.DrawImage(drillLever, drillLeverBounds,
                new RectangleF(0f, 0f, drillLever.Width, drillLever.Height), GraphicsUnit.Pixel);


        DrawDrum(g, line);

        var vacuum = line.Bays[3];
        DrawVacuumHolster(g, line);
        DrawVacuumHose(g, line);
        using var pressureOff = new SolidBrush(Color.FromArgb(255, 40, 53, 61));
        using var pressureOn = new SolidBrush(line.VacuumContact
            ? Color.FromArgb(255, 101, 230, 223)
            : Color.FromArgb(255, 230, 181, 58));
        g.FillRectangle(pressureOff, vacuum.CenterX - 31f, line.DeckY - 138f, 62f, 8f);
        g.FillRectangle(pressureOn, vacuum.CenterX - 29f, line.DeckY - 136f,
            58f * line.VacuumProgress, 4f);

        var filter = line.Bays[4];
        var filterKnob = FilterKnobSprite.Value;
        if (filterKnob is not null)
            g.DrawImage(filterKnob,
                new RectangleF(line.FilterKnobCenter.X - 15f, line.FilterKnobCenter.Y - 12f, 30f, 25f),
                new RectangleF(0f, 0f, filterKnob.Width, filterKnob.Height), GraphicsUnit.Pixel);
        if (line.FilterLaserActive)
        {
            using var laserCore = new SolidBrush(Color.FromArgb(230, 101, 230, 223));
            using var laserGlow = new SolidBrush(Color.FromArgb(55, 101, 230, 223));
            for (var beam = -2; beam <= 2; beam++)
            {
                var x = line.FilterLaserX + beam * 4f;
                g.FillRectangle(laserGlow, x - 2f, line.DeckY - 139f, 5f, 137f);
                g.FillRectangle(laserCore, x, line.DeckY - 139f, 1f, 137f);
            }
        }
        for (var i = 0; i < line.Bays.Count; i++)
            DrawDrainPipeInterior(g, line.Bays[i], line, i);
        DrawBasinForeground(g, line);

        g.InterpolationMode = interpolation;
        g.SmoothingMode = smoothing;
        g.PixelOffsetMode = pixelOffset;
    }

    private void DrawDrum(Graphics g, ProcessingLine line)
    {
        var rotor = DrumRotorSprite.Value;
        var center = line.DrumCenter;
        var normalizedAngle = line.DrumAngle / MathF.Tau;
        var rotorFrame = ((int)MathF.Round(normalizedAngle * 8f) % 8 + 8) % 8;
        var diameter = ProcessingLine.DrumOuterRadius * 2f;
        using var drumShadow = new SolidBrush(Color.FromArgb(255, 5, 9, 12));
        using var drumRim = new SolidBrush(Color.FromArgb(255, 72, 91, 99));
        using var drumInside = new SolidBrush(Color.FromArgb(255, 8, 14, 18));
        FillPixelOctagon(g, drumShadow, center + new Vector2(2f, 3f), diameter + 8f, diameter + 8f, 14f);
        FillPixelOctagon(g, drumRim, center, diameter, diameter, 13f);
        FillPixelOctagon(g, drumInside, center,
            ProcessingLine.DrumInteriorRadius * 2f + 2f,
            ProcessingLine.DrumInteriorRadius * 2f + 2f, 11f);
        if (rotor is not null && rotor.Width >= 64 * 8)
        {
            using var attributes = new ImageAttributes();
            var matrix = new ColorMatrix { Matrix33 = 0.62f };
            attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
            g.DrawImage(rotor,
                new Rectangle((int)MathF.Round(center.X - ProcessingLine.DrumOuterRadius),
                    (int)MathF.Round(center.Y - ProcessingLine.DrumOuterRadius),
                    (int)diameter, (int)diameter),
                rotorFrame * 64, 0, 64, 64,
                GraphicsUnit.Pixel,
                attributes);
        }

        if (line.DrumLoading) DrawDrumLoadingLift(g, line);

        if (line.DrumLockedBody is { } drumBody)
        {
            if (!line.DrumLoading)
            {
                var clipState = g.Save();
                using var opening = new GraphicsPath();
                opening.AddEllipse(center.X - ProcessingLine.DrumInteriorRadius,
                    center.Y - ProcessingLine.DrumInteriorRadius,
                    ProcessingLine.DrumInteriorRadius * 2f,
                    ProcessingLine.DrumInteriorRadius * 2f);
                g.SetClip(opening, CombineMode.Intersect);
                DrawBlob(g, drumBody, false, drumBody.VisualRotation, machineLit: true);
                using var glass = new SolidBrush(Color.FromArgb(24, 99, 190, 187));
                g.FillRectangle(glass,
                    center.X - ProcessingLine.DrumInteriorRadius,
                    center.Y - ProcessingLine.DrumInteriorRadius,
                    ProcessingLine.DrumInteriorRadius * 2f,
                    ProcessingLine.DrumInteriorRadius * 2f);
                if (rotor is not null && rotor.Width >= 64 * 8)
                {
                    using var foregroundAttributes = new ImageAttributes();
                    var foregroundMatrix = new ColorMatrix { Matrix33 = 0.20f };
                    foregroundAttributes.SetColorMatrix(foregroundMatrix, ColorMatrixFlag.Default,
                        ColorAdjustType.Bitmap);
                    g.DrawImage(rotor,
                        new Rectangle((int)MathF.Round(center.X - ProcessingLine.DrumOuterRadius),
                            (int)MathF.Round(center.Y - ProcessingLine.DrumOuterRadius),
                            (int)diameter, (int)diameter),
                        rotorFrame * 64, 0, 64, 64,
                        GraphicsUnit.Pixel,
                        foregroundAttributes);
                }
                g.Restore(clipState);
            }
        }

        using var rimHighlight = new SolidBrush(Color.FromArgb(255, 151, 174, 178));
        g.FillRectangle(rimHighlight, center.X - 33f, center.Y - 57f, 66f, 4f);
        g.FillRectangle(rimHighlight, center.X - 33f, center.Y + 53f, 66f, 4f);
        g.FillRectangle(rimHighlight, center.X - 57f, center.Y - 33f, 4f, 66f);
        g.FillRectangle(rimHighlight, center.X + 53f, center.Y - 33f, 4f, 66f);

        // Two hatch leaves remain attached to the drum and separate only after
        // it has aligned downward, making the discharge physically legible.
        var open = MathF.Max(line.DrumDoorOpenness, line.DrumLoadHatchOpenness);
        using var hatchDark = new SolidBrush(Color.FromArgb(255, 9, 15, 19));
        using var hatch = new SolidBrush(Color.FromArgb(255, 85, 108, 116));
        using var hatchEdge = new SolidBrush(Color.FromArgb(255, 183, 198, 198));
        var hatchY = center.Y + 45f + open * 10f;
        var spread = open * 32f;
        g.FillRectangle(hatchDark, center.X - 51f - spread, hatchY - 1f, 51f, 10f);
        g.FillRectangle(hatchDark, center.X + spread, hatchY - 1f, 51f, 10f);
        g.FillRectangle(hatch, center.X - 49f - spread, hatchY, 49f, 7f);
        g.FillRectangle(hatch, center.X + spread, hatchY, 49f, 7f);
        g.FillRectangle(hatchEdge, center.X - 49f - spread, hatchY, 49f, 2f);
        g.FillRectangle(hatchEdge, center.X + spread, hatchY, 49f, 2f);

        var wheel = DrumHandwheelSprite.Value;
        var wheelFrame = ((int)MathF.Round(line.DrumWheelAngle / MathF.Tau * 8f) % 8 + 8) % 8;
        if (wheel is not null && wheel.Width >= 40 * 8)
            g.DrawImage(wheel,
                new RectangleF(line.DrumWheelCenter.X - 20f, line.DrumWheelCenter.Y - 20f, 40f, 40f),
                new RectangleF(wheelFrame * 40f, 0f, 40f, 40f),
                GraphicsUnit.Pixel);

        using var track = new SolidBrush(Color.FromArgb(255, 19, 28, 33));
        using var fill = new SolidBrush(line.DrumFinishing
            ? Color.FromArgb(255, 230, 181, 58)
            : Color.FromArgb(255, 101, 230, 223));
        g.FillRectangle(track, center.X - 43f, line.DeckY - 153f, 86f, 6f);
        g.FillRectangle(fill, center.X - 41f, line.DeckY - 151f, 82f * line.DrumProgress, 2f);

        // Intake is the final Bay 3 foreground pass. No housing, rim, hatch,
        // wheel, or lift component may occlude the blob until the complete
        // loading animation has ended and it is actually behind the drum glass.
        if (line.DrumLoading && line.DrumLockedBody is { } loadingBody)
            DrawBlob(g, loadingBody, false, loadingBody.VisualRotation, machineLit: true);
    }

    private static void DrawDrumLoadingLift(Graphics g, ProcessingLine line)
    {
        var centerX = line.DrumCenter.X;
        var platformY = line.DrumLiftPlatformY;
        using var railShadow = new SolidBrush(Color.FromArgb(255, 7, 12, 16));
        using var rail = new SolidBrush(Color.FromArgb(255, 62, 81, 89));
        using var railEdge = new SolidBrush(Color.FromArgb(255, 137, 159, 164));
        using var piston = new SolidBrush(Color.FromArgb(255, 92, 115, 121));
        using var cyan = new SolidBrush(Color.FromArgb(255, 88, 215, 204));
        using var amber = new SolidBrush(Color.FromArgb(255, 224, 176, 48));

        // Fixed guides and a telescoping ram make the transport path readable:
        // belt height -> open lower hatch -> drum interior.
        g.FillRectangle(railShadow, centerX - 35f, line.DrumCenter.Y + 42f, 7f,
            line.DeckY - line.DrumCenter.Y - 34f);
        g.FillRectangle(railShadow, centerX + 28f, line.DrumCenter.Y + 42f, 7f,
            line.DeckY - line.DrumCenter.Y - 34f);
        g.FillRectangle(rail, centerX - 33f, line.DrumCenter.Y + 42f, 3f,
            line.DeckY - line.DrumCenter.Y - 36f);
        g.FillRectangle(rail, centerX + 30f, line.DrumCenter.Y + 42f, 3f,
            line.DeckY - line.DrumCenter.Y - 36f);
        g.FillRectangle(railEdge, centerX - 32f, line.DrumCenter.Y + 45f, 1f,
            line.DeckY - line.DrumCenter.Y - 42f);
        g.FillRectangle(railEdge, centerX + 31f, line.DrumCenter.Y + 45f, 1f,
            line.DeckY - line.DrumCenter.Y - 42f);

        var ramTop = platformY + 5f;
        var ramBottom = line.DeckY + 12f;
        g.FillRectangle(railShadow, centerX - 8f, ramTop, 16f, MathF.Max(3f, ramBottom - ramTop));
        g.FillRectangle(piston, centerX - 5f, ramTop, 10f, MathF.Max(3f, ramBottom - ramTop));
        g.FillRectangle(cyan, centerX - 3f, ramTop + 2f, 2f, MathF.Max(1f, ramBottom - ramTop - 4f));

        g.FillRectangle(railShadow, centerX - 47f, platformY - 3f, 94f, 13f);
        g.FillRectangle(rail, centerX - 44f, platformY - 1f, 88f, 8f);
        g.FillRectangle(railEdge, centerX - 40f, platformY - 1f, 80f, 2f);
        for (var x = -34f; x <= 30f; x += 16f)
            g.FillRectangle(amber, centerX + x, platformY + 4f, 8f, 2f);
    }

    private static void DrawReceivingTub(Graphics g, ProcessingLine line)
    {
        if (line.Belts.Count == 0) return;
        var sprite = ReceivingTubSprite.Value;
        var bounds = line.ReceivingTubBounds;
        if (sprite is not null)
        {
            g.DrawImage(sprite, bounds,
                new RectangleF(0f, 0f, sprite.Width, sprite.Height), GraphicsUnit.Pixel);
            return;
        }

        using var shell = new Pen(Color.FromArgb(255, 83, 106, 116), 8f)
        {
            StartCap = LineCap.Square,
            EndCap = LineCap.Square,
            LineJoin = LineJoin.Miter
        };
        g.DrawLines(shell,
        [
            new PointF(bounds.Left + 4f, bounds.Top + 5f),
            new PointF(bounds.Left + 28f, bounds.Top + 30f),
            new PointF(bounds.Right - 42f, bounds.Top + 30f),
            new PointF(bounds.Right - 4f, bounds.Top + 4f)
        ]);
    }

    private static void DrawActuatorColumn(
        Graphics g, float centerX, float anchorY, float movingEndY, float width)
    {
        var bottom = MathF.Max(anchorY + 6f, movingEndY);
        using var shadow = new SolidBrush(Color.FromArgb(255, 9, 14, 18));
        using var sleeve = new SolidBrush(Color.FromArgb(255, 53, 69, 78));
        using var shaft = new SolidBrush(Color.FromArgb(255, 116, 139, 148));
        using var glint = new SolidBrush(Color.FromArgb(255, 178, 197, 199));
        g.FillRectangle(shadow, centerX - width * 0.5f - 3f, anchorY, width + 6f, bottom - anchorY);
        g.FillRectangle(sleeve, centerX - width * 0.5f, anchorY, width, bottom - anchorY);
        g.FillRectangle(shaft, centerX - width * 0.25f, anchorY + 3f, width * 0.5f, bottom - anchorY - 3f);
        g.FillRectangle(glint, centerX - 2f, anchorY + 3f, 3f, MathF.Max(2f, bottom - anchorY - 6f));
    }

    private static void DrawBreakerBox(Graphics g, ProcessingLine? line)
    {
        if (line is null) return;
        var bounds = line.BreakerBounds;
        var sprite = BreakerBoxSprite.Value;
        if (sprite is not null)
            g.DrawImage(sprite, bounds,
                new RectangleF(0f, 0f, sprite.Width, sprite.Height), GraphicsUnit.Pixel);

        var trackTop = line.BreakerTrackTop;
        var trackBottom = line.BreakerTrackBottom;
        var end = line.BreakerLeverHandle;
        using var shadow = new Pen(Color.FromArgb(255, 6, 10, 13), 13f)
        {
            StartCap = LineCap.Square,
            EndCap = LineCap.Square
        };
        using var steel = new Pen(Color.FromArgb(255, 191, 205, 204), 7f)
        {
            StartCap = LineCap.Square,
            EndCap = LineCap.Square
        };
        using var handle = new SolidBrush(line.Powered
            ? Color.FromArgb(255, 101, 230, 223)
            : Color.FromArgb(255, 240, 195, 75));
        g.DrawLine(shadow, trackTop.X, trackTop.Y, trackBottom.X, trackBottom.Y);
        g.DrawLine(steel, trackTop.X, trackTop.Y, trackBottom.X, trackBottom.Y);
        g.FillRectangle(handle, end.X - 9f, end.Y - 8f, 18f, 16f);
    }

    private static void DrawBreakerLamp(Graphics g, ProcessingLine? line)
    {
        if (line is null) return;
        var bounds = line.BreakerBounds;
        var center = new Vector2(bounds.Left + bounds.Width * 0.5f, bounds.Top + 7f);
        var color = line.Powered
            ? Color.FromArgb(101, 230, 223)
            : Color.FromArgb(240, 195, 75);
        using var far = new SolidBrush(Color.FromArgb(line.Powered ? 12 : 24, color));
        using var near = new SolidBrush(Color.FromArgb(line.Powered ? 20 : 48, color));
        using var lamp = new SolidBrush(Color.FromArgb(255, color));
        FillPixelOctagon(g, far, center, line.Powered ? 34f : 70f, line.Powered ? 28f : 58f, 10f);
        FillPixelOctagon(g, near, center, line.Powered ? 22f : 44f, line.Powered ? 18f : 36f, 7f);
        g.FillRectangle(lamp, center.X - 8f, center.Y - 5f, 16f, 10f);
        if (!line.Powered)
        {
            using var font = new Font("Consolas", 8f, FontStyle.Bold, GraphicsUnit.Point);
            using var text = new SolidBrush(Color.FromArgb(238, 240, 195, 75));
            g.DrawString("GRAB HANDLE • PULL DOWN", font, text, bounds.Left - 10f, bounds.Bottom + 4f);
        }
        DrawDoorwayBloodStains(g, line);
    }

    private static void DrawDoorwayBloodStains(Graphics g, ProcessingLine line)
    {
        if (line.DoorwayStains.Count == 0) return;
        using var dark = new SolidBrush(Color.FromArgb(205, 102, 3, 17));
        using var wet = new SolidBrush(Color.FromArgb(225, 211, 8, 22));
        foreach (var stain in line.DoorwayStains)
        {
            var brush = stain.Wetness > 0.3f ? wet : dark;
            var size = 3f + stain.Amount * 5f;
            if (stain.Vertical)
            {
                g.FillRectangle(brush, MathF.Round(stain.Position.X - 2f),
                    MathF.Round(stain.Position.Y - size * 0.5f), 4f, size);
                if ((stain.Variation & 3) == 0)
                    g.FillRectangle(brush, stain.Position.X - 1f, stain.Position.Y + size * 0.5f, 2f, 7f);
            }
            else
            {
                g.FillRectangle(brush, MathF.Round(stain.Position.X - size * 0.5f),
                    MathF.Round(stain.Position.Y - 2f), size, 4f);
            }
        }
    }

    private static void DrawFixtureEditHandles(
        Graphics g, HoldingChamber? chamber, ProcessingLine? line)
    {
        RectangleF? selected = null;
        if (chamber?.CounterSelected == true) selected = chamber.CounterBounds;
        else if (line?.BreakerSelected == true) selected = line.BreakerBounds;
        if (selected is not { } bounds) return;

        using var outline = new Pen(Color.FromArgb(245, 101, 230, 223), 2f);
        using var handle = new SolidBrush(Color.FromArgb(255, 101, 230, 223));
        using var labelBack = new SolidBrush(Color.FromArgb(225, 7, 12, 16));
        using var labelText = new SolidBrush(Color.FromArgb(245, 199, 244, 239));
        using var font = new Font("Consolas", 7.5f, FontStyle.Bold, GraphicsUnit.Point);
        g.DrawRectangle(outline, bounds.X - 3f, bounds.Y - 3f, bounds.Width + 6f, bounds.Height + 6f);
        g.FillRectangle(handle, bounds.Left - 6f, bounds.Top - 6f, 8f, 8f);
        g.FillRectangle(handle, bounds.Right - 2f, bounds.Top - 6f, 8f, 8f);
        g.FillRectangle(handle, bounds.Left - 6f, bounds.Bottom - 2f, 8f, 8f);
        g.FillRectangle(handle, bounds.Right - 2f, bounds.Bottom - 2f, 8f, 8f);
        var labelWidth = 190f;
        var labelX = Math.Clamp(bounds.Left, 2f, 1280f - labelWidth - 2f);
        var labelY = bounds.Top > 24f ? bounds.Top - 22f : bounds.Bottom + 8f;
        g.FillRectangle(labelBack, labelX, labelY, labelWidth, 17f);
        g.DrawString("DRAG TO MOVE • AUTO-SAVED", font, labelText, labelX + 4f, labelY + 2f);
    }

    private static void DrawMachineStatusLight(
        Graphics g,
        ProcessingLine line,
        ProcessingBay bay,
        int bayIndex)
    {
        if (!line.Powered)
        {
            using var offHousing = new SolidBrush(Color.FromArgb(255, 28, 38, 44));
            using var offLamp = new SolidBrush(Color.FromArgb(255, 39, 50, 54));
            g.FillRectangle(offHousing, bay.CenterX - 11f, line.DeckY - 190f, 22f, 22f);
            g.FillRectangle(offLamp, bay.CenterX - 5f, line.DeckY - 184f, 10f, 10f);
            return;
        }
        var sprite = MachineStatusSprite.Value;
        var inUse = line.IsBayInUse(bayIndex);
        var bounds = new RectangleF(bay.CenterX - 11f, line.DeckY - 190f, 22f, 22f);
        if (sprite is not null && sprite.Width >= 32 && sprite.Height >= 16)
        {
            var frame = inUse ? 1 : 0;
            g.DrawImage(sprite, bounds, new RectangleF(frame * 16f, 0f, 16f, 16f), GraphicsUnit.Pixel);
            return;
        }
        using var housing = new SolidBrush(Color.FromArgb(255, 42, 55, 62));
        using var lamp = new SolidBrush(inUse
            ? Color.FromArgb(255, 235, 62, 68)
            : Color.FromArgb(255, 78, 224, 157));
        g.FillRectangle(housing, bounds);
        g.FillEllipse(lamp, bounds.Left + 5f, bounds.Top + 5f, 12f, 12f);
    }

    private static void DrawMachineStatusGlow(
        Graphics g,
        ProcessingLine line,
        ProcessingBay bay,
        int bayIndex)
    {
        if (!line.Powered) return;
        var inUse = line.IsBayInUse(bayIndex);
        var color = inUse ? Color.FromArgb(235, 55, 65) : Color.FromArgb(65, 235, 157);
        var center = new Vector2(bay.CenterX, line.DeckY - 179f);
        using var far = new SolidBrush(Color.FromArgb(10, color));
        using var middle = new SolidBrush(Color.FromArgb(19, color));
        using var near = new SolidBrush(Color.FromArgb(31, color));
        using var wash = new SolidBrush(Color.FromArgb(7, color));
        // Stepped octagons preserve the pixel language while low-alpha nested
        // layers blend into the wall and machine instead of reading as a flat
        // colored circle pasted over the scene.
        FillPixelOctagon(g, far, center, 58f, 48f, 10f);
        FillPixelOctagon(g, middle, center, 42f, 36f, 7f);
        FillPixelOctagon(g, near, center, 30f, 28f, 5f);
        g.FillRectangle(wash, center.X - 19f, center.Y + 12f, 38f, 108f);
    }

    private static void DrawVacuumHolster(Graphics g, ProcessingLine line)
    {
        var sprite = VacuumHolsterSprite.Value;
        if (sprite is null) return;
        var center = line.VacuumHolsterCenter;
        var bounds = new RectangleF(center.X - 15f, center.Y - 25f, 30f, 50f);
        g.DrawImage(sprite, bounds,
            new RectangleF(0f, 0f, sprite.Width, sprite.Height), GraphicsUnit.Pixel);
    }

    private static void DrawVacuumHose(Graphics g, ProcessingLine line)
    {
        var nodes = line.VacuumHose.Nodes;
        if (nodes.Count < 2) return;
        using var hoseShadow = new Pen(VacuumHoseShadowColor, 10f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        using var hose = new Pen(VacuumHoseBodyColor, 6f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        using var hoseLight = new Pen(VacuumHoseHighlightColor, 1f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        var direction = line.VacuumHose.NozzleFacing;
        var points = new PointF[nodes.Count];
        for (var i = 0; i < nodes.Count; i++) points[i] = new PointF(nodes[i].X, nodes[i].Y);
        // The simulated endpoint is the nozzle grip/rotation pivot. Visually the
        // rubber hose terminates at the rear socket, not through its center.
        var nozzleBack = nodes[^1] - direction * 20f;
        points[^1] = new PointF(nozzleBack.X, nozzleBack.Y);
        g.DrawLines(hoseShadow, points);
        g.DrawLines(hose, points);
        g.DrawLines(hoseLight, points);

        if (line.VacuumContact)
        {
            // These are rubber bulges caused by material travelling inside the
            // hose, not exposed chunks. Use the hose's exact three-color stack.
            using var lumpShadow = new SolidBrush(VacuumHoseShadowColor);
            using var lumpBody = new SolidBrush(VacuumHoseBodyColor);
            using var lumpGlint = new SolidBrush(VacuumHoseHighlightColor);
            for (var i = 0; i < 3; i++)
            {
                var travel = (line.VacuumFlowPhase + i / 3f) % 1f;
                var position = PointAlongHose(nodes, travel, nozzleBack);
                var size = 9f + (i & 1) * 2f;
                FillPixelOctagon(g, lumpShadow, position, size + 4f, size + 4f, 3f);
                FillPixelOctagon(g, lumpBody, position, size, size, 2f);
                g.FillRectangle(lumpGlint, position.X - 1f, position.Y - size * 0.25f, 2f, 2f);
            }
        }

        var coupler = VacuumCouplerSprite.Value;
        if (coupler is not null)
            g.DrawImage(coupler,
                new RectangleF(line.VacuumHoseAnchor.X - 13f, line.VacuumHoseAnchor.Y - 13f, 26f, 26f),
                new RectangleF(0f, 0f, coupler.Width, coupler.Height), GraphicsUnit.Pixel);

        var nozzle = VacuumNozzleSprite.Value;
        if (nozzle is null) return;
        var end = nodes[^1];
        var angle = MathF.Atan2(direction.Y, direction.X) * 180f / MathF.PI;
        var state = g.Save();
        g.TranslateTransform(end.X, end.Y);
        g.RotateTransform(angle);
        g.DrawImage(nozzle, new RectangleF(-23f, -12f, 46f, 24f),
            new RectangleF(0f, 0f, nozzle.Width, nozzle.Height), GraphicsUnit.Pixel);
        g.Restore(state);

        if (line.VacuumContact)
        {
            using var suction = new Pen(Color.FromArgb(170, 101, 230, 223), 1f)
            {
                DashStyle = DashStyle.Dot
            };
            var tangent = new Vector2(-direction.Y, direction.X);
            for (var i = -1; i <= 1; i++)
            {
                var start = end + direction * 21f + tangent * (i * 4f);
                var finish = end + direction * 38f + tangent * (i * 7f);
                g.DrawLine(suction, start.X, start.Y, finish.X, finish.Y);
            }
        }
    }

    private static void DrawCartForeground(Graphics g, ProcessingLine line)
    {
        var cart = OutputCartSprite.Value;
        if (cart is null) return;
        var bounds = line.OutputCartBounds;
        g.DrawImage(cart, bounds,
            new RectangleF(0f, 0f, cart.Width, cart.Height), GraphicsUnit.Pixel);
    }

    private static void DrawCartStatus(Graphics g, ProcessingLine line)
    {
        if (line.CartState != CartCycleState.Docked || !line.IsCartLoaded) return;
        using var statusBack = new SolidBrush(Color.FromArgb(225, 12, 18, 23));
        using var statusBrush = new SolidBrush(Color.FromArgb(255, 214, 72));
        using var statusFont = new Font("Consolas", 6.5f, FontStyle.Bold, GraphicsUnit.Point);
        g.FillRectangle(statusBack, line.CartDockBounds.Left - 3f, line.CartDockBounds.Top - 17f, 110f, 13f);
        g.DrawString("CLICK TO DISPATCH", statusFont, statusBrush,
            line.CartDockBounds.Left - 3f, line.CartDockBounds.Top - 17f);
    }

    private static Vector2 PointAlongHose(
        IReadOnlyList<Vector2> nodes,
        float nozzleToAnchor,
        Vector2? nozzleEndpoint = null)
    {
        var index = (1f - Math.Clamp(nozzleToAnchor, 0f, 1f)) * (nodes.Count - 1);
        var lower = Math.Clamp((int)MathF.Floor(index), 0, nodes.Count - 1);
        var upper = Math.Min(nodes.Count - 1, lower + 1);
        var lowerPoint = nozzleEndpoint.HasValue && lower == nodes.Count - 1
            ? nozzleEndpoint.Value
            : nodes[lower];
        var upperPoint = nozzleEndpoint.HasValue && upper == nodes.Count - 1
            ? nozzleEndpoint.Value
            : nodes[upper];
        return Vector2.Lerp(lowerPoint, upperPoint, index - lower);
    }

    private static void FillPixelOctagon(
        Graphics g,
        Brush brush,
        Vector2 center,
        float width,
        float height,
        float corner)
    {
        var x = MathF.Round(center.X - width * 0.5f);
        var y = MathF.Round(center.Y - height * 0.5f);
        var maximumCorner = MathF.Max(0.25f, MathF.Min(width, height) * 0.35f);
        corner = Math.Clamp(corner, MathF.Min(1f, maximumCorner), maximumCorner);
        g.FillRectangle(brush, x + corner, y, width - corner * 2f, height);
        g.FillRectangle(brush, x, y + corner, width, height - corner * 2f);
    }

    private void DrawHoldingChamber(Graphics g, HoldingChamber? chamber)
    {
        if (chamber is null) return;
        var previousSmoothing = g.SmoothingMode;
        var previousInterpolation = g.InterpolationMode;
        var previousPixelOffset = g.PixelOffsetMode;
        g.SmoothingMode = SmoothingMode.None;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        // Continue the chamber's feed neck beyond the logical screen edge so
        // incoming units visibly arrive from the factory above the room.
        var spriteBounds = chamber.SpriteBounds;
        var feedTube = chamber.FeedTubeBounds;
        g.FillRectangle(_chamberDoorEdgeBrush, feedTube);
        g.FillRectangle(_chamberDoorBrush,
            feedTube.Left + 5f, feedTube.Top, feedTube.Width - 10f, feedTube.Height);
        g.FillRectangle(_chamberGlassFallbackBrush,
            feedTube.Left + 9f, feedTube.Top, 4f, feedTube.Height);

        var doorY = chamber.Center.Y + chamber.InnerRadius - 8f;
        var halfWidth = chamber.HatchHalfWidth;
        var doorHeight = 14f;
        DrawChamberDoorFlap(g, chamber.Center.X - halfWidth, doorY,
            halfWidth, doorHeight, chamber.HatchOpen * 90f, leftFlap: true);
        DrawChamberDoorFlap(g, chamber.Center.X + halfWidth, doorY,
            halfWidth, doorHeight, chamber.HatchOpen * -90f, leftFlap: false);

        var sprite = HoldingChamberSprite.Value;
        if (sprite is not null)
        {
            g.DrawImage(sprite, chamber.SpriteBounds,
                new RectangleF(0f, 0f, sprite.Width, sprite.Height), GraphicsUnit.Pixel);
        }
        else
        {
            g.FillEllipse(_chamberGlassFallbackBrush,
                chamber.Center.X - chamber.InnerRadius,
                chamber.Center.Y - chamber.InnerRadius,
                chamber.InnerRadius * 2f,
                chamber.InnerRadius * 2f);
        }

        var pivot = chamber.LeverPivot;
        var handle = chamber.LeverHandle;
        g.DrawLine(_chamberLeverShadowPen, pivot.X, pivot.Y, handle.X, handle.Y);
        g.DrawLine(_chamberLeverPen, pivot.X, pivot.Y, handle.X, handle.Y);
        g.FillEllipse(_chamberWarningBrush, handle.X - 10f, handle.Y - 10f, 20f, 20f);
        g.FillRectangle(_chamberDoorEdgeBrush, handle.X - 5f, handle.Y - 5f, 10f, 10f);

        var counterBounds = chamber.CounterBounds;
        g.FillRectangle(_chamberDoorBrush, counterBounds);
        g.DrawRectangle(_chamberLeverPen, counterBounds.X, counterBounds.Y, counterBounds.Width, counterBounds.Height);
        using var counterLabelFont = new Font("Consolas", 6.5f, FontStyle.Bold, GraphicsUnit.Point);
        using var counterValueFont = new Font("Consolas", 11f, FontStyle.Bold, GraphicsUnit.Point);
        using var counterLabel = new SolidBrush(Color.FromArgb(235, 135, 158, 166));
        using var counterValue = new SolidBrush(Color.FromArgb(255, 124, 233, 223));
        g.DrawString("UNITS", counterLabelFont, counterLabel, counterBounds.Left + 6f, counterBounds.Top + 3f);
        g.DrawString($"{chamber.UnitsProduced % 10000:0000}", counterValueFont, counterValue,
            counterBounds.Left + 5f, counterBounds.Top + 14f);

        g.SmoothingMode = previousSmoothing;
        g.InterpolationMode = previousInterpolation;
        g.PixelOffsetMode = previousPixelOffset;
    }

    private void DrawChamberDoorFlap(
        Graphics g,
        float pivotX,
        float pivotY,
        float width,
        float height,
        float angle,
        bool leftFlap)
    {
        var state = g.Save();
        g.TranslateTransform(pivotX, pivotY);
        g.RotateTransform(angle);
        var left = leftFlap ? 0f : -width;
        g.FillRectangle(_chamberDoorBrush, left, 0f, width, height);
        g.FillRectangle(_chamberDoorEdgeBrush, left, 0f, width, 3f);
        var warningX = leftFlap ? width - 15f : left + 6f;
        g.FillRectangle(_chamberWarningBrush, warningX, 4f, 9f, 6f);
        g.Restore(state);
    }

    private void DrawBloodSurfaceStains(
        Graphics g,
        DestructibleGrid grid,
        IReadOnlyList<ConveyorBelt> conveyors)
    {
        var previousSmoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.None;
        using (var previousClip = g.Clip)
        {
            g.SetClip(GetBloodSurfaceClip(grid), CombineMode.Intersect);
            DrawMarks(grid.BloodStains, Vector2.Zero);
            g.SetClip(previousClip, CombineMode.Replace);
        }
        foreach (var conveyor in conveyors)
        {
            DrawMarks(conveyor.BloodStains, conveyor.Position);
            DrawTransientDrops(conveyor);
        }

        void DrawTransientDrops(ConveyorBelt conveyor)
        {
            foreach (var drop in conveyor.TransientDrops)
            {
                var x = MathF.Round((conveyor.Position.X + drop.Position.X) * 0.5f) * 2f;
                var y = MathF.Round((conveyor.Position.Y + drop.Position.Y) * 0.5f) * 2f;
                var size = (drop.Variation & 1) == 0 ? 2f : 4f;
                g.FillRectangle(_wetStainBrushes[1], x, y, size, size);
                if ((drop.Variation & 4) != 0)
                    g.FillRectangle(_wetStainBrushes[0], x, y - 4f, 2f, 2f);
            }
        }
        g.SmoothingMode = previousSmoothing;

        void DrawMarks(IReadOnlyList<BloodSurfaceMark> marks, Vector2 origin)
        {
            foreach (var mark in marks)
            {
                var intensity = Math.Clamp(mark.Amount / 0.55f, 0.16f, 1f);
                var paletteIndex = intensity < 0.38f ? 0 : intensity < 0.72f ? 1 : 2;
                var brush = mark.Wetness > 0.12f ? _wetStainBrushes[paletteIndex] : _dryStainBrushes[paletteIndex];
                var tangentHorizontal = MathF.Abs(mark.SurfaceNormal.Y) > 0.55f;
                var widthVariation = 0.72f + (mark.Variation & 7) / 7f * 0.58f;
                var heightVariation = 0.70f + ((mark.Variation >> 3) & 7) / 7f * 0.66f;
                var width = mark.Radius * (mark.IsDrip ? 1.05f : tangentHorizontal ? 2.25f : 1.35f) * widthVariation;
                var height = mark.Radius * (mark.IsDrip ? 2.8f : tangentHorizontal ? 0.95f : 1.85f) * heightVariation;
                var jitter = (((mark.Variation >> 6) & 3) - 1.5f) * 0.65f;
                if (mark.IsDrip)
                {
                    width = Math.Clamp(width, 2f, 12f);
                    height = mark.VisibleTrailLength;
                }
                else
                {
                    width = Math.Clamp(width, 2f, 9f);
                    // Horizontal surfaces spread along X; vertical wall paint
                    // spreads along Y. The old universal 5 px height cap
                    // flattened wall splashes and hid repeated accumulation.
                    height = Math.Clamp(height, 2f, tangentHorizontal ? 5f : 11f);
                }

                float x;
                float y;
                if (mark.IsDrip)
                {
                    x = MathF.Abs(mark.SurfaceNormal.X) > MathF.Abs(mark.SurfaceNormal.Y)
                        ? mark.SurfaceNormal.X < 0f
                            ? origin.X + mark.Position.X
                            : origin.X + mark.Position.X - width
                        : origin.X + mark.Position.X - width * 0.5f + jitter;
                    y = origin.Y + mark.Position.Y;
                }
                else if (MathF.Abs(mark.SurfaceNormal.Y) >= MathF.Abs(mark.SurfaceNormal.X))
                {
                    x = origin.X + mark.Position.X - width * 0.5f + mark.SurfaceNormal.Y * jitter;
                    y = mark.SurfaceNormal.Y < 0f
                        ? origin.Y + mark.Position.Y
                        : origin.Y + mark.Position.Y - height;
                }
                else
                {
                    x = mark.SurfaceNormal.X < 0f
                        ? origin.X + mark.Position.X
                        : origin.X + mark.Position.X - width;
                    y = origin.Y + mark.Position.Y - height * 0.5f - mark.SurfaceNormal.X * jitter;
                }
                x = MathF.Round(x * 0.5f) * 2f;
                y = MathF.Round(y * 0.5f) * 2f;
                width = MathF.Max(2f, MathF.Round(width * 0.5f) * 2f);
                height = MathF.Max(2f, MathF.Round(height * 0.5f) * 2f);
                if (mark.IsDrip)
                {
                    DrawSegmentedDrip(brush, x, y, width, height, mark.Variation);
                    continue;
                }
                g.FillRectangle(brush, x, y, width, height);
                if (mark.Wetness <= 0.20f || mark.Amount < 0.16f) continue;
                g.FillRectangle(_wetStainShine,
                    x + 1f,
                    y,
                    2f,
                    2f);
            }
        }

        void DrawSegmentedDrip(
            Brush brush,
            float x,
            float y,
            float width,
            float height,
            byte variation)
        {
            var state = (uint)(variation + 1) * 0x9E3779B9u;
            var centerX = x + width * 0.5f;
            var anchorCenterX = centerX;
            var maximumWander = MathF.Max(2f, width * 0.85f + 2f);
            var previousCenterX = centerX;
            var previousWidth = width;
            var cursorY = y;
            var remaining = height;
            var segmentIndex = 0;
            while (remaining > 1f && segmentIndex < 24)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                var segmentHeight = MathF.Min(remaining, 2f + ((state >> 4) & 3u) * 2f);
                var taper = 1f - segmentIndex * 0.075f;
                var widthNoise = 0.72f + (state & 7u) / 7f * 0.48f;
                var segmentWidth = Math.Clamp(MathF.Round(width * taper * widthNoise * 0.5f) * 2f, 2f, width + 2f);
                var wander = ((int)((state >> 8) & 3u) - 1) * 1.2f;
                centerX = Math.Clamp(centerX + wander,
                    anchorCenterX - maximumWander,
                    anchorCenterX + maximumWander);
                if (segmentIndex > 0)
                {
                    // Join consecutive wandering sections across the tile's
                    // front face. Without this bridge a narrow two-pixel lane
                    // could step sideways and make the panel art appear to
                    // occlude the blood even though stains render afterward.
                    var bridgeLeft = MathF.Min(
                        previousCenterX - previousWidth * 0.28f,
                        centerX - segmentWidth * 0.28f);
                    var bridgeRight = MathF.Max(
                        previousCenterX + previousWidth * 0.28f,
                        centerX + segmentWidth * 0.28f);
                    g.FillRectangle(brush,
                        MathF.Round(bridgeLeft * 0.5f) * 2f,
                        cursorY - 1f,
                        MathF.Max(2f, MathF.Round((bridgeRight - bridgeLeft) * 0.5f) * 2f),
                        2f);
                }
                g.FillRectangle(brush,
                    MathF.Round((centerX - segmentWidth * 0.5f) * 0.5f) * 2f,
                    cursorY,
                    segmentWidth,
                    segmentHeight);
                previousCenterX = centerX;
                previousWidth = segmentWidth;
                cursorY += segmentHeight;
                remaining -= segmentHeight;
                segmentIndex++;
                if (remaining > 5f && ((state >> 12) & 7u) == 0u)
                {
                    // Keep the former depth beat, but render it as a narrow
                    // front-face neck rather than a transparent hole through
                    // which the tile center shows completely.
                    var neckWidth = MathF.Max(2f, MathF.Round(segmentWidth * 0.55f * 0.5f) * 2f);
                    g.FillRectangle(brush,
                        MathF.Round((centerX - neckWidth * 0.5f) * 0.5f) * 2f,
                        cursorY,
                        neckWidth,
                        2f);
                    cursorY += 2f;
                    remaining -= 2f;
                }
            }

            var beadSize = Math.Clamp(width * (0.65f + (variation & 3) * 0.12f), 2f, 8f);
            g.FillRectangle(brush,
                MathF.Round((centerX - beadSize * 0.5f) * 0.5f) * 2f,
                MathF.Max(y, cursorY - beadSize * 0.35f),
                MathF.Round(beadSize * 0.5f) * 2f,
                MathF.Round(beadSize * 0.5f) * 2f);
        }
    }

    private Region GetBloodSurfaceClip(DestructibleGrid grid)
    {
        if (ReferenceEquals(_bloodSurfaceClipGrid, grid) &&
            _bloodSurfaceClipRevision == grid.SurfaceRevision && _bloodSurfaceClip is not null)
            return _bloodSurfaceClip;

        _bloodSurfaceClip?.Dispose();
        _bloodSurfaceClip = new Region();
        _bloodSurfaceClip.MakeEmpty();
        for (var y = 0; y < grid.Rows; y++)
        for (var x = 0; x < grid.Columns; x++)
        {
            if (!grid.Cell(x, y).IsSolid) continue;
            _bloodSurfaceClip.Union(new RectangleF(
                x * grid.CellSize,
                y * grid.CellSize,
                grid.CellSize,
                grid.CellSize));
        }
        _bloodSurfaceClipGrid = grid;
        _bloodSurfaceClipRevision = grid.SurfaceRevision;
        return _bloodSurfaceClip;
    }

    private void DrawConveyors(Graphics g, IReadOnlyList<ConveyorBelt> conveyors)
    {
        foreach (var conveyor in conveyors)
        {
            var rect = new RectangleF(conveyor.Position.X, conveyor.Position.Y, conveyor.Width, conveyor.Height);
            var loopRadius = rect.Height * 0.5f;
            var leftLoopCenter = rect.Left + loopRadius;
            var rightLoopCenter = rect.Right - loopRadius;
            g.FillRectangle(_conveyorBeltBrush, leftLoopCenter, rect.Top, MathF.Max(1f, rightLoopCenter - leftLoopCenter), rect.Height);
            g.FillEllipse(_conveyorBeltBrush, rect.Left, rect.Top, rect.Height, rect.Height);
            g.FillEllipse(_conveyorBeltBrush, rect.Right - rect.Height, rect.Top, rect.Height, rect.Height);
            var loopPen = conveyor.IsSelected ? _selectedConveyorEdgePen : _conveyorEdgePen;
            g.DrawLine(loopPen, leftLoopCenter, rect.Top, rightLoopCenter, rect.Top);
            g.DrawLine(loopPen, leftLoopCenter, rect.Bottom, rightLoopCenter, rect.Bottom);
            g.DrawArc(loopPen, rect.Left, rect.Top, rect.Height, rect.Height, 90f, 180f);
            g.DrawArc(loopPen, rect.Right - rect.Height, rect.Top, rect.Height, rect.Height, -90f, 180f);

            var rollerSize = Math.Clamp(rect.Height - 8f, 12f, 64f);
            var rollerRadius = rollerSize * 0.5f;
            var rollerY = rect.Top + rect.Height * 0.5f;
            var rollerSpacing = MathF.Max(rollerSize * 1.45f, 48f);
            var rollerAngle = conveyor.AnimationOffset / MathF.Max(1f, rollerRadius);
            var leftRollerX = leftLoopCenter;
            var rightRollerX = rightLoopCenter;
            DrawRoller(leftRollerX);
            for (var x = leftRollerX + rollerSpacing; x < rightRollerX - rollerSpacing * 0.45f; x += rollerSpacing)
                DrawRoller(x);
            if (rightRollerX - leftRollerX > rollerSize * 1.2f) DrawRoller(rightRollerX);

            void DrawRoller(float x)
            {
                g.FillEllipse(_conveyorRollerBrush, x - rollerRadius, rollerY - rollerRadius, rollerSize, rollerSize);
                g.DrawEllipse(_conveyorSpokePen, x - rollerRadius, rollerY - rollerRadius, rollerSize, rollerSize);
                for (var spoke = 0; spoke < 3; spoke++)
                {
                    var angle = rollerAngle + spoke * MathF.Tau / 3f;
                    g.DrawLine(_conveyorSpokePen, x, rollerY,
                        x + MathF.Cos(angle) * rollerRadius * 0.72f,
                        rollerY + MathF.Sin(angle) * rollerRadius * 0.72f);
                }
                g.FillEllipse(_conveyorHubBrush, x - 2.5f, rollerY - 2.5f, 5f, 5f);
            }

            var direction = conveyor.Speed >= 0f ? 1f : -1f;
            var phase = ((conveyor.AnimationOffset % 18f) + 18f) % 18f;
            for (var x = rect.Left + phase; x < rect.Right; x += 18f)
            {
                g.DrawLine(_conveyorTreadPen, x, rect.Top + 2f, x + 5f, rect.Top + 7f);
            }
            var returnPhase = (18f - phase) % 18f;
            for (var x = rect.Left + returnPhase; x < rect.Right; x += 18f)
            {
                g.DrawLine(_conveyorTreadPen, x, rect.Bottom - 2f, x + 5f, rect.Bottom - 7f);
            }
            g.DrawLine(_conveyorTrackPen, leftLoopCenter, rect.Top + 1.5f, rightLoopCenter, rect.Top + 1.5f);
            g.DrawLine(_conveyorTrackPen, leftLoopCenter, rect.Bottom - 1.5f, rightLoopCenter, rect.Bottom - 1.5f);
            var arcPhase = phase / MathF.Max(1f, loopRadius);
            for (var tick = 0; tick < 5; tick++)
            {
                var rightAngle = -MathF.PI * 0.5f + tick * MathF.PI / 4f + arcPhase;
                var leftAngle = MathF.PI * 0.5f + tick * MathF.PI / 4f + arcPhase;
                DrawArcTread(rightLoopCenter, rollerY, rightAngle);
                DrawArcTread(leftLoopCenter, rollerY, leftAngle);
            }

            void DrawArcTread(float centerX, float centerY, float angle)
            {
                var inner = loopRadius - 4f;
                g.DrawLine(_conveyorTreadPen,
                    centerX + MathF.Cos(angle) * inner,
                    centerY + MathF.Sin(angle) * inner,
                    centerX + MathF.Cos(angle) * loopRadius,
                    centerY + MathF.Sin(angle) * loopRadius);
            }

            for (var x = rect.Left + 16f; x < rect.Right - 8f; x += 34f)
            {
                var armX = x - direction * 7f;
                g.DrawLine(_conveyorMotionPen, armX, rect.Top + 7f, x, rect.Top + 11f);
                g.DrawLine(_conveyorMotionPen, armX, rect.Top + 15f, x, rect.Top + 11f);
            }

            if (!conveyor.IsSelected) continue;
            g.DrawString($"BELT {conveyor.Speed:+0;-0;0} px/s", SystemFonts.CaptionFont!, _conveyorLabelBrush,
                rect.Left, rect.Top - 20f);
            g.FillRectangle(_conveyorHandleBrush, rect.Right - 5f, rect.Top + rect.Height * 0.5f - 5f, 10f, 10f);
            g.FillRectangle(_conveyorHandleBrush, rect.Left + rect.Width * 0.5f - 5f, rect.Bottom - 5f, 10f, 10f);
        }
    }

    private void DrawBlob(
        Graphics g,
        SoftBody body,
        bool grabbed,
        float faceRotation = 0f,
        bool machineLit = false)
    {
        var debris = body.IsDetachedDebris;
        var materialContour = BlobContourBuilder.BuildShell(body);
        var hull = materialContour.Points;
        if (hull.Length >= 3 &&
            (hull.Any(point => !float.IsFinite(point.X) || !float.IsFinite(point.Y)) ||
             MathF.Abs(PolygonArea(hull)) < 1f))
            hull = Array.Empty<Vector2>();
        if (hull.Length < 3)
        {
            var fragmentBrush = machineLit
                ? _blobMachineLitDarkBrush
                : body.IsDetachedDebris ? _blobDebrisDarkBrush : _blobDarkBrush;
            var fallbackSmoothing = g.SmoothingMode;
            if (body.IsDetachedDebris) g.SmoothingMode = SmoothingMode.None;
            for (var particleIndex = 0; particleIndex < body.Particles.Length; particleIndex++)
            {
                if (!body.IsPhysicalParticle(particleIndex)) continue;
                var particle = body.Particles[particleIndex];
                if (body.IsDetachedDebris)
                {
                    var diameter = MathF.Max(4f, MathF.Round(particle.Radius * 1.4f));
                    var x = MathF.Round(particle.Position.X - diameter * 0.5f);
                    var y = MathF.Round(particle.Position.Y - diameter * 0.5f);
                    g.FillRectangle(_tissuePixelRimBrush, x, y, diameter, diameter);
                    if (diameter >= 6f)
                        g.FillRectangle(fragmentBrush, x + 1f, y + 1f, diameter - 2f, diameter - 2f);
                    continue;
                }
                var points = new PointF[7];
                for (var point = 0; point < points.Length; point++)
                {
                    var angle = MathF.Tau * point / points.Length + (body.ParentId + particleIndex * 3) * 0.17f;
                    var radius = particle.Radius * (0.76f + ((point + body.ParentId) % 3) * 0.11f);
                    points[point] = new PointF(
                        particle.Position.X + MathF.Cos(angle) * radius,
                        particle.Position.Y + MathF.Sin(angle) * radius);
                }
                g.FillPolygon(fragmentBrush, points);
                if (machineLit && particleIndex % 4 == 0)
                    g.FillRectangle(_blobMachineLitPixelBrush,
                        MathF.Round(particle.Position.X - 1f), MathF.Round(particle.Position.Y - 1f), 2f, 2f);
            }
            g.SmoothingMode = fallbackSmoothing;
            if (machineLit && !debris && body.Particles.Length >= 7)
                DrawFace(g, body, faceRotation);
            if (DebugDraw) DrawBodyDebug(g, body);
            return;
        }

        var center = body.Center;
        var shell = hull.Select(point => new PointF(point.X, point.Y)).ToArray();
        // The validated material contour is already an authoritative perimeter. Runtime
        // rendering must not hit-test every physical particle against a fresh GDI path on
        // every frame: machinery creates local damage continuously, turning that visual
        // safety check into the dominant frame cost. The expensive validation remains in
        // BuildMaterialPath for diagnostics; live damaged shapes use the pixel-friendly
        // polygon fallback, which cannot curve inward across supported tissue.
        using var path = body.HasLocalDamage || debris
            ? BuildRuntimeDamagedPath(shell)
            : BuildHealthyPath(shell);

        var previousSmoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.None;
        g.FillPath(machineLit
            ? _blobMachineLitDarkBrush
            : grabbed ? _blobGrabbedDarkBrush : debris ? _blobDebrisDarkBrush : _blobDarkBrush, path);
        DrawPixelBlobOutline(g, hull, grabbed, machineLit);
        g.SmoothingMode = previousSmoothing;

        if (!debris && body.Particles.Length >= 7) DrawFace(g, body, faceRotation);
        if (DebugDraw) DrawBodyDebug(g, body);
    }

    private void DrawPixelBlobOutline(
        Graphics g,
        IReadOnlyList<Vector2> hull,
        bool grabbed,
        bool machineLit = false)
    {
        const int pixelSize = 4;
        var required = 0;
        for (var edgeIndex = 0; edgeIndex < hull.Count; edgeIndex++)
            required += Math.Max(1, (int)MathF.Ceiling(
                Vector2.Distance(hull[edgeIndex], hull[(edgeIndex + 1) % hull.Count]) / 3.2f)) + 1;
        if (required > _blobPixelOutlineRectangles.Length)
            Array.Resize(ref _blobPixelOutlineRectangles,
                Math.Max(required, _blobPixelOutlineRectangles.Length * 2));

        var previousCount = _blobPixelOutlineRectangleCount;
        _blobPixelOutlineRectangleCount = 0;
        for (var edgeIndex = 0; edgeIndex < hull.Count; edgeIndex++)
        {
            var from = hull[edgeIndex];
            var to = hull[(edgeIndex + 1) % hull.Count];
            var length = Vector2.Distance(from, to);
            var samples = Math.Max(1, (int)MathF.Ceiling(length / 3.2f));
            for (var sample = 0; sample <= samples; sample++)
            {
                var point = Vector2.Lerp(from, to, sample / (float)samples);
                var x = (int)MathF.Round((point.X - pixelSize * 0.5f) * 0.5f) * 2;
                var y = (int)MathF.Round((point.Y - pixelSize * 0.5f) * 0.5f) * 2;
                _blobPixelOutlineRectangles[_blobPixelOutlineRectangleCount++] =
                    new Rectangle(x, y, pixelSize, pixelSize);
            }
        }
        if (_blobPixelOutlineRectangleCount < previousCount)
            Array.Clear(_blobPixelOutlineRectangles, _blobPixelOutlineRectangleCount,
                previousCount - _blobPixelOutlineRectangleCount);
        g.FillRectangles(machineLit
                ? _blobMachineLitPixelBrush
                : grabbed ? _blobGrabbedPixelBrush : _blobPixelRedBrush,
            _blobPixelOutlineRectangles);
    }

    private static GraphicsPath BuildHealthyPath(PointF[] shell)
    {
        var path = new GraphicsPath();
        path.AddClosedCurve(shell, 0.58f);
        return path;
    }

    private static GraphicsPath BuildRuntimeDamagedPath(PointF[] shell)
    {
        var path = new GraphicsPath(FillMode.Winding);
        path.AddPolygon(shell);
        return path;
    }

    internal static GraphicsPath BuildMaterialPath(
        PointF[] shell,
        bool[] wound,
        SoftBody? requiredBody = null,
        IReadOnlyList<int>? requiredParticles = null)
    {
        // Winding fill prevents a tightly folded but valid damaged boundary from
        // punching an accidental alternate-fill hole through supported tissue.
        var path = new GraphicsPath(FillMode.Winding);
        path.StartFigure();
        var signedArea = 0f;
        for (var i = 0; i < shell.Length; i++)
        {
            var next = (i + 1) % shell.Length;
            signedArea += shell[i].X * shell[next].Y - shell[next].X * shell[i].Y;
        }
        var interiorSign = MathF.Sign(signedArea);
        for (var i = 0; i < shell.Length; i++)
        {
            var previous = (i + shell.Length - 1) % shell.Length;
            var next = (i + 1) % shell.Length;
            var afterNext = (i + 2) % shell.Length;
            var from = shell[i];
            var to = shell[next];
            if (wound.Length == shell.Length && wound[i] && wound[next])
            {
                path.AddLine(from, to);
                continue;
            }

            var controlA = new PointF(
                from.X + (shell[next].X - shell[previous].X) * 0.12f,
                from.Y + (shell[next].Y - shell[previous].Y) * 0.12f);
            var controlB = new PointF(
                to.X - (shell[afterNext].X - shell[i].X) * 0.12f,
                to.Y - (shell[afterNext].Y - shell[i].Y) * 0.12f);
            if (wound.Length == shell.Length && wound[i])
                controlA = new PointF(from.X + (to.X - from.X) * 0.22f, from.Y + (to.Y - from.Y) * 0.22f);
            if (wound.Length == shell.Length && wound[next])
                controlB = new PointF(to.X - (to.X - from.X) * 0.22f, to.Y - (to.Y - from.Y) * 0.22f);
            controlA = KeepControlOutsideMaterial(from, to, controlA, interiorSign);
            controlB = KeepControlOutsideMaterial(from, to, controlB, interiorSign);
            path.AddBezier(from, controlA, controlB, to);
        }
        path.CloseFigure();
        if (requiredBody is not null && !ContainsPhysicalCenters(path, requiredBody, requiredParticles))
        {
            // A cubic can still cut across a concavity even when each individual
            // control stays outside its supporting edge. Use the authoritative,
            // already validated shell only for that exceptional body/frame.
            path.Reset();
            path.AddPolygon(shell);
        }
        return path;
    }

    private static bool ContainsPhysicalCenters(
        GraphicsPath path,
        SoftBody body,
        IReadOnlyList<int>? requiredParticles)
    {
        Pen? boundaryTolerance = null;
        try
        {
            var count = requiredParticles?.Count ?? body.Particles.Length;
            for (var candidateIndex = 0; candidateIndex < count; candidateIndex++)
            {
                var particleIndex = requiredParticles is null ? candidateIndex : requiredParticles[candidateIndex];
                if (!body.IsPhysicalParticle(particleIndex)) continue;
                var position = body.Particles[particleIndex].Position;
                if (path.IsVisible(position.X, position.Y)) continue;
                // Match the actual visible outline width used by DrawBlob.
                boundaryTolerance ??= new Pen(Color.Black, 3f);
                if (!path.IsOutlineVisible(position.X, position.Y, boundaryTolerance)) return false;
            }
            return true;
        }
        finally
        {
            boundaryTolerance?.Dispose();
        }
    }

    private static PointF KeepControlOutsideMaterial(PointF from, PointF to, PointF control, float interiorSign)
    {
        var edgeX = to.X - from.X;
        var edgeY = to.Y - from.Y;
        var lengthSquared = edgeX * edgeX + edgeY * edgeY;
        if (lengthSquared < 0.0001f) return control;
        var controlSide = edgeX * (control.Y - from.Y) - edgeY * (control.X - from.X);
        if (interiorSign * controlSide <= 0f) return control;

        // Project an inward control back onto its supporting edge. A cubic whose
        // anchors and controls are on/outside that half-plane cannot bow through
        // the material/contact polygon between the anchors.
        var t = ((control.X - from.X) * edgeX + (control.Y - from.Y) * edgeY) / lengthSquared;
        return new PointF(from.X + edgeX * t, from.Y + edgeY * t);
    }

    private static float PolygonArea(ReadOnlySpan<Vector2> points)
    {
        var area = 0f;
        for (var i = 0; i < points.Length; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Length];
            area += a.X * b.Y - b.X * a.Y;
        }
        return area * 0.5f;
    }

    private void DrawFace(Graphics g, SoftBody body, float rotation)
    {
        var c = body.Center;
        var eyeOffset = body.Radius * 0.22f;
        var eyeLift = -body.Radius * 0.08f;
        var eyeSize = Math.Clamp(MathF.Round(body.Radius * 0.18f * 0.5f) * 2f, 8f, 16f);
        var cos = MathF.Cos(rotation);
        var sin = MathF.Sin(rotation);
        void DrawEye(float localX)
        {
            var eyeX = c.X + localX * cos - eyeLift * sin;
            var eyeY = c.Y + localX * sin + eyeLift * cos;
            g.FillRectangle(_blobPixelRedBrush,
                MathF.Round((eyeX - eyeSize * 0.5f) * 0.5f) * 2f,
                MathF.Round((eyeY - eyeSize * 0.5f) * 0.5f) * 2f,
                eyeSize,
                eyeSize);
        }
        DrawEye(-eyeOffset);
        DrawEye(eyeOffset);
    }

    private void DrawBodyDebug(Graphics g, SoftBody body)
    {
        _debugConstraintPath.Reset();
        _debugParticlePath.Reset();
        _debugSupportedParticlePath.Reset();
        foreach (var constraint in body.Constraints)
        {
            if (constraint.Broken) continue;
            var a = body.Particles[constraint.A].Position;
            var b = body.Particles[constraint.B].Position;
            _debugConstraintPath.StartFigure();
            _debugConstraintPath.AddLine(a.X, a.Y, b.X, b.Y);
        }
        if (_debugConstraintPath.PointCount > 0) g.DrawPath(_constraintPen, _debugConstraintPath);
        for (var particleIndex = 0; particleIndex < body.Particles.Length; particleIndex++)
        {
            if (!body.IsPhysicalParticle(particleIndex)) continue;
            var particle = body.Particles[particleIndex];
            var marker = new RectangleF(
                MathF.Round(particle.Position.X) - 2f,
                MathF.Round(particle.Position.Y) - 2f,
                4f,
                4f);
            (particle.Contacting ? _debugSupportedParticlePath : _debugParticlePath)
                .AddRectangle(marker);
        }
        if (_debugParticlePath.PointCount > 0)
            g.FillPath(_debugParticleBrush, _debugParticlePath);
        if (_debugSupportedParticlePath.PointCount > 0)
            g.FillPath(_debugSupportedParticleBrush, _debugSupportedParticlePath);
    }

    private void DrawInstructions(Graphics g, Size viewport)
    {
        using var titleBrush = new SolidBrush(Color.FromArgb(235, 228, 244, 255));
        using var textBrush = new SolidBrush(Color.FromArgb(205, 177, 199, 217));
        g.DrawString("BLOBFORGE // SOFT-BODY LAB", _titleFont, titleBrush, 22, 18);
        g.DrawString("B request next   C conveyor   L lantern   drag counter/breaker   click/drag edit   wheel cable   Del remove", _hudFont, textBrush, 24, 49);

        using var tagBrush = new SolidBrush(Color.FromArgb(185, 12, 16, 24));
        g.FillRectangle(tagBrush, viewport.Width - 225, 18, 198, 30);
        g.DrawString("120Hz BODY  •  60Hz MATTER", _hudFont, textBrush, viewport.Width - 216, 25);
    }

    private void DrawDebug(Graphics g, BlobWorld world)
    {
        const int x = 20;
        const int y = 82;
        var now = Environment.TickCount64;
        if (now >= _nextDebugPanelRefresh)
        {
            _nextDebugPanelRefresh = now + 200;
            using var panelGraphics = Graphics.FromImage(_debugPanel);
            panelGraphics.Clear(Color.Transparent);
            panelGraphics.FillRectangle(_debugPanelBackgroundBrush, 0, 0, _debugPanel.Width, _debugPanel.Height);
            var lines = new[]
            {
                $"FPS                 {Fps,7:0.0}",
                $"frame               {FrameMs,7:0.00} ms",
                $"world render        {RenderMs,7:0.00} ms",
                $"frame present       {PresentMs,7:0.00} ms",
                $"fixed updates       {FixedUpdateMs,7:0.00} ms",
                $"audio state         {AudioUpdateMs,7:0.00} ms",
                $"simulation total    {world.LastSimulationMs,7:0.00} ms",
                $"body physics        {world.LastBodyPhysicsMs,7:0.00} ms",
                $"granular sim        {world.LastGranularSimulationMs,7:0.00} ms",
                $"steps / skipped     {world.StepsThisFrame,3} / {world.SkippedSteps}",
                $"blobs / sleeping    {world.Bodies.Count,3} / {world.SleepingCount}",
                $"tissue particles    {world.Bodies.Sum(b => b.Particles.Length),7}",
                $"bonds               {world.Bodies.Sum(b => b.Constraints.Count),7}",
                $"local areas         {world.Bodies.Sum(b => b.AreaConstraints.Count),7}",
                $"broken bonds        {world.Bodies.Sum(b => b.BrokenLinkCount),7}",
                $"solver iterations   {world.LastConstraintIterations,7}",
                $"contacts            {world.ContactsThisStep,7}",
                $"blob contacts       {world.BlobContactsThisStep,7}",
                $"splits frame/total  {world.TopologySplitsThisStep,3} / {world.TotalTopologySplits}",
                $"detached chunks     {world.DetachedChunkCount,7}",
                $"active wounds       {world.ActiveWoundCount,7}",
                $"tissue pixels       {world.Granular.TissuePixelCount,7}",
                $"blood pixels        {world.Granular.BloodCount,7}",
                $"blood spawned       {world.Granular.BloodSpawnedThisStep,7}",
                $"impact splatters    {world.Granular.BloodSplatteredThisStep,7}",
                $"stain marks         {world.Grid.StainedCellCount,7}",
                $"destroyed cells     {world.Grid.DestroyedCellCount,7}",
            };
            for (var i = 0; i < lines.Length; i++)
                panelGraphics.DrawString(lines[i], _hudFont, _debugPanelTextBrush, 10, 9 + i * 16);
        }
        g.DrawImageUnscaled(_debugPanel, x, y);
    }
}
