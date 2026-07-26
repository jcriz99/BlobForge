using System.Numerics;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using BlobForge.Physics;
using BlobForge.Rendering;
using BlobForge.World;
using BlobForge.Audio;

namespace BlobForge.Diagnostics;

public static class SelfTests
{
    private const float Dt = 1f / 120f;

    public static int RunAll()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("standard archetype spawns consistently", StandardArchetypeSpawnsConsistently),
            ("processing units use the compact physical scale", ProcessingUnitUsesCompactScale),
            ("fixed viewport fits and maps the whole world", FixedViewportFitsAndMapsWorld),
            ("debug overlay layers toggle independently", DebugOverlayLayersToggleIndependently),
            ("factory tiles follow structural topology", FactoryTilesFollowStructure),
            ("holding chamber contains releases and feeds one at a time", HoldingChamberFeedsOneAtATime),
            ("lever holds the hatch and pixel lighting stays cached", LeverAndLightingAreDeterministic),
            ("main breaker requires a downward handle pull", BreakerRequiresDownwardHandlePull),
            ("powered breaker reverses upward to end the day", BreakerReversesUpwardToPowerOff),
            ("holding chamber receives and releases a full soft body", HoldingChamberReceivesAndReleasesBody),
            ("powered receiving tub replaces the chamber support tower", ReceivingTubReplacesTerrainTower),
            ("released blobs cannot re-enter the holding chamber", ReleasedBlobCannotReenterChamber),
            ("blood treats the chamber tube as environment", BloodTreatsChamberTubeAsEnvironment),
            ("processing line has independent back-pressure segments", ProcessingLineBackPressureIsIndependent),
            ("continuous flow uses one offscreen belt with machinery removed", ContinuousFlowUsesSingleAutomaticLine),
            ("continuous conveyor keeps committed tissue above its top run", ContinuousConveyorContainsCommittedTissue),
            ("a small spawned gore fraction can fall through conveyors", SpawnedGoreCanBypassConveyors),
            ("overhead tube tumbles untouchable blobs before release", OverheadTubeBlobsTumbleWithoutInteraction),
            ("overhead tube visibly stages and releases physical blobs", OverheadTubeStagesAndReleasesBodies),
            ("overhead tube glass preserves hard-impact weapon damage", OverheadTubeGlassPreservesHardImpactDamage),
            ("continuous belt enters through physical wall portals", ContinuousBeltUsesWallPortals),
            ("butcher cleaver physically contacts and cuts along its visible edge", KnifePokesPhysicalTissue),
            ("cleaver swings only on LMB and blunt surfaces never cut", CleaverFacesMovementAndOnlyEdgeDamages),
            ("cleaver carries persistent contact-local blood stains", CleaverCarriesBloodStains),
            ("blood pixels stain contacted blob tissue persistently", BloodPixelsStainBlobTissue),
            ("detached tissue retains some cute blob coloration", DetachedTissueRetainsBlobColor),
            ("dropped cleaver settles and rides without belt-driven rolling", DroppedCleaverRidesWithoutRolling),
            ("nearby cleaver flies back to its centered wall rack", KnifeReturnsToHolster),
            ("arsenal selection swaps the grabbable tool in the centered rack", ArsenalSelectionSwapsCenteredTool),
            ("physical dumbwaiter token deposits and rerolls one unlocked weapon", WeaponDumbwaiterRerollsOneWeapon),
            ("arsenal primary actions dispatch weapon-specific behavior", ArsenalPrimaryActionsAreDistinct),
            ("slingshot impacts damage launched and struck blobs", SlingshotImpactsDamageBothBodies),
            ("expanded arsenal entities keep their promised distinct mechanics", ExpandedArsenalMechanicsAreDistinct),
            ("explosive fractures launch outward with varied fragment spin", ExplosiveFracturesHaveVariedSpin),
            ("single exit drain routes blood and tissue after full belt travel", ContinuousEndDrainRoutesMatter),
            ("spike crusher locks damages and releases one blob", SpikeCrusherCycleIsLocalized),
            ("bay two spike drill holds damages and releases one blob", SpikeDrillCycleRequiresHeldLever),
            ("bays three through five use distinct player-operated machinery", FinalMachineControlsAreDistinct),
            ("machine drains feed the conserved blood basin", MachineDrainsFeedBasin),
            ("basin inflow sloshes internally without staining the floor below", BasinInflowStaysContained),
            ("full basin keeps its physical drain open and spills excess", FullBasinKeepsDrainOpenAndSpillsExcess),
            ("purchased blood worker forms routes and operates real machinery", BloodWorkerAutomatesMachinery),
            ("basin blood uses conserved sleeping cellular fluid", BasinFluidIsCellularAndConserved),
            ("basin gallons liters and day payout share one calibrated volume", BasinVolumeAndPayoutAreCalibrated),
            ("day-end trucks drain conserved basin blood as physical pixels", DayEndShipmentConservesBlood),
            ("later machine bays produce progressively richer basin blood", LaterBaysIncreaseBloodYield),
            ("Diego is dormant and basin bubbles wait for 35% fill", DiegoIsDormantAndBubblesWaitForFill),
            ("machine platforms stay narrow between transfer belts", NarrowTablesEjectToNextTransfer),
            ("final conveyor reliably transfers blobs into the output cart", FinalConveyorTransfersIntoCart),
            ("loaded output cart dispatches and returns empty", OutputCartDispatchesAndReturns),
            ("blobs stay grabbable on conveyors until Bay 1 capture", BayOneEntryDisablesPickup),
            ("cart foreground occludes every loose matter layer", CartForegroundOccludesLooseMatter),
            ("cart floor contains blobs and blood while dispatching", CartFloorContainsMovingContents),
            ("cart walls contain fast granular blood", CartWallsContainFastBlood),
            ("audio settings persist master SFX and music buses", AudioSettingsPersistBusVolumes),
            ("filled tissue lattice", FilledTissueLattice),
            ("local area preservation", AreaPreservation),
            ("flat-floor deterministic rest", FlatFloorRest),
            ("deformed tissue deterministic rest", DeformedTissueRest),
            ("grab promotes full tissue", GrabPromotesFullTissue),
            ("representation scheduler prioritizes and caps active tissue", RepresentationSchedulerPrioritizesAndCapsActiveTissue),
            ("event damage destroys terrain cell", ImpactDamageDestroysCell),
            ("tissue bonds are damageable", TissueBondsBreak),
            ("blob faces blink and briefly react to damage", BlobFacesBlinkAndReactToDamage),
            ("interior point damage redirects to visible tissue", InteriorPointDamageRedirects),
            ("outside point damage is ignored", OutsidePointDamageIsIgnored),
            ("single point hit removes only local tissue", SinglePointHitStaysLocal),
            ("single point bite cannot leave cell-less ghost chunks", PointBiteCannotLeaveGhostChunk),
            ("click bite and drag slice stay distinct", DamageGesturesStayDistinct),
            ("straight slice creates flat matching surfaces", StraightSliceCreatesFlatSurfaces),
            ("cut segments stay inside each child silhouette", CutSegmentsStayInsideChildSilhouette),
            ("damaged collision nodes belong to visible tissue", DamagedCollisionNodesBelongToVisibleTissue),
            ("damaged visual shell contains every physical contact center", DamagedShellContainsPhysicalCenters),
            ("repeated point bites keep every contact inside the visual shell", RepeatedPointBitesContainContacts),
            ("damaged contours are simple bounded and deterministic", DamagedContoursAreStable),
            ("damaged contour extent does not pop between frames", DamagedContourExtentDoesNotPop),
            ("heavily damaged resting tissue cannot size-flip", HeavilyDamagedRestCannotSizeFlip),
            ("contour topology is rebuilt only on damage events", ContourTopologyIsEventCached),
            ("dense drag sampling is topology invariant", DenseDragSamplingIsInvariant),
            ("collision hull is derived from rendered material", CollisionHullUsesMaterialContour),
            ("cut surface follows body translation and rotation", CutSurfaceFollowsBodyTransform),
            ("cut geometry renders without exception", CutGeometryRenders),
            ("cut creates independent components", CutCreatesComponents),
            ("fresh cut parent and chunk do not interlock", FreshCutPiecesDoNotInterlock),
            ("detached tissue stays visible before granulating", DetachedChunkLifecycle),
            ("airborne detached tissue retains its cut-time shape", AirborneChunkRetainsShape),
            ("sleeping cut partners do not fake a landing", SleepingCutPartnersDoNotCrumble),
            ("crumbling tissue cannot render lattice crowns", CrumblingContourCannotCrown),
            ("crumbling tissue cannot leave ghost node chains", CrumblingCannotLeaveGhostNodes),
            ("multiple blobs physically separate", MultipleBlobsSeparate),
            ("sustained pressure does not launch passive blob", SustainedPressureDoesNotLaunch),
            ("held blob pressure visibly squishes and recoils", HeldBlobPressureSquishesAndRecoils),
            ("held side contact preserves passive gravity", HeldSideContactPreservesGravity),
            ("repeated hard jamming cannot intertwine blobs", RepeatedJammingCannotIntertwine),
            ("stacked blobs settle without chatter", StackedBlobsSettle),
            ("dense blob pile loses energy and sleeps", DenseBlobPileSettles),
            ("grounded blob retains rolling momentum", GroundedBlobKeepsRolling),
            ("blob falls through an unsupported gap", BlobFallsThroughGap),
            ("editable conveyor carries blobs without launching", ConveyorCarriesBlob),
            ("grab target is bounded and rate limited", GrabTargetIsBounded),
            ("held blob compresses against side walls without crossing them", HeldBlobSquishesAgainstSideWall),
            ("held pressure cannot pin another blob into the floor", HeldPressureCannotEmbedBlobInFloor),
            ("held blob pressure cannot stretch bodies against a wall", HeldPressureCannotStretchAtWall),
            ("held blob stays synchronized with pointer", GrabStaysSynchronized),
            ("thrown blob retains airborne momentum", ThrowRetainsMomentum),
            ("arena walls reject whole blob", ArenaWallsRejectWholeBlob),
            ("wounds emit simulated blood", WoundsEmitBlood),
            ("bleeding slows and clots over time", BleedingSlowsAndClots),
            ("blood emission has bounded upward speed", BloodEmissionIsWoundLike),
            ("blood pixels cannot remain inside blob tissue", BloodCannotRemainInsideBlob),
            ("overcrowded blood and tissue fall through the foreground", DenseGranularPilesBecomeForegroundSpills),
            ("blood paints terrain and dries persistently", BloodPaintsAndDriesOnTerrain),
            ("dried stains wait for explicit cleaning", DriedStainsWaitForCleaning),
            ("fresh blood diversifies dried runoff", FreshBloodDiversifiesDriedRunoff),
            ("terrain runoff stays in front of tile interiors", TerrainRunoffStaysInFrontOfTileInteriors),
            ("wall blood survives stain churn and forms runoff", WallBloodSurvivesChurnAndDrips),
            ("active blood trails survive stain-layer churn", ActiveBloodTrailsSurviveStainChurn),
            ("old saturated blood zones can renew runoff", SaturatedBloodZoneRenewsRunoff),
            ("settled blood pools keep staining a saturated floor", SettledBloodPoolKeepsStaining),
            ("mass damage remains event-budgeted", MassDamageRemainsBudgeted),
            ("blob personalities produce varied occasional hops", BlobPersonalitiesProduceVariedHops)
        };

        var failed = 0;
        Console.WriteLine("BlobForge Matter Lab regression suite");
        foreach (var test in tests)
        {
            try
            {
                test.Run();
                Console.WriteLine($"  PASS  {test.Name}");
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"  FAIL  {test.Name}: {ex.Message}");
            }
        }
        Console.WriteLine(failed == 0 ? "All tests passed." : $"{failed} test(s) failed.");
        return failed == 0 ? 0 : 1;
    }

    private static void BreakerRequiresDownwardHandlePull()
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        var line = new ProcessingLine(
            DestructibleGrid.ProcessingDeckRow * grid.CellSize,
            powered: false,
            breakerPosition: new Vector2(190f, 45f));
        var world = new BlobWorld(grid) { ProcessingLine = line };

        var housingOnly = new Vector2(line.BreakerBounds.Left + 10f, line.BreakerBounds.Bottom - 10f);
        Assert(line.HitBreaker(housingOnly) && !line.BeginBreakerLeverDrag(housingOnly),
            "clicking the breaker housing still behaved like the power switch");
        world.Step(Dt);
        Assert(!line.Powered, "breaker housing click powered the factory");

        var handle = line.BreakerLeverHandle;
        Assert(line.BeginBreakerLeverDrag(handle), "breaker handle could not be grabbed");
        var partial = Vector2.Lerp(line.BreakerTrackTop, line.BreakerTrackBottom, 0.55f);
        Assert(!line.DragBreakerLever(partial), "partial breaker pull latched power too early");
        line.EndBreakerLeverDrag();
        for (var step = 0; step < 30; step++) world.Step(Dt);
        Assert(!line.Powered && line.BreakerLever < 0.05f,
            "released partial breaker pull did not spring back without powering on");

        Assert(line.BeginBreakerLeverDrag(line.BreakerLeverHandle),
            "breaker handle could not be grabbed for a full pull");
        Assert(line.DragBreakerLever(line.BreakerTrackBottom + new Vector2(0f, 8f)),
            "full downward breaker pull did not cross the latching threshold");
        line.EndBreakerLeverDrag();
        world.Step(Dt);
        Assert(line.Powered && line.BreakerLever >= 0.99f,
            "latched breaker pull failed to initiate factory power");

        line.SetBreakerPosition(new Vector2(360f, 90f), 1280f, 720f);
        Assert(MathF.Abs(line.BreakerBounds.Left - 360f) < 0.01f &&
               MathF.Abs(line.BreakerBounds.Top - 90f) < 0.01f,
            "breaker housing could no longer be repositioned independently of its handle");
    }

    private static void BreakerReversesUpwardToPowerOff()
    {
        var line = new ProcessingLine(480f, powered: true);
        var world = new BlobWorld(FlatGrid()) { ProcessingLine = line };
        Assert(line.BeginBreakerLeverDrag(line.BreakerLeverHandle),
            "powered breaker handle could not be grabbed");
        var partial = Vector2.Lerp(line.BreakerTrackTop, line.BreakerTrackBottom, 0.55f);
        Assert(!line.DragBreakerLever(partial),
            "partial upward breaker pull ended the day too early");
        line.EndBreakerLeverDrag();
        for (var step = 0; step < 30; step++) world.Step(Dt);
        Assert(line.Powered && line.BreakerLever > 0.98f,
            "released partial shutdown did not restore the live breaker");

        Assert(line.BeginBreakerLeverDrag(line.BreakerLeverHandle),
            "live breaker could not begin a full upward pull");
        Assert(line.DragBreakerLever(line.BreakerTrackTop - new Vector2(0f, 8f)) &&
               line.PoweringDown,
            "full upward pull did not begin the shutdown sequence");
        line.EndBreakerLeverDrag();
        for (var step = 0; step < 90 && line.Powered; step++) world.Step(Dt);
        Assert(!line.Powered && !line.PoweringDown && line.BreakerLever < 0.01f,
            "shutdown sequence did not finish with power and lever fully off");
    }

    private static void FixedViewportFitsAndMapsWorld()
    {
        var logical = new Size(1280, 720);
        foreach (var available in new[] { new Size(960, 620), new Size(1280, 720), new Size(1920, 1080), new Size(3440, 1440) })
        {
            var viewport = ViewportLayout.Fit(available, logical);
            Assert(viewport.Left >= 0 && viewport.Top >= 0 && viewport.Right <= available.Width &&
                   viewport.Bottom <= available.Height,
                $"viewport {viewport} escaped available area {available}");
            var topLeft = ViewportLayout.ToWorld(viewport.Location, viewport, logical, true);
            var bottomRight = ViewportLayout.ToWorld(new Point(viewport.Right, viewport.Bottom), viewport, logical, true);
            Assert(Vector2.DistanceSquared(topLeft, Vector2.Zero) < 0.001f,
                "viewport top-left did not map to world origin");
            Assert(Vector2.DistanceSquared(bottomRight, new Vector2(logical.Width, logical.Height)) < 0.01f,
                "viewport bottom-right did not expose the full logical world");
            var expectedScale = MathF.Min(
                available.Width / (float)logical.Width,
                available.Height / (float)logical.Height);
            var expectedSize = new Size(
                Math.Max(1, (int)MathF.Floor(logical.Width * expectedScale)),
                Math.Max(1, (int)MathF.Floor(logical.Height * expectedScale)));
            Assert(viewport.Size == expectedSize,
                $"viewport {viewport.Size} did not use the available aspect-fit scale {expectedSize}");
        }
        Assert(ViewportLayout.Fit(new Size(1920, 1080), logical) == new Rectangle(0, 0, 1920, 1080),
            "16:9 fullscreen did not fill the display");
    }

    private static void DebugOverlayLayersToggleIndependently()
    {
        var renderer = new GameRenderer { DebugDraw = true };
        Assert(renderer.DebugShowFps &&
               renderer.DebugShowBlobPoints &&
               !renderer.DebugShowBonds &&
               renderer.DebugShowToolColliders &&
               !renderer.DebugShowMetrics,
            "debug overlay did not start in the lightweight inspection layout");

        Assert(renderer.TryHandleDebugOverlayClick(new Vector2(227f, 93f)) &&
               !renderer.DebugShowBlobPoints &&
               renderer.DebugShowToolColliders,
            "BLOBS toggle also hid the independent tool colliders");
        Assert(renderer.TryHandleDebugOverlayClick(new Vector2(428f, 93f)) &&
               renderer.DebugShowMetrics,
            "METRICS toggle did not expose the detailed diagnostic text");
        Assert(renderer.TryHandleDebugOverlayClick(new Vector2(105f, 93f)) &&
               !renderer.DebugShowFps &&
               !renderer.DebugShowBlobPoints &&
               !renderer.DebugShowBonds &&
               !renderer.DebugShowToolColliders &&
               !renderer.DebugShowMetrics,
            "ALL- did not disable every optional debug layer");
        Assert(renderer.TryHandleDebugOverlayClick(new Vector2(47f, 93f)) &&
               renderer.DebugShowFps &&
               renderer.DebugShowBlobPoints &&
               renderer.DebugShowBonds &&
               renderer.DebugShowToolColliders &&
               renderer.DebugShowMetrics,
            "ALL+ did not restore every optional debug layer");
    }

    private static void FactoryTilesFollowStructure()
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildSampleArena();

        for (var y = 0; y < grid.Rows - 1; y++)
        {
            var left = GameRenderer.SelectFactoryTile(grid, 0, y, grid.Cell(0, y));
            var right = GameRenderer.SelectFactoryTile(grid, grid.Columns - 1, y, grid.Cell(grid.Columns - 1, y));
            Assert(left == right, $"outer wall service bays do not mirror at row {y}");
        }

        for (var x = 1; x < grid.Columns - 1; x++)
        {
            var tile = GameRenderer.SelectFactoryTile(grid, x, grid.Rows - 1, grid.Cell(x, grid.Rows - 1));
            Assert(tile == (3, 2), $"foundation changed course at column {x}");
        }

        for (var y = 4; y < 11; y++)
        for (var x = 28; x <= 29; x++)
        {
            var tile = GameRenderer.SelectFactoryTile(grid, x, y, grid.Cell(x, y));
            Assert(tile == (2, 0), $"interior glass panel at {x},{y} was not a framed window");
        }

        var pillarEdge = GameRenderer.SelectFactoryTile(grid, 20, 10, grid.Cell(20, 10));
        var pillarService = GameRenderer.SelectFactoryTile(grid, 21, 10, grid.Cell(21, 10));
        Assert(pillarEdge == (1, 0), "concrete edge received an interior service tile");
        Assert(pillarService == (1, 3), "wide concrete column has no planned service hatch");

        var backdropPath = Path.Combine(AppContext.BaseDirectory, "Assets", "FactoryBackdropTileset.png");
        Assert(File.Exists(backdropPath), "factory background tileset is missing from the runnable asset set");
        using var backdrop = new Bitmap(backdropPath);
        Assert(backdrop.Size == new Size(256, 128),
            $"factory background tileset has invalid dimensions ({backdrop.Width}x{backdrop.Height})");
        Assert(GameRenderer.SelectFactoryBackgroundTile(4, 6, 22) == (1, 0),
            "upper utility chase did not use a continuous conduit tile");
        Assert(GameRenderer.SelectFactoryBackgroundTile(5, 6, 22) == (1, 5),
            "upper utility chase has no valve/gauge variation");
        Assert(GameRenderer.SelectFactoryBackgroundTile(7, 6, 22) == (1, 3),
            "upper utility chase has no planned junction box");
        Assert(GameRenderer.SelectFactoryBackgroundTile(9, 8, 22) == (2, 2),
            "utility chase corner did not continue down the wall");
        Assert(GameRenderer.SelectFactoryBackgroundTile(11, 11, 22) == (2, 4),
            "machinery wall has no aligned access panel");
        Assert(GameRenderer.SelectFactoryBackgroundTile(2, 18, 22) == (3, 2),
            "lower basin wall has no drainage grille");

        var backgroundVariants = new HashSet<(int Row, int Column)>();
        for (var y = 0; y < 22; y++)
        for (var x = 0; x < 40; x++)
            backgroundVariants.Add(GameRenderer.SelectFactoryBackgroundTile(x, y, 22));
        Assert(backgroundVariants.Count >= 24,
            $"factory background placement only used {backgroundVariants.Count} tile variations");
    }

    private static void HoldingChamberFeedsOneAtATime()
    {
        var chamber = new HoldingChamber(new Vector2(250f, 250f), 120f);
        var closedParticle = new Particle
        {
            Position = chamber.Center + new Vector2(0f, 118f),
            PreviousPosition = chamber.Center + new Vector2(0f, 112f),
            Radius = 8f,
            InverseMass = 1f
        };
        var closedContact = chamber.ResolveParticle(ref closedParticle, Dt);
        Assert(closedContact.Hit && closedContact.IsTop,
            "closed chamber hatch did not support its contents");

        chamber.BeginLeverDrag(chamber.LeverRestHandle);
        var leverLength = Vector2.Distance(chamber.LeverPivot, chamber.LeverHandle);
        chamber.UpdateLeverDrag(chamber.LeverRestHandle + new Vector2(180f, 0f));
        Assert(MathF.Abs(Vector2.Distance(chamber.LeverPivot, chamber.LeverHandle) - leverLength) < 0.01f,
            "lever arm changed length instead of rotating around its pivot");
        chamber.Step(0.25f);
        Assert(chamber.IsOpen, "rightward lever pull did not open the hatch");
        for (var i = 0; i < 240; i++) chamber.Step(Dt);
        Assert(chamber.IsOpen, "held lever allowed the release hatch to close");
        var releasedParticle = new Particle
        {
            Position = chamber.Center + new Vector2(0f, 118f),
            PreviousPosition = chamber.Center + new Vector2(0f, 112f),
            Radius = 8f,
            InverseMass = 1f
        };
        Assert(!chamber.ResolveParticle(ref releasedParticle, Dt).Hit,
            "open hatch still blocked the release opening");

        var clearedParticle = new Particle
        {
            Position = chamber.Center + new Vector2(180f, 135f),
            PreviousPosition = chamber.Center + new Vector2(175f, 135f),
            Radius = 6f,
            InverseMass = 1f
        };
        Assert(!chamber.ResolveParticle(ref clearedParticle, Dt).Hit,
            "cleared body particle was still captured by the chamber field");
        chamber.EndLeverDrag();
        for (var i = 0; i < 150; i++) chamber.Step(Dt);
        Assert(chamber.HatchOpen < 0.015f,
            "release hatch stayed open too long after the lever was released");

        var feedChamber = new HoldingChamber(new Vector2(250f, 210f), 120f);
        var feed = new ChamberFeedController(feedChamber);
        var bodies = new List<SoftBody>();
        for (var i = 0; i < 40; i++) feed.Update(bodies, Dt, BlobArchetype.ProcessingUnit.Create);
        Assert(bodies.Count == 1 && feed.UnitsSpawned == 1,
            "automatic feed did not create exactly one incoming unit");
        Assert(feedChamber.UnitsProduced == 1,
            "holding-chamber production counter did not record the first unit");
        Assert(feedChamber.IsAdmitted(bodies[0]),
            "factory-created unit was not admitted into the chamber");
        for (var i = 0; i < 120; i++) feed.Update(bodies, Dt, BlobArchetype.ProcessingUnit.Create);
        Assert(bodies.Count == 1, "occupied chamber spawned a second unit");

        var releasedBody = bodies[0];
        for (var i = 0; i < releasedBody.Particles.Length; i++)
        {
            releasedBody.Particles[i].Position += new Vector2(0f, 420f);
            releasedBody.Particles[i].PreviousPosition += new Vector2(0f, 420f);
        }
        feedChamber.TriggerRelease();
        for (var i = 0; i < 110; i++)
        {
            feedChamber.Step(Dt);
            feed.Update(bodies, Dt, BlobArchetype.ProcessingUnit.Create);
        }
        Assert(bodies.Count == 1,
            "next unit entered before the release hatch closed");
        for (var i = 0; i < 300; i++)
        {
            feedChamber.Step(Dt);
            feed.Update(bodies, Dt, BlobArchetype.ProcessingUnit.Create);
        }
        Assert(bodies.Count == 2 && feed.UnitsSpawned == 2,
            "feed did not replenish after the released unit cleared the chamber and hatch closed");
        Assert(feedChamber.UnitsProduced == 2,
            "holding-chamber production counter did not track replenished units");
        Assert(!feedChamber.IsAdmitted(releasedBody),
            "released unit retained permission to re-enter the chamber");
    }

    private static void ProcessingLineBackPressureIsIndependent()
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        var line = new ProcessingLine(DestructibleGrid.ProcessingDeckRow * grid.CellSize);
        var world = new BlobWorld(grid) { ProcessingLine = line, Gravity = Vector2.Zero };
        world.Conveyors.AddRange(line.Belts);
        var active = BlobArchetype.ProcessingUnit.Create(new Vector2(line.Bays[0].CenterX, line.DeckY - 30f));
        var queued = BlobArchetype.ProcessingUnit.Create(new Vector2(line.Bays[0].Left - 9f, line.DeckY - 30f));
        world.Bodies.Add(active);
        world.Bodies.Add(queued);

        world.Step(Dt);
        Assert(ReferenceEquals(line.LockedBody, active), "crusher did not lock the centered blob");
        Assert(world.PickBody(active.Center) is null, "crusher-locked blob remained grabbable");
        Assert(MathF.Abs(line.Belts[0].Speed) < 0.01f, "queued feed belt failed to stop near the occupied machine");
        Assert(line.Belts.Skip(1).All(belt => MathF.Abs(belt.Speed - ProcessingLine.OperatingSpeed) < 0.01f),
            "back-pressure stopped unrelated downstream conveyor segments");
        Assert(line.Belts.Count == 6 && line.Bays.Count == 5,
            "processing line did not reserve five narrow tables between independent transfer belts");
    }

    private static void SpikeCrusherCycleIsLocalized()
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        var line = new ProcessingLine(DestructibleGrid.ProcessingDeckRow * grid.CellSize);
        var world = new BlobWorld(grid) { ProcessingLine = line, Gravity = Vector2.Zero };
        world.Conveyors.AddRange(line.Belts);
        var blob = BlobArchetype.ProcessingUnit.Create(new Vector2(line.Bays[0].CenterX, line.DeckY - 30f));
        world.Bodies.Add(blob);
        world.Step(Dt);
        line.SetCrusherButtonHeld(true);
        for (var i = 0; i < 100; i++) world.Step(Dt);
        var broken = world.Bodies.Where(body => body.ParentId == blob.ParentId).Sum(body => body.BrokenLinkCount);
        Assert(line.CrusherTravel > 0.98f, "held crusher button did not lower the press");
        Assert(broken > 0, "spike tips made no localized structural wounds");
        Assert(broken < 45, $"single crusher cycle caused unbounded repeated damage ({broken} links)");
        var collisionBody = line.LockedBody ?? world.Bodies.First(body => body.ParentId == blob.ParentId);
        var gapParticle = new Particle
        {
            Position = new Vector2(line.Bays[0].CenterX - 27f, line.CrusherHeadTop + 44f),
            PreviousPosition = new Vector2(line.Bays[0].CenterX - 27f, line.CrusherHeadTop + 44f),
            Radius = 1.25f
        };
        var gapContact = line.ResolveParticle(collisionBody, ref gapParticle, Dt);
        Assert(!gapContact.Hit, "crusher still used a flat slab across the visible gaps between spikes");
        var tipParticle = new Particle
        {
            Position = new Vector2(line.Bays[0].CenterX - 36f, line.CrusherHeadTop + 47f),
            PreviousPosition = new Vector2(line.Bays[0].CenterX - 36f, line.CrusherHeadTop + 47f),
            Radius = 1.25f
        };
        Assert(line.ResolveParticle(collisionBody, ref tipParticle, Dt).Hit,
            "drawn crusher spike tip was missing matching physical contact geometry");

        line.SetCrusherButtonHeld(false);
        for (var i = 0; i < 80; i++) world.Step(Dt);
        Assert(line.CrusherTravel < 0.01f && line.LockedBody is null,
            "crusher failed to retract and release after the button was released");
        Assert(world.Bodies.Where(body => body.ParentId == blob.ParentId).All(body => !line.IsLocked(body)),
            "processed blob remained machine-locked after the cycle");
    }

    private static void SpikeDrillCycleRequiresHeldLever()
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        var line = new ProcessingLine(DestructibleGrid.ProcessingDeckRow * grid.CellSize);
        var world = new BlobWorld(grid) { ProcessingLine = line, Gravity = Vector2.Zero };
        world.Conveyors.AddRange(line.Belts);
        var blob = BlobArchetype.ProcessingUnit.Create(new Vector2(line.Bays[0].CenterX, line.DeckY - 30f));
        world.Bodies.Add(blob);
        world.Step(Dt);
        line.SetCrusherButtonHeld(true);
        for (var i = 0; i < 100; i++) world.Step(Dt);
        line.SetCrusherButtonHeld(false);
        for (var i = 0; i < 80; i++) world.Step(Dt);

        blob = world.Bodies.Where(body => body.ParentId == blob.ParentId)
            .OrderByDescending(body => body.Particles.Length)
            .First();
        blob.ApplyTranslation(new Vector2(
            line.Bays[1].CenterX - blob.Center.X,
            line.DeckY - 30f - blob.Center.Y), preserveVelocity: true);
        blob.AddImpulse(-blob.AverageVelocity(Dt), Dt);
        world.Step(Dt);
        Assert(ReferenceEquals(line.DrillLockedBody, blob), "processed blob did not lock into bay two");
        line.SetDrillLeverHeld(true);
        var sawSpinUp = false;
        var sawImpactRecoil = false;
        for (var i = 0; i < 125; i++)
        {
            world.Step(Dt);
            sawSpinUp |= line.DrillSpinSpeed >= 30f;
            sawImpactRecoil |= line.DrillRecoil > 0.9f;
        }
        Assert(line.DrillTravel > 0.98f, "held drill lever did not lower the rotating bit");
        Assert(sawSpinUp, "drill never reached its violent high-speed spin state");
        Assert(sawImpactRecoil, "drill contact produced no visible hammer recoil");
        Assert(line.DrillBrokenLinks > 0,
            $"drill contact produced no localized structural wound (pulses {line.DrillDamagePulses}, " +
            $"center {line.DrillLockedBody?.Center}, tip {line.DrillTip})");
        Assert(line.DrillBrokenLinks < 55,
            $"single drill cycle caused unbounded repeated damage ({line.DrillBrokenLinks} links)");

        line.SetDrillLeverHeld(false);
        for (var i = 0; i < 90; i++) world.Step(Dt);
        Assert(line.DrillTravel < 0.01f && line.DrillLockedBody is null,
            "drill failed to retract and release after the lever was released");
        Assert(line.DrillSpinSpeed < 0.01f, "drill motor failed to spin down after release");
    }

    private static void FinalMachineControlsAreDistinct()
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        var line = new ProcessingLine(DestructibleGrid.ProcessingDeckRow * grid.CellSize);
        var world = new BlobWorld(grid) { ProcessingLine = line, Gravity = Vector2.Zero };
        world.Conveyors.AddRange(line.Belts);
        var seed = BlobArchetype.ProcessingUnit.Create(new Vector2(line.Bays[0].CenterX, line.DeckY - 30f));
        var parentId = seed.ParentId;
        world.Bodies.Add(seed);

        SoftBody Largest() => world.Bodies
            .Where(body => body.ParentId == parentId)
            .OrderByDescending(body => body.Particles.Length)
            .First();
        SoftBody MoveToBay(int bayIndex)
        {
            var body = Largest();
            body.ApplyTranslation(new Vector2(
                line.Bays[bayIndex].CenterX - body.Center.X,
                line.DeckY - 30f - body.Center.Y), preserveVelocity: true);
            body.AddImpulse(-body.AverageVelocity(Dt), Dt);
            world.Step(Dt);
            return body;
        }

        world.Step(Dt);
        line.SetCrusherButtonHeld(true);
        for (var i = 0; i < 100; i++) world.Step(Dt);
        line.SetCrusherButtonHeld(false);
        for (var i = 0; i < 80; i++) world.Step(Dt);

        MoveToBay(1);
        line.SetDrillLeverHeld(true);
        for (var i = 0; i < 125; i++) world.Step(Dt);
        line.SetDrillLeverHeld(false);
        for (var i = 0; i < 90; i++) world.Step(Dt);

        var pressBody = MoveToBay(2);
        Assert(ReferenceEquals(line.PressLockedBody, pressBody), "bay three failed to capture the drilled unit");
        Assert(ProcessingLine.DrumInteriorRadius >= pressBody.Radius + 6f,
            $"bay three drum opening is visibly smaller than its processing blob " +
            $"({ProcessingLine.DrumInteriorRadius:0.0} radius vs {pressBody.Radius:0.0})");
        Assert(line.DrumLoading, "bay three skipped its visible intake lift sequence");
        Assert(!line.BeginDrumWheelDrag(line.DrumWheelCenter + new Vector2(20f, 0f)),
            "bay three allowed its drum to spin before the blob finished loading");
        var intakeStartY = pressBody.Center.Y;
        var loadFrames = 0;
        for (; loadFrames < 56 && line.DrumLoading; loadFrames++) world.Step(Dt);
        Assert(pressBody.Center.Y < intakeStartY - 8f,
            $"bay three intake lift did not visibly raise its blob ({intakeStartY:0.0} -> {pressBody.Center.Y:0.0})");
        Assert(pressBody.Center.Y > line.DrumCenter.Y + 5f,
            "bay three intake still teleported its blob directly to the drum center");
        using (var loadingRender = new Bitmap(1280, 720))
        using (var loadingGraphics = Graphics.FromImage(loadingRender))
        {
            new GameRenderer().Draw(loadingGraphics, loadingRender.Size, world, null);
            var visibleForegroundPixels = 0;
            var lowerOpeningY = (int)MathF.Ceiling(line.DrumCenter.Y + ProcessingLine.DrumInteriorRadius);
            for (var y = lowerOpeningY; y <= (int)line.DeckY + 4; y++)
            for (var x = (int)(line.DrumCenter.X - 48f); x <= (int)(line.DrumCenter.X + 48f); x++)
            {
                var pixel = loadingRender.GetPixel(x, y);
                if (pixel.R >= 55 && pixel.R <= 190 && pixel.G > 145 && pixel.B > 120)
                    visibleForegroundPixels++;
            }
            Assert(visibleForegroundPixels >= 5,
                $"loading blob was clipped behind Bay 3 before entering the drum " +
                $"({visibleForegroundPixels} visible foreground pixels)");
        }
        while (line.DrumLoading && loadFrames < 170)
        {
            world.Step(Dt);
            loadFrames++;
        }
        Assert(!line.DrumLoading && loadFrames >= 110 && loadFrames <= 145,
            $"bay three intake was not a deliberate visible lift cycle ({loadFrames} frames)");
        Assert(Vector2.Distance(pressBody.Center, line.DrumCenter) <= 8f,
            "bay three lift failed to place its blob inside the drum");
        world.Gravity = new Vector2(0f, 980f);
        // Put the captured unit against the upper wall with no crank input. A real
        // drum must let gravity tumble it back to the bottom instead of pinning its
        // center to a scripted rotor angle.
        var freeTravel = MathF.Max(2f, ProcessingLine.DrumInteriorRadius - pressBody.Radius - 2f);
        var upperTarget = line.DrumCenter - Vector2.UnitY * freeTravel;
        pressBody.ApplyTranslation(upperTarget - pressBody.Center, preserveVelocity: true);
        pressBody.AddImpulse(-pressBody.AverageVelocity(Dt), Dt);
        var upperY = pressBody.Center.Y;
        for (var i = 0; i < 90; i++) world.Step(Dt);
        Assert(pressBody.Center.Y > upperY + freeTravel * 0.85f,
            $"unpowered drum held the blob above its gravity-resting position " +
            $"({upperY:0.0} -> {pressBody.Center.Y:0.0})");
        Assert(pressBody.Particles.Where((_, index) => pressBody.IsPhysicalParticle(index)).All(particle =>
                Vector2.Distance(particle.Position, line.DrumCenter) + particle.Radius <=
                ProcessingLine.DrumInteriorRadius + 1.2f),
            "gravity-driven drum allowed tissue through its closed circular wall");
        Assert(line.BeginDrumWheelDrag(line.DrumWheelCenter + new Vector2(20f, 0f)),
            "bay three hand wheel refused a valid grab");
        line.DragDrumWheel(line.DrumWheelCenter +
                           new Vector2(MathF.Cos(0.06f), MathF.Sin(0.06f)) * 20f);
        for (var i = 0; i < 8; i++) world.Step(Dt);
        Assert(MathF.Abs(line.DrumAngularSpeed) >= 10f,
            $"bay three base drum RPM still ramped too slowly ({line.DrumAngularSpeed:0.0} rad/s)");
        var peakDrumSpeed = MathF.Abs(line.DrumAngularSpeed);
        var drumFrames = 0;
        for (var i = 1; i <= 360 && line.DrumLockedBody is not null; i++)
        {
            var angle = i * MathF.Tau / 24f;
            line.DragDrumWheel(line.DrumWheelCenter +
                               new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 20f);
            world.Step(Dt);
            drumFrames++;
            peakDrumSpeed = MathF.Max(peakDrumSpeed, MathF.Abs(line.DrumAngularSpeed));
            if (i == 36)
            {
                using var drumRender = new Bitmap(1280, 720);
                using var drumGraphics = Graphics.FromImage(drumRender);
                new GameRenderer().Draw(drumGraphics, drumRender.Size, world, null);
                var mintBlobPixels = 0;
                var center = line.DrumCenter;
                for (var y = (int)(center.Y - 38f); y <= (int)(center.Y + 38f); y++)
                for (var x = (int)(center.X - 38f); x <= (int)(center.X + 38f); x++)
                {
                    var pixel = drumRender.GetPixel(x, y);
                    if (pixel.R >= 55 && pixel.R <= 190 && pixel.G > 145 && pixel.B > 120)
                        mintBlobPixels++;
                }
                Assert(mintBlobPixels >= 30,
                    $"spinning blob was not visibly rendered inside the drum ({mintBlobPixels} mint pixels)");
            }
            if (i == 90)
                Assert(line.DrumProgress < 0.70f,
                    $"bay three still completed with too few rotations ({line.DrumProgress:P0} after 90 frames)");
        }
        line.EndDrumWheelDrag();
        for (var i = 0; i < 120 && line.DrumLockedBody is not null; i++)
        {
            world.Step(Dt);
            drumFrames++;
        }
        Assert(line.PressLockedBody is null && line.PressTravel < 0.01f,
            "drum failed to align, open, and release");
        Assert(pressBody.Center.X >= line.Belts[3].Position.X + 28f &&
               pressBody.Center.Y + pressBody.Radius <= line.DeckY + 2f,
            $"drum released its blob into the drain chase instead of the outgoing belt " +
            $"({pressBody.Center.X:0.0},{pressBody.Center.Y:0.0})");
        Assert(!pressBody.Particles.Where((_, index) => pressBody.IsPhysicalParticle(index)).Any(particle =>
                MathF.Abs(particle.Position.X - line.Bays[2].CenterX) < 18f &&
                particle.Position.Y > line.DeckY + 4f),
            "drum discharge left tissue embedded inside the Bay 3 drain pipe");
        Assert(drumFrames < 330,
            $"drum interaction remained too long after sustained wheel input ({drumFrames} frames)");
        Assert(peakDrumSpeed > 5f, "circular hand-wheel input did not drive drum speed");
        Assert(line.PressBrokenLinks > 0, "completed drum cycle caused no structural damage");

        var vacuumBody = MoveToBay(3);
        Assert(ReferenceEquals(line.VacuumLockedBody, vacuumBody), "bay four failed to capture the pressed unit");
        for (var i = 0; i < 45; i++) world.Step(Dt);
        Assert(line.BeginVacuumDrag(line.VacuumHose.NozzlePosition),
            "vacuum nozzle refused a valid grab while a unit was locked");
        for (var i = 0; i < 18; i++) world.Step(Dt);
        var desiredFacing = Vector2.Normalize(vacuumBody.Center - line.VacuumHose.NozzlePosition);
        Assert(Vector2.Dot(line.VacuumHose.NozzleFacing, desiredFacing) > 0.94f,
            "grabbed vacuum nozzle did not automatically turn toward the locked blob");
        var sawVacuumFlow = false;
        for (var i = 0; i < 360 && line.VacuumLockedBody is not null; i++)
        {
            line.DragVacuumNozzle(vacuumBody.Center);
            world.Step(Dt);
            sawVacuumFlow |= line.VacuumContact && line.VacuumFlowPhase > 0f;
        }
        Assert(line.VacuumLockedBody is null,
            "completed extraction failed to release the unit immediately");
        Assert(sawVacuumFlow, "active vacuum extraction never advanced the visible hose-lump flow phase");
        line.EndVacuumDrag(vacuumBody.Center);
        for (var i = 0; i < 180; i++) world.Step(Dt);
        Assert(Vector2.Distance(line.VacuumHose.NozzlePosition, line.VacuumNozzleRest) < 18f,
            "released vacuum nozzle failed to settle back into its rack");
        Assert(Vector2.Dot(line.VacuumHose.NozzleFacing, -Vector2.UnitY) > 0.96f,
            "holstered vacuum nozzle was not vertically flipped upward");
        Assert(line.VacuumExtractedLinks > 0, "vacuum contact extracted no local structure");
        Assert(line.VacuumDrainRemaining <= 0.05f,
            "vacuum drain kept pouring after its processed blob had left Bay 4");

        var filterBody = MoveToBay(4);
        var filterEntryParticles = filterBody.PhysicalParticleCount;
        Assert(ReferenceEquals(line.FilterLockedBody, filterBody), "bay five failed to capture the pumped unit");
        Assert(line.FilterLaserActive, "filter lasers were not energized when the unit entered bay five");
        Assert(line.BeginFilterDrag(line.FilterKnobCenter), "filter knob refused a valid drag start");
        // A single sparse mouse event spanning the full rail must be enough. Gameplay
        // cannot depend on how many intermediate WM_MOUSEMOVE messages Windows emits.
        line.DragFilterKnob(line.Bays[4].CenterX - 34f);
        Assert(!line.FilterLaserActive, "filter lasers stayed on after the completed cutting pass");
        line.EndFilterDrag();
        Assert(!line.FilterReturning && line.FilterLockedBody is null,
            "completed filter pass did not release and reset immediately");
        world.Step(Dt);
        Assert(line.FilterBrokenLinks > 0, "laser filter traversal produced no structural cuts");
        Assert(line.FilterKnob > 0.99f, "filter handle did not reset to its right-hand start");
        Assert(!line.BeginFilterDrag(line.FilterKnobCenter),
            "completed blob could start a second laser pass");
        Assert(world.Granular.Particles.Count(particle => particle.Kind == GranularKind.Tissue) >= 8,
            "final machine did not guarantee a small residue load for the output cart");

        for (var i = 0; i < 30; i++) world.Step(Dt);
        var survivingRemnant = world.Bodies
            .Where(body => body.ParentId == parentId && body.PhysicalParticleCount > 0)
            .OrderByDescending(body => body.PhysicalParticleCount)
            .FirstOrDefault();
        var requiredRemnant = Math.Min(
            ProcessingLine.FilterProtectedRemnantParticles,
            filterEntryParticles);
        Assert(survivingRemnant is not null && survivingRemnant.PhysicalParticleCount >= requiredRemnant,
            $"Bay 5 destroyed the protected final remnant " +
            $"({survivingRemnant?.PhysicalParticleCount ?? 0}/{requiredRemnant} particles survived)");

        var cart = line.OutputCartBounds;
        bool RemnantReachedCart() => world.Bodies.Any(body =>
            body.ParentId == parentId && body.PhysicalParticleCount >= requiredRemnant &&
            body.Center.X >= cart.Left + 5f && body.Center.X <= cart.Right - 5f &&
            body.Center.Y >= cart.Top - 38f);
        for (var i = 0; i < 720 && !RemnantReachedCart(); i++) world.Step(Dt);
        Assert(RemnantReachedCart(),
            "the protected Bay 5 remnant failed to travel into the output cart");

    }

    private static void AudioSettingsPersistBusVolumes()
    {
        var root = Path.Combine(Path.GetTempPath(), $"BlobForge-audio-bus-{Guid.NewGuid():N}");
        try
        {
            using (var mixer = new SoundEffectMixer(root))
            {
                mixer.MasterVolume = 73;
                mixer.SfxVolume = 41;
                mixer.MusicVolume = 26;
                Assert(mixer.Get(SoundCue.Drill).Bus == AudioBus.Sfx,
                    "machinery audio was not routed through the SFX bus");
                Assert(mixer.Get(SoundCue.Music).Bus == AudioBus.Music,
                    "reserved future music channel was not routed through the Music bus");
                var expectedSfx = 0.73f * 0.41f * mixer.Get(SoundCue.Drill).Volume / 100f;
                Assert(MathF.Abs(mixer.EffectiveVolume(SoundCue.Drill) - expectedSfx) < 0.0001f,
                    "SFX bus volume was not included in machinery playback level");
                Assert(MathF.Abs(mixer.EffectiveVolume(SoundCue.Music) - 0.73f * 0.26f) < 0.0001f,
                    "Music bus volume was not included in future music playback level");
            }

            using var reloaded = new SoundEffectMixer(root);
            Assert(reloaded.MasterVolume == 73 && reloaded.SfxVolume == 41 && reloaded.MusicVolume == 26,
                "master, SFX, and music slider values did not persist");
        }
        finally
        {
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
            catch
            {
                // A diagnostic cleanup failure must not mask an actual mixer assertion.
            }
        }
    }

    private static void MachineDrainsFeedBasin()
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        var line = new ProcessingLine(DestructibleGrid.ProcessingDeckRow * grid.CellSize);
        var world = new BlobWorld(grid) { ProcessingLine = line };
        world.Conveyors.AddRange(line.Belts);

        for (var bayIndex = 0; bayIndex < line.Bays.Count; bayIndex++)
        {
            var position = new Vector2(line.Bays[bayIndex].CenterX, line.DeckY - 38f);
            for (var i = 0; i < 8; i++)
            {
                world.Granular.Particles.Add(new GranularParticle
                {
                    Position = position + new Vector2((i % 3 - 1) * 2f, -i * 1.5f),
                    PreviousPosition = position - new Vector2(0f, 3f),
                    Radius = 2.1f,
                    Lifetime = 30f,
                    Kind = i % 5 == 0 ? GranularKind.Tissue : GranularKind.Blood
                });
            }
        }

        for (var i = 0; i < 480; i++) world.Step(Dt);
        Assert(line.Basin.TotalDeposited > 12f,
            $"drain pipes failed to deliver loose material to the basin ({line.Basin.TotalDeposited:0.0})");
        Assert(line.Basin.Heights.Any(height => height > 0.08f),
            "basin received material without producing a fluid surface");
        Assert(line.Basin.FluidLevel01 > 0f && line.Basin.FluidLevel01 < 0.05f,
            $"small pipe inflow filled the basin too quickly ({line.Basin.FluidLevel01:P1})");
        var renderedVolume = line.Basin.Heights.Sum() * (line.Basin.Width / BloodBasin.ColumnCount);
        Assert(MathF.Abs(renderedVolume - line.Basin.CurrentFluidVolume) < 0.5f,
            $"basin surface disagreed with its volume gauge ({renderedVolume:0.0} vs {line.Basin.CurrentFluidVolume:0.0})");
        Assert(line.Basin.TotalConsumedFluid == 0f &&
               MathF.Abs(line.Basin.CurrentFluidVolume + line.Basin.PendingFluidVolume -
                         line.Basin.TotalDeposited) < 0.02f,
            "dormant Diego consumed material delivered through the machine drains");
        Assert(line.Basin.PipeStains.Count > 0 && line.Bays.Any(bay =>
                line.Basin.PipeStainNear(bay.CenterX) is not null),
            "machine inflow did not leave blood coating on an interior drain outlet");
    }

    private static void BasinInflowStaysContained()
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        var line = new ProcessingLine(DestructibleGrid.ProcessingDeckRow * grid.CellSize);
        var world = new BlobWorld(grid) { ProcessingLine = line, Gravity = Vector2.Zero };
        world.Conveyors.AddRange(line.Belts);
        var x = line.Basin.Left + line.Basin.Width * 0.52f;
        line.Basin.AddMaterial(x, line.Basin.FluidCapacity * 0.18f, 0f, 0f);
        var start = new Vector2(x, line.Basin.SurfaceYAt(x) - 24f);
        world.Granular.Particles.Add(new GranularParticle
        {
            Position = start,
            PreviousPosition = start - new Vector2(0f, 108f),
            Radius = 2.4f,
            Lifetime = 10f,
            Kind = GranularKind.Blood,
            SplatterOnImpact = true
        });

        for (var step = 0; step < 4; step++) world.Step(Dt);
        Assert(world.Granular.Particles.All(particle => particle.Kind != GranularKind.Blood),
            "fast basin inflow survived long enough to cross the tank");
        Assert(line.Basin.SuspendedDrops.Count > 0 && line.Basin.PendingFluidVolume > 0f &&
               line.Basin.CurrentFluidVolume < line.Basin.TotalDeposited,
            "basin inflow vanished immediately instead of floating and dissolving");
        Assert(line.Basin.TotalDeposited > 0f && line.Basin.SloshAmplitude > 0.08f,
            "contained inflow did not add fluid and a visible slosh impulse");
        Assert(line.Basin.SurfaceSplashes.Count >= 2 && line.Basin.SurfaceRipples.Count > 0,
            "blood entering an existing pool produced no surface splash or expanding ripple");
        Assert(line.Basin.InteriorStains.Count > 0 && line.Basin.InteriorStains.All(stain =>
                stain.X >= line.Basin.Left + 4f && stain.X <= line.Basin.Right - 4f &&
                stain.Y >= line.Basin.Top + 5f && stain.Y <= line.Basin.Bottom - 7f),
            "basin splash marks were not kept on the interior glass/sides");
        Assert(!grid.BloodStains.Any(mark =>
                line.IsBasinProtectedFloor(mark.Position)),
            "contained basin blood painted the structural floor below the tank");

        var initialSlosh = line.Basin.SloshAmplitude;
        for (var step = 0; step < 720; step++) world.Step(Dt);
        Assert(line.Basin.SloshAmplitude < initialSlosh * 0.12f,
            "basin slosh did not damp after the inflow settled");
    }

    private static void FullBasinKeepsDrainOpenAndSpillsExcess()
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        var line = new ProcessingLine(DestructibleGrid.ProcessingDeckRow * grid.CellSize);
        var world = new BlobWorld(grid) { ProcessingLine = line };
        world.Conveyors.AddRange(line.Belts);
        var basin = line.Basin;
        var centerX = basin.Left + basin.Width * 0.5f;

        basin.AddMaterial(centerX, basin.FluidCapacity * 2f, 180f, 0f);
        // This is the exact former crash path: once rounding put the stored total
        // microscopically above capacity, Math.Clamp received a negative maximum.
        for (var i = 0; i < 128; i++)
            basin.AddSuspendedMaterial(centerX, basin.Top + 12f, 25f, 210f, 0f, 2.5f);
        Assert(basin.IsFull && basin.StoredVolume <= basin.FluidCapacity + 0.001f,
            $"full basin escaped its capacity invariant ({basin.StoredVolume} / {basin.FluidCapacity})");

        world.Step(Dt);
        Assert(line.BasinAtCapacity && !line.MachineryLockedByStorage,
            "100% storage did not preserve normal factory operation while freezing reserve growth");
        Assert(line.Belts.All(belt => MathF.Abs(belt.Speed - ProcessingLine.OperatingSpeed) < 0.001f),
            "100% storage stopped a processing-line conveyor");
        Assert(!Enumerable.Range(0, line.Bays.Count).All(line.IsBayInUse),
            "100% storage falsely put every machine into a locked/busy state");

        var itemBounds = line.BloodShopItemBounds(0);
        var itemPoint = new Vector2(itemBounds.Left + itemBounds.Width * 0.5f,
            itemBounds.Top + itemBounds.Height * 0.5f);
        var beforePurchase = basin.SpendableBlood;
        Assert(line.TryActivateBloodShop(itemPoint) && line.BloodShopItems[0].Purchased,
            "blood exchange did not purchase an affordable upgrade socket");
        Assert(MathF.Abs(beforePurchase - basin.SpendableBlood - line.BloodShopItems[0].Cost) < 0.02f,
            "upgrade socket price was not deducted from the conserved basin value");
        Assert(!line.BasinAtCapacity, "spending below capacity did not clear the basin capacity state");

        world.Step(Dt);
        Assert(line.Belts.All(belt => MathF.Abs(belt.Speed - ProcessingLine.OperatingSpeed) < 0.001f),
            "processing conveyors did not restart after the basin dropped below 100%");
        var relief = line.BloodShopReliefBounds;
        var reliefPoint = new Vector2(relief.Left + relief.Width * 0.5f, relief.Top + relief.Height * 0.5f);
        var beforeRelief = basin.SpendableBlood;
        Assert(line.TryActivateBloodShop(reliefPoint) &&
               MathF.Abs(beforeRelief - basin.SpendableBlood - ProcessingLine.ReliefValveCost) < 0.02f,
            "repeatable purge control did not spend its displayed basin price");

        var continuousGrid = new DestructibleGrid(40, 22, 32);
        continuousGrid.BuildProcessingStation();
        continuousGrid.OpenContinuousConveyorPortals();
        var continuousLine = new ProcessingLine(
            DestructibleGrid.ProcessingDeckRow * continuousGrid.CellSize,
            continuousFlow: true);
        continuousLine.Basin.AddMaterial(
            continuousLine.Basin.Left + continuousLine.Basin.Width * 0.5f,
            continuousLine.Basin.FluidCapacity,
            180f,
            0f);
        Assert(continuousLine.BasinAtCapacity,
            "continuous-flow basin did not report its full reserve state");

        var collectorParticle = new GranularParticle
        {
            Position = new Vector2(
                continuousLine.ContinuousDrainCollectorBounds.Left + 20f,
                continuousLine.ContinuousDrainCollectorBounds.Top + 4f),
            PreviousPosition = new Vector2(
                continuousLine.ContinuousDrainCollectorBounds.Left + 17f,
                continuousLine.ContinuousDrainCollectorBounds.Top + 4f),
            Radius = 2f,
            Lifetime = 10f,
            Kind = GranularKind.Blood
        };
        var collectorBefore = collectorParticle.Position;
        Assert(continuousLine.RouteThroughContinuousEndDrain(ref collectorParticle, Dt) &&
               Vector2.DistanceSquared(collectorParticle.Position, collectorBefore) > 0.001f,
            "full basin incorrectly closed the physical collector and stopped funnel travel");
        var enteredBasinDrop = false;
        for (var step = 0; step < 420; step++)
        {
            continuousLine.RouteThroughContinuousEndDrain(ref collectorParticle, Dt);
            enteredBasinDrop |=
                collectorParticle.Position.Y >= continuousLine.ContinuousDrainBasinMouth.Y - 5f &&
                MathF.Abs(collectorParticle.Position.X - continuousLine.ContinuousDrainBasinMouth.X) <= 18f;
            if (enteredBasinDrop) break;
        }
        Assert(enteredBasinDrop,
            "blood accepted by the full-basin collector never traversed the normal pipe to its mouth");

        var denseDrain = new GranularMaterialSystem();
        for (var index = 0; index < 180; index++)
        {
            var x = continuousLine.ContinuousDrainCollectorBounds.Left + 5f + index % 10 * 3.2f;
            var y = continuousLine.ContinuousDrainCollectorBounds.Top + 2f + index / 10 * 0.4f;
            denseDrain.Particles.Add(new GranularParticle
            {
                Position = new Vector2(x, y),
                PreviousPosition = new Vector2(x - 2f, y),
                Radius = 2.1f,
                Lifetime = 30f,
                Kind = index % 4 == 0 ? GranularKind.Tissue : GranularKind.Blood
            });
        }
        var noBodies = new List<SoftBody>();
        for (var step = 0; step < 720; step++)
        {
            denseDrain.BeginStep();
            denseDrain.Step(1f / 60f, new Vector2(0f, 980f), continuousGrid,
                noBodies, continuousLine.Belts, processingLine: continuousLine);
        }
        Assert(denseDrain.Particles.All(particle =>
                !continuousLine.ContinuousDrainCollectorBounds.Contains(
                    particle.Position.X, particle.Position.Y) &&
                !particle.InContinuousDrain),
            "dense full-basin inflow rebuilt a collision plug in the funnel or pipe");

        var spillParticle = new GranularParticle
        {
            Position = new Vector2(
                continuousLine.Basin.Left + continuousLine.Basin.Width * 0.72f,
                continuousLine.Basin.FluidTop + 2f),
            PreviousPosition = new Vector2(
                continuousLine.Basin.Left + continuousLine.Basin.Width * 0.72f,
                continuousLine.Basin.FluidTop - 4f),
            Radius = 2.4f,
            Lifetime = 10f,
            Kind = GranularKind.Blood
        };
        var storedBeforeSpill = continuousLine.Basin.StoredVolume;
        Assert(!continuousLine.TryCollectBasinInflow(ref spillParticle, Dt),
            "blood landing on a full basin was incorrectly counted and consumed");
        Assert(MathF.Abs(continuousLine.Basin.StoredVolume - storedBeforeSpill) < 0.001f &&
               continuousLine.Basin.TotalOverflowed > 0f,
            "full-basin overflow changed stored/spendable blood");
        var firstSpilledRight =
            spillParticle.Position.X > continuousLine.Basin.Right + 18f;
        var firstSpilledLeft =
            spillParticle.Position.X < continuousLine.Basin.Left - 18f;
        Assert((firstSpilledRight || firstSpilledLeft) &&
               spillParticle.Position.Y <= continuousLine.Basin.Top + 6f &&
               (firstSpilledRight
                   ? spillParticle.Position.X > spillParticle.PreviousPosition.X
                   : spillParticle.Position.X < spillParticle.PreviousPosition.X),
            "excess blood was not physically displaced over the nearest basin lip");
        Assert(continuousLine.Basin.SurfaceSplashes.Count > 0,
            "overflow impact produced no visible disturbance on the full pool");

        var oppositeSpill = spillParticle with
        {
            Position = new Vector2(
                continuousLine.Basin.Left + continuousLine.Basin.Width * 0.72f,
                continuousLine.Basin.FluidTop + 2f),
            PreviousPosition = new Vector2(
                continuousLine.Basin.Left + continuousLine.Basin.Width * 0.72f,
                continuousLine.Basin.FluidTop - 4f)
        };
        Assert(!continuousLine.TryCollectBasinInflow(ref oppositeSpill, Dt) &&
               (firstSpilledRight
                   ? oppositeSpill.Position.X < continuousLine.Basin.Left - 18f &&
                     oppositeSpill.Position.X < oppositeSpill.PreviousPosition.X
                   : oppositeSpill.Position.X > continuousLine.Basin.Right + 18f &&
                     oppositeSpill.Position.X > oppositeSpill.PreviousPosition.X),
            "successive full-basin overflow did not alternate to the opposite lip");
        var spillLeft = 0;
        var spillRight = 0;
        for (var impact = 0; impact < 24; impact++)
        {
            if (continuousLine.Basin.RegisterOverflowImpact(
                    continuousLine.Basin.Left + continuousLine.Basin.Width * 0.72f,
                    240f + impact * 7f, 2.4f, 18f))
                spillRight++;
            else
                spillLeft++;
        }
        Assert(spillLeft == spillRight,
            $"full-basin overflow was not evenly distributed ({spillLeft} left/{spillRight} right)");
        Assert(continuousLine.Basin.FrontOverflowStains.Count == 0,
            "full basin still created the disabled front-glass overflow trails");
    }

    private static void BloodWorkerAutomatesMachinery()
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        var line = new ProcessingLine(DestructibleGrid.ProcessingDeckRow * grid.CellSize);
        var world = new BlobWorld(grid) { ProcessingLine = line };
        world.Conveyors.AddRange(line.Belts);
        line.Basin.AddMaterial(line.Basin.Left + 20f, ProcessingLine.FactoryWorkerCost + 500f,
            180f, 0f);

        var itemBounds = line.BloodShopItemBounds(0);
        var purchasePoint = new Vector2(itemBounds.Left + itemBounds.Width * 0.5f,
            itemBounds.Top + itemBounds.Height * 0.5f);
        Assert(line.TryActivateBloodShop(purchasePoint) && line.FactoryWorkers.Count == 1,
            "worker purchase did not spawn one forming worker at the basin outlet");
        Assert(line.BloodShopItems[0].PurchaseCount == 1 && line.BloodShopItems[0].CanPurchase,
            "repeatable worker shop item did not retain its bounded crew capacity");

        var body = BlobArchetype.ProcessingUnit.Create(
            new Vector2(line.Bays[0].CenterX, line.DeckY - 30f));
        world.Bodies.Add(body);
        var sawClimb = false;
        var sawWalk = false;
        var sawOperate = false;
        for (var step = 0; step < 2_100 && !line.HasEnteredBayOne(body); step++) world.Step(Dt);
        for (var step = 0; step < 2_100 && line.LockedBody is not null; step++)
        {
            world.Step(Dt);
            var activity = line.FactoryWorkers[0].Activity;
            sawClimb |= activity is FactoryWorkerActivity.Climbing or FactoryWorkerActivity.Descending;
            sawWalk |= activity == FactoryWorkerActivity.Walking;
            sawOperate |= activity == FactoryWorkerActivity.Operating &&
                          line.FactoryWorkers[0].AssignedBay == 0;
        }

        Assert(sawClimb && sawWalk && sawOperate,
            $"worker route skipped required scaffold animation states " +
            $"(climb {sawClimb}, walk {sawWalk}, operate {sawOperate})");
        world.Step(Dt);
        Assert(line.LockedBody is null && line.FactoryWorkers[0].Activity == FactoryWorkerActivity.Ascending,
            $"worker failed to hold and release the crusher's real button cycle " +
            $"(locked {line.LockedBody is not null}, activity {line.FactoryWorkers[0].Activity})");

        bool BayHasBody(int bay) => bay switch
        {
            1 => line.DrillLockedBody is not null,
            2 => line.DrumLockedBody is not null,
            3 => line.VacuumLockedBody is not null,
            4 => line.FilterLockedBody is not null,
            _ => false
        };
        SoftBody MoveParentToBay(int bay)
        {
            for (var step = 0; step < 1_200 &&
                               line.FactoryWorkers[0].Activity != FactoryWorkerActivity.Idle; step++)
                world.Step(Dt);
            var current = world.Bodies.Where(candidate => candidate.ParentId == body.ParentId)
                .OrderByDescending(candidate => candidate.PhysicalParticleCount)
                .First();
            current.ApplyTranslation(new Vector2(line.Bays[bay].CenterX - current.Center.X,
                line.DeckY - 30f - current.Center.Y), preserveVelocity: true);
            current.AddImpulse(-current.AverageVelocity(Dt), Dt);
            world.Step(Dt);
            Assert(BayHasBody(bay), $"processed worker-test blob did not capture in Bay {bay + 1}");
            return current;
        }
        void CompleteWorkerBay(int bay, int maximumSteps)
        {
            var sawBayOperation = false;
            for (var step = 0; step < maximumSteps && BayHasBody(bay); step++)
            {
                world.Step(Dt);
                var worker = line.FactoryWorkers[0];
                sawBayOperation |= worker.Activity == FactoryWorkerActivity.Operating &&
                                   worker.AssignedBay == bay;
            }
            Assert(sawBayOperation && !BayHasBody(bay),
                $"worker failed to operate and release Bay {bay + 1} " +
                $"(operated {sawBayOperation}, occupied {BayHasBody(bay)})");
        }

        MoveParentToBay(1);
        CompleteWorkerBay(1, 2_400);
        MoveParentToBay(2);
        CompleteWorkerBay(2, 3_600);
        MoveParentToBay(3);
        CompleteWorkerBay(3, 2_400);
        MoveParentToBay(4);
        CompleteWorkerBay(4, 2_400);

        var workerSprite = Path.Combine(AppContext.BaseDirectory, "Assets", "FactoryWorker.png");
        var infrastructure = Path.Combine(AppContext.BaseDirectory, "Assets", "WorkerInfrastructure.png");
        using var workerBitmap = new Bitmap(workerSprite);
        using var infrastructureBitmap = new Bitmap(infrastructure);
        Assert(workerBitmap.Width == 24 * 22 && workerBitmap.Height == 32,
            "worker spritesheet dimensions do not match the 22-frame runtime contract");
        Assert(infrastructureBitmap.Width == 128 && infrastructureBitmap.Height == 64,
            "worker infrastructure atlas dimensions do not match its runtime contract");
    }

    private static void BasinFluidIsCellularAndConserved()
    {
        var basin = new BloodBasin(120f, 240f, 866f, 101f);
        var depositX = basin.Left + basin.Width * 0.5f;
        basin.AddMaterial(depositX, 500f, downwardSpeed: 85f, nutrition: 0f);

        float RepresentedVolume()
        {
            var fills = 0f;
            for (var y = 0; y < BloodBasin.FluidGridHeight; y++)
            for (var x = 0; x < BloodBasin.FluidGridWidth; x++)
                fills += basin.FluidFillAt(x, y);
            return fills * basin.FluidCellVolume;
        }

        int OccupiedColumns()
        {
            var occupied = 0;
            for (var x = 0; x < BloodBasin.FluidGridWidth; x++)
            {
                var hasFluid = false;
                for (var y = 0; y < BloodBasin.FluidGridHeight; y++)
                    hasFluid |= basin.FluidFillAt(x, y) > 0f;
                if (hasFluid) occupied++;
            }
            return occupied;
        }

        var initialWidth = OccupiedColumns();
        Assert(MathF.Abs(RepresentedVolume() - basin.CurrentFluidVolume) < 0.02f,
            "cellular basin did not represent the authoritative deposited volume");
        for (var step = 0; step < 600; step++) basin.Step(Dt);
        var settledWidth = OccupiedColumns();
        Assert(settledWidth > initialWidth * 1.55f,
            $"localized basin deposit did not spread like cellular liquid ({initialWidth} -> {settledWidth} columns)");
        var occupiedColumns = Enumerable.Range(0, BloodBasin.FluidGridWidth)
            .Where(x => Enumerable.Range(0, BloodBasin.FluidGridHeight)
                .Any(y => basin.FluidFillAt(x, y) > 0f))
            .ToArray();
        Assert(occupiedColumns[^1] - occupiedColumns[0] + 1 == occupiedColumns.Length,
            "shallow basin liquid settled into disconnected grains instead of one puddle");
        Assert(!basin.FluidIsActive, "settled basin fluid continued scanning indefinitely");
        Assert(MathF.Abs(RepresentedVolume() - basin.CurrentFluidVolume) < 0.02f,
            "cellular flow changed basin mass while settling");

        var stableRevision = basin.FluidVisualRevision;
        for (var step = 0; step < 120; step++) basin.Step(Dt);
        Assert(basin.FluidVisualRevision == stableRevision,
            "sleeping basin fluid kept invalidating its cached raster");

        var deepBasin = new BloodBasin(120f, 240f, 866f, 101f);
        deepBasin.AddMaterial(depositX, deepBasin.FluidCapacity * 0.32f,
            downwardSpeed: 85f, nutrition: 0f);
        for (var step = 0; step < 30; step++) deepBasin.Step(Dt);
        var columnFills = Enumerable.Range(0, BloodBasin.FluidGridWidth)
            .Select(x => Enumerable.Range(0, BloodBasin.FluidGridHeight)
                .Sum(y => deepBasin.FluidFillAt(x, y)))
            .ToArray();
        Assert(columnFills.Min() > 0f,
            "a substantial liquid deposit did not wet the complete basin floor");
        Assert(columnFills.Max() - columnFills.Min() <= 1.001f,
            $"basin pressure left sand-like hills ({columnFills.Min():0.##} to {columnFills.Max():0.##} cells)");
        var highColumn = columnFills.Min() + 0.5f;
        var topBandTransitions = 0;
        for (var x = 0; x < BloodBasin.FluidGridWidth; x++)
        {
            var next = (x + 1) % BloodBasin.FluidGridWidth;
            if ((columnFills[x] > highColumn) != (columnFills[next] > highColumn))
                topBandTransitions++;
        }
        Assert(topBandTransitions is >= 2 and <= 24,
            $"basin top row retained a repeated sawtooth distribution ({topBandTransitions} transitions)");

        var visualDepths = Enumerable.Range(0, BloodBasin.FluidGridWidth)
            .Select(deepBasin.VisualFluidDepthAt)
            .ToArray();
        var largestVisualStep = Enumerable.Range(0, BloodBasin.FluidGridWidth)
            .Max(x => MathF.Abs(visualDepths[x] - visualDepths[(x + 1) % BloodBasin.FluidGridWidth]));
        Assert(visualDepths.Max() - visualDepths.Min() >= 0.25f,
            "settled basin surface lost all subtle wave variation");
        Assert(largestVisualStep <= 0.22f,
            $"settled basin surface still had a tooth-like edge ({largestVisualStep:0.###} cells)");
        Assert(!deepBasin.FluidIsActive,
            "hydrostatically level basin liquid did not return to sleep");

        var volumeBeforeDormancyCheck = basin.CurrentFluidVolume;
        basin.AddMaterial(depositX, 220f, downwardSpeed: 40f, nutrition: 12f);
        for (var step = 0; step < 300; step++) basin.Step(Dt);
        Assert(MathF.Abs(basin.CurrentFluidVolume - (volumeBeforeDormancyCheck + 220f)) < 0.02f &&
               basin.TotalConsumedFluid == 0f,
            "dormant Diego removed volume from the cellular basin");
        Assert(MathF.Abs(RepresentedVolume() - basin.CurrentFluidVolume) < 0.02f,
            "cell raster diverged from authoritative volume during dormant-creature simulation");
    }

    private static void BasinVolumeAndPayoutAreCalibrated()
    {
        var basin = new BloodBasin(250f, 571f, 866f, 101f);
        var expectedCapacityLiters =
            866f / BloodBasin.WorldUnitsPerMeter *
            (101f - 12f) / BloodBasin.WorldUnitsPerMeter *
            BloodBasin.EstimatedTankDepthMeters *
            BloodBasin.LitersPerCubicMeter;
        Assert(MathF.Abs(basin.EstimatedCapacityLiters - expectedCapacityLiters) < 0.1f,
            "basin capacity did not follow its authored physical dimensions");
        basin.AddMaterial(
            basin.Left + basin.Width * 0.5f,
            basin.FluidCapacity * 0.5f,
            0f,
            0f);
        Assert(MathF.Abs(basin.EstimatedStoredLiters - expectedCapacityLiters * 0.5f) < 0.1f &&
               MathF.Abs(
                   basin.EstimatedStoredGallons * BloodBasin.LitersPerUsGallon -
                   basin.EstimatedStoredLiters) < 0.1f,
            "gallons and liters did not describe the same conserved blood");

        var progression = GameProgression.CreateTransient();
        progression.ToggleVolumeUnit();
        Assert(progression.VolumeUnit == BasinVolumeUnit.Liters,
            "volume preference did not toggle to liters");
        var expectedBloodPayout = decimal.Round(
            (decimal)basin.EstimatedStoredGallons * GameProgression.BaseBloodRatePerGallon,
            2,
            MidpointRounding.AwayFromZero);
        var payout = progression.CompleteDay(basin, processedBlobs: 3);
        Assert(payout.BloodPayout == expectedBloodPayout &&
               payout.ProcessedPayout == 3m * GameProgression.BaseProcessedBlobRate &&
               payout.TotalPayout == payout.BloodPayout + payout.ProcessedPayout &&
               payout.CurrencyAfterSale == progression.Currency,
            "day payout did not itemize blood and damage-qualified processing correctly");
        Assert(basin.StoredVolume <= 0.001f && basin.FluidCellCount == 0,
            "selling the day did not empty the authoritative basin");
        Assert(progression.TryUnlockWeapon("NAIL_GUN") &&
               progression.IsWeaponUnlocked("NAIL_GUN"),
            "earned currency could not unlock a weapon");

        for (var day = 0; day < GameProgression.DaysPerYear; day++)
            progression.AdvanceDay();
        Assert(progression.Year == 2 && progression.DayOfYear == 1 &&
               progression.DayLabel().Contains("YEAR 2", StringComparison.Ordinal),
            "absolute day progression did not roll into year two");
    }

    private static void DayEndShipmentConservesBlood()
    {
        var basin = new BloodBasin(250f, 507f, 866f, 101f);
        basin.AddMaterial(
            basin.Left + basin.Width * 0.5f,
            basin.FluidCapacity * 0.64f,
            0f,
            0f);
        var initial = basin.StoredVolume;
        var sequence = new BloodShipmentSequence(
            basin,
            GameProgression.BaseBloodRatePerGallon,
            processedBlobs: 4,
            processedRate: GameProgression.BaseProcessedBlobRate);
        var maximumConservationError = 0f;
        var maximumDrainShelf = 0f;
        var previousDisplayedBlood = 0m;

        for (var step = 0; step < 60 * 120 && !sequence.Complete; step++)
        {
            sequence.Update(Dt);
            if (step % 20 == 0 && basin.StoredVolume < initial - 0.01f &&
                basin.FluidCellCount >= BloodBasin.FluidGridWidth)
            {
                var columnFills = Enumerable.Range(0, BloodBasin.FluidGridWidth)
                    .Select(x => Enumerable.Range(0, BloodBasin.FluidGridHeight)
                        .Sum(y => basin.FluidFillAt(x, y)))
                    .ToArray();
                maximumDrainShelf = MathF.Max(maximumDrainShelf,
                    columnFills.Max() - columnFills.Min());
            }
            var inFlight = 0f;
            for (var i = 0; i < sequence.Pixels.Count; i++)
                inFlight += sequence.Pixels[i].Volume;
            var represented = basin.StoredVolume + inFlight + sequence.ShippedVolume;
            maximumConservationError = MathF.Max(
                maximumConservationError,
                MathF.Abs(initial - represented));
            Assert(sequence.InFlightParticleCount <= BloodShipmentSequence.MaximumParticles,
                "day-end transfer escaped its bounded physical-pixel budget");
            if (sequence.Stage is not BloodShipmentStage.FinalizingPayout and
                not BloodShipmentStage.Complete)
            {
                Assert(sequence.DisplayedBloodEarnings + 0.02m >= previousDisplayedBlood &&
                       sequence.DisplayedBloodEarnings <= sequence.LoadedBloodPayout + 0.02m,
                    "live earnings stopped being a smooth function of blood received by trucks");
                previousDisplayedBlood = sequence.DisplayedBloodEarnings;
            }
        }

        Assert(sequence.Complete,
            "day-end blood shipment did not finish within its bounded sequence time " +
            $"(stage {sequence.Stage}, basin {basin.StoredVolume:0.00}, " +
            $"shipped {sequence.ShippedVolume:0.00}/{initial:0.00}, " +
            $"pixels {sequence.InFlightParticleCount}, trucks " +
            $"{sequence.DepartedTruckCount}/{sequence.PlannedTruckCount})");
        Assert(basin.StoredVolume <= 0.001f &&
               MathF.Abs(sequence.ShippedVolume - initial) <= 0.02f,
            "day-end trucks did not empty and receive the authoritative basin volume");
        Assert(sequence.DepartedTruckCount == sequence.PlannedTruckCount &&
               sequence.DepartedTruckCount is >= 1 and <= BloodShipmentSequence.MaximumTrucks,
            "day-end shipment did not dispatch the planned bounded truck batches");
        Assert(maximumConservationError <= MathF.Max(0.1f, initial * 0.00001f),
            $"day-end transfer created or lost blood while pixels were in flight " +
            $"({maximumConservationError:0.000} volume error)");
        Assert(maximumDrainShelf <= 1.001f,
            $"shipment extraction left end walls instead of draining as one pool " +
            $"({maximumDrainShelf:0.###} cell height spread)");
        Assert(sequence.DisplayedTotalEarnings == sequence.ProjectedTotalPayout &&
               sequence.ProjectedTotalPayout ==
               sequence.ProjectedBloodPayout + sequence.ProcessedBonus,
            "shipment earnings did not finish at the exact itemized day payout");
    }

    private static void LaterBaysIncreaseBloodYield()
    {
        var yields = new float[5];
        for (var bayIndex = 0; bayIndex < yields.Length; bayIndex++)
        {
            var grid = new DestructibleGrid(40, 22, 32);
            grid.BuildProcessingStation();
            var line = new ProcessingLine(DestructibleGrid.ProcessingDeckRow * grid.CellSize);
            var world = new BlobWorld(grid) { ProcessingLine = line };
            world.Conveyors.AddRange(line.Belts);
            var x = line.Bays[bayIndex].CenterX;
            var position = new Vector2(x, line.Basin.SurfaceYAt(x));
            world.Granular.Particles.Add(new GranularParticle
            {
                Position = position,
                PreviousPosition = position - new Vector2(0f, 3f),
                Radius = 2f,
                Lifetime = 10f,
                Kind = GranularKind.Blood
            });
            world.Step(Dt);
            yields[bayIndex] = line.Basin.TotalDeposited;
            Assert(MathF.Abs(line.BloodYieldMultiplierAt(x) -
                             line.BloodYieldMultiplierForBay(bayIndex)) < 0.001f,
                $"bay {bayIndex + 1} drain did not resolve to its authored blood yield");
        }

        for (var bayIndex = 1; bayIndex < yields.Length; bayIndex++)
            Assert(yields[bayIndex] > yields[bayIndex - 1] * 1.15f,
                $"bay {bayIndex + 1} blood yield did not increase enough " +
                $"({yields[bayIndex - 1]:0.00} -> {yields[bayIndex]:0.00})");
        Assert(yields[^1] > yields[0] * 2.3f,
            $"final bay blood was not substantially richer than bay one ({yields[0]:0.00} -> {yields[^1]:0.00})");
    }

    private static void DiegoIsDormantAndBubblesWaitForFill()
    {
        Assert(!BloodBasin.DiegoEnabled, "Diego was not disabled");
        var basin = new BloodBasin(250f, 560f, 866f, 101f);
        var initialScale = basin.CreatureScale;
        var initialX = basin.CreatureX;
        for (var i = 0; i < 12; i++) basin.AddMaterial(620f, 3f, 45f, 4f);
        var volumeBeforeDormancyCheck = basin.CurrentFluidVolume;
        for (var i = 0; i < 360; i++) basin.Step(Dt);
        Assert(!basin.CreatureIsFeeding && basin.CreatureConsumed == 0f &&
               basin.CreatureScale == initialScale && basin.CreatureX == initialX,
            "disabled Diego still advanced, fed, grew, or moved");
        Assert(MathF.Abs(basin.CurrentFluidVolume - volumeBeforeDormancyCheck) < 0.02f &&
               basin.TotalConsumedFluid == 0f,
            "disabled Diego still consumed basin fluid");
        Assert(GameRenderer.BasinBubbleCountForLevel(GameRenderer.BasinBubbleThreshold - 0.001f) == 0,
            "basin bubbles appeared below the 35% fill threshold");
        Assert(GameRenderer.BasinBubbleCountForLevel(GameRenderer.BasinBubbleThreshold) == 1 &&
               GameRenderer.BasinBubbleCountForLevel(1f) == 14,
            "basin bubble count did not begin at 35% and scale with deeper fluid");
    }

    private static void OutputCartDispatchesAndReturns()
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        var line = new ProcessingLine(DestructibleGrid.ProcessingDeckRow * grid.CellSize);
        var world = new BlobWorld(grid) { ProcessingLine = line, Gravity = Vector2.Zero };
        world.Conveyors.AddRange(line.Belts);
        var cart = line.CartDockBounds;
        var body = BlobArchetype.ProcessingUnit.Create(new Vector2(cart.Left + cart.Width * 0.5f, cart.Top + 8f));
        Assert(cart.Width > body.Radius * 2f + 20f && cart.Height > body.Radius * 1.35f,
            "output cart is not visibly and physically larger than a processing blob");
        world.Bodies.Add(body);
        world.Step(Dt);
        Assert(line.IsCartLoaded, "cart did not recognize the landed blob");
        Assert(line.TryDispatchCart(world.Bodies), "click-equivalent dispatch rejected a loaded cart");
        Assert(line.CartState == CartCycleState.DoorOpening, "cart moved before its service door began opening");
        for (var i = 0; i < 300; i++) world.Step(Dt);
        Assert(line.CartState == CartCycleState.Docked && !line.IsCartLoaded,
            "cart failed to return empty to its dock");
        Assert(world.Bodies.All(candidate => candidate.ParentId != body.ParentId),
            "dispatched blob remained in the active simulation after leaving through the door");
    }

    private static void NarrowTablesEjectToNextTransfer()
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        var line = new ProcessingLine(DestructibleGrid.ProcessingDeckRow * grid.CellSize);
        Assert(line.Bays.All(bay => bay.Width <= 48.01f),
            "machine platforms expanded into the transfer runs");
        Assert(line.Belts.All(belt => belt.Height <= 26.01f),
            "processing transfer belts were not visually thinned for the tank clearance");
    }

    private static void FinalConveyorTransfersIntoCart()
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        var line = new ProcessingLine(DestructibleGrid.ProcessingDeckRow * grid.CellSize);
        var world = new BlobWorld(grid) { ProcessingLine = line };
        world.Conveyors.AddRange(line.Belts);
        var finalBelt = line.Belts[^1];
        var body = BlobArchetype.ProcessingUnit.Create(new Vector2(
            finalBelt.Position.X + 18f,
            line.DeckY - 36f));
        world.Bodies.Add(body);

        // Laser residue commonly reaches the cart before the larger soft body. That
        // partial load must not stop the short output belt underneath the body.
        var cart = line.OutputCartBounds;
        for (var i = 0; i < 10; i++)
            world.Granular.Particles.Add(new GranularParticle
            {
                Position = new Vector2(cart.Left + 26f + i % 5 * 5f, line.CartFloorY - 6f - i / 5 * 4f),
                PreviousPosition = new Vector2(cart.Left + 26f + i % 5 * 5f, line.CartFloorY - 6f - i / 5 * 4f),
                Radius = 2f,
                Lifetime = 20f,
                Kind = GranularKind.Tissue
            });

        bool BodyReachedCart() => body.Center.X >= cart.Left + 5f &&
                                  body.Center.X <= cart.Right - 5f &&
                                  body.Center.Y >= cart.Top - 38f;

        var minimumX = body.Center.X;
        for (var step = 0; step < 480 && !BodyReachedCart(); step++)
        {
            world.Step(Dt);
            minimumX = MathF.Min(minimumX, body.Center.X);
        }

        Assert(BodyReachedCart() && line.IsCartLoaded,
            $"final belt left a blob short of the cart (center {body.Center.X:0.0}/{body.Center.Y:0.0}, " +
            $"belt end {finalBelt.Position.X + finalBelt.Width:0.0})");
        Assert(minimumX >= finalBelt.Position.X - 12f,
            "output transfer kicked the blob backward off the final belt");
    }

    private static void BayOneEntryDisablesPickup()
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        var line = new ProcessingLine(DestructibleGrid.ProcessingDeckRow * grid.CellSize);
        var world = new BlobWorld(grid) { ProcessingLine = line };
        world.Conveyors.AddRange(line.Belts);
        var body = BlobArchetype.ProcessingUnit.Create(
            new Vector2(line.Belts[0].Position.X + 42f, line.DeckY - 62f));
        world.Bodies.Add(body);
        Assert(ReferenceEquals(world.PickBody(body.Center), body),
            "blob was unpickable before reaching a conveyor");
        for (var i = 0; i < 180 && !world.IsConveyorCommitted(body); i++) world.Step(Dt);
        Assert(world.IsConveyorCommitted(body), "blob never made top contact with the first conveyor");
        Assert(ReferenceEquals(world.PickBody(body.Center), body),
            "conveyor contact incorrectly disabled blob pickup before Bay 1");

        body.ApplyTranslation(
            new Vector2(line.Bays[0].CenterX - body.Center.X, line.DeckY - 30f - body.Center.Y),
            preserveVelocity: false);
        world.Step(Dt);
        Assert(ReferenceEquals(line.LockedBody, body) && line.HasEnteredBayOne(body),
            "Bay 1 did not capture and permanently commit the entering blob");
        Assert(world.PickBody(body.Center) is null,
            "blob remained pickable after entering Bay 1");

        line.SetCrusherButtonHeld(true);
        for (var i = 0; i < 100; i++) world.Step(Dt);
        line.SetCrusherButtonHeld(false);
        for (var i = 0; i < 80; i++) world.Step(Dt);
        Assert(line.LockedBody is null, "Bay 1 did not release the committed blob after its cycle");
        var releasedBody = world.Bodies
            .Where(candidate => candidate.ParentId == body.ParentId && candidate.IsPickable)
            .MaxBy(candidate => candidate.Particles.Length);
        Assert(releasedBody is not null && world.PickBody(releasedBody.Center) is null,
            "Bay 1 pickup lock was lost after the machine released the blob");
    }

    private static void CartForegroundOccludesLooseMatter()
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        var line = new ProcessingLine(DestructibleGrid.ProcessingDeckRow * grid.CellSize);
        var world = new BlobWorld(grid) { ProcessingLine = line };
        world.Conveyors.AddRange(line.Belts);
        using var baseline = new Bitmap(1280, 720);
        using var composite = new Bitmap(1280, 720);
        using var baselineGraphics = Graphics.FromImage(baseline);
        using var compositeGraphics = Graphics.FromImage(composite);
        var renderer = new GameRenderer();
        renderer.Draw(baselineGraphics, baseline.Size, world, null);

        var cart = line.CartDockBounds;
        var bloodPoint = new Vector2(cart.Left + cart.Width * 0.50f, cart.Top + 28f);
        var tissuePoint = new Vector2(cart.Left + cart.Width * 0.62f, cart.Top + 28f);
        world.Granular.Particles.Add(new GranularParticle
        {
            Position = bloodPoint,
            PreviousPosition = bloodPoint,
            Radius = 6f,
            Lifetime = 10f,
            Kind = GranularKind.Blood
        });
        world.Granular.Particles.Add(new GranularParticle
        {
            Position = tissuePoint,
            PreviousPosition = tissuePoint,
            Radius = 6f,
            Lifetime = 10f,
            Kind = GranularKind.Tissue
        });
        world.Bodies.Add(BlobArchetype.ProcessingUnit.Create(
            new Vector2(cart.Left + cart.Width * 0.5f, cart.Top + 8f)));
        renderer.Draw(compositeGraphics, composite.Size, world, null);

        Assert(baseline.GetPixel((int)bloodPoint.X, (int)bloodPoint.Y).ToArgb() ==
               composite.GetPixel((int)bloodPoint.X, (int)bloodPoint.Y).ToArgb(),
            "blood rendered in front of the cart shell");
        Assert(baseline.GetPixel((int)tissuePoint.X, (int)tissuePoint.Y).ToArgb() ==
               composite.GetPixel((int)tissuePoint.X, (int)tissuePoint.Y).ToArgb(),
            "detached tissue rendered in front of the cart shell");
    }

    private static void CartFloorContainsMovingContents()
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        var line = new ProcessingLine(DestructibleGrid.ProcessingDeckRow * grid.CellSize);
        var world = new BlobWorld(grid) { ProcessingLine = line, Gravity = Vector2.Zero };
        world.Conveyors.AddRange(line.Belts);
        var cart = line.CartDockBounds;
        var body = BlobArchetype.ProcessingUnit.Create(
            new Vector2(cart.Left + cart.Width * 0.5f, line.CartFloorY - 22f));
        world.Bodies.Add(body);
        for (var i = 0; i < 8; i++) world.Step(Dt);
        Assert(line.IsCartLoaded && line.TryDispatchCart(world.Bodies),
            "contained blob did not arm the cart dispatch");
        for (var i = 0; i < 90 && line.CartState != CartCycleState.Departing; i++) world.Step(Dt);
        Assert(line.CartState == CartCycleState.Departing, "cart never began moving through the doorway");
        for (var i = 0; i < 12; i++) world.Step(Dt);
        Assert(body.Particles.Where((_, index) => body.IsPhysicalParticle(index))
                .All(particle => particle.Position.Y + particle.Radius <= line.CartFloorY + 0.15f),
            "blob fell through the cart floor during dispatch");

        var bloodPosition = new Vector2(
            line.OutputCartBounds.Left + line.OutputCartBounds.Width * 0.5f,
            line.CartFloorY - 2f);
        world.Granular.Particles.Add(new GranularParticle
        {
            Position = bloodPosition,
            PreviousPosition = bloodPosition - new Vector2(0f, 18f),
            Radius = 2.4f,
            Lifetime = 10f,
            Kind = GranularKind.Blood
        });
        for (var i = 0; i < 4; i++) world.Step(Dt);
        Assert(world.Granular.Particles.All(particle =>
                particle.Position.X < line.OutputCartBounds.Left ||
                particle.Position.X > line.OutputCartBounds.Right ||
                particle.Position.Y + particle.Radius <= line.CartFloorY + 0.15f),
            "blood escaped through the underside of the cart");
    }

    private static void CartWallsContainFastBlood()
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        var line = new ProcessingLine(DestructibleGrid.ProcessingDeckRow * grid.CellSize);
        var world = new BlobWorld(grid) { ProcessingLine = line, Gravity = Vector2.Zero };
        world.Conveyors.AddRange(line.Belts);
        var cart = line.OutputCartBounds;
        var leftStart = new Vector2(cart.Left + 16f, line.CartFloorY - 14f);
        var rightStart = new Vector2(cart.Right - 16f, line.CartFloorY - 18f);
        world.Granular.Particles.Add(new GranularParticle
        {
            Position = leftStart,
            PreviousPosition = leftStart + new Vector2(92f, 0f),
            Radius = 2.4f,
            Lifetime = 10f,
            Kind = GranularKind.Blood
        });
        world.Granular.Particles.Add(new GranularParticle
        {
            Position = rightStart,
            PreviousPosition = rightStart - new Vector2(92f, 0f),
            Radius = 2.4f,
            Lifetime = 10f,
            Kind = GranularKind.Blood
        });

        for (var step = 0; step < 12; step++) world.Step(Dt);
        var innerLeft = cart.Left + 7f;
        var innerRight = cart.Right - 7f;
        Assert(world.Granular.Particles.Count == 2 && world.Granular.Particles.All(particle =>
                particle.Position.X - particle.Radius >= innerLeft - 0.15f &&
                particle.Position.X + particle.Radius <= innerRight + 0.15f &&
                particle.Position.Y + particle.Radius <= line.CartFloorY + 0.15f),
            "high-speed blood tunneled through an output-cart wall or floor");
    }

    private static void LeverAndLightingAreDeterministic()
    {
        var chamber = new HoldingChamber(new Vector2(180f, 170f), 82f);
        var feed = new ChamberFeedController(chamber);
        var bodies = new List<SoftBody>();
        feed.RequestNext();
        chamber.BeginLeverDrag(chamber.LeverRestHandle);
        chamber.UpdateLeverDrag(chamber.LeverPivot + new Vector2(100f, 0f));
        for (var i = 0; i < 240; i++)
        {
            chamber.Step(Dt);
            feed.Update(bodies, Dt, BlobArchetype.ProcessingUnit.Create);
        }
        Assert(chamber.IsOpen && bodies.Count == 0,
            "feed spawned a unit while the user was holding the hatch open");
        chamber.EndLeverDrag();
        for (var i = 0; i < 180; i++)
        {
            chamber.Step(Dt);
            feed.Update(bodies, Dt, BlobArchetype.ProcessingUnit.Create);
        }
        Assert(chamber.HatchOpen < 0.015f && bodies.Count == 1,
            "feed did not wait for the held hatch to close before spawning");

        var grid = new DestructibleGrid(10, 6, 32);
        grid.BuildSampleArena();
        var world = new BlobWorld(grid);
        world.Lighting.ConfigureAmbient(0.74f, Color.FromArgb(8, 13, 20));
        world.Lighting.ConfigureDirectional(new Vector2(0.2f, 1f), 0.12f, Color.SteelBlue);
        world.Lighting.AddIndustrialLight(new IndustrialLight(
            new Vector2(160f, 40f), Vector2.UnitY, 120f, 55f, 0.35f, Color.LightGoldenrodYellow));
        using var bitmap = new Bitmap(320, 192);
        using var graphics = Graphics.FromImage(bitmap);
        var renderer = new GameRenderer();
        renderer.Draw(graphics, bitmap.Size, world, null);
        renderer.Draw(graphics, bitmap.Size, world, null);
        Assert(renderer.LightingCacheBuildCount == 1,
            "unchanged static lighting was rebuilt every frame");
        world.Lighting.ConfigureAmbient(0.76f, Color.FromArgb(8, 13, 20));
        renderer.Draw(graphics, bitmap.Size, world, null);
        Assert(renderer.LightingCacheBuildCount == 2,
            "lighting cache did not invalidate after its configuration changed");

        var blackoutGrid = new DestructibleGrid(10, 6, 32);
        var blackoutWorld = new BlobWorld(blackoutGrid)
        {
            ProcessingLine = new ProcessingLine(
                deckY: 170f,
                powered: false,
                breakerPosition: new Vector2(190f, 45f))
        };
        blackoutWorld.Lighting.ConfigureProcessingStation();
        blackoutWorld.Lighting.SetFactoryPower(false);
        using var blackoutBitmap = new Bitmap(320, 192);
        using var blackoutGraphics = Graphics.FromImage(blackoutBitmap);
        renderer.Draw(blackoutGraphics, blackoutBitmap.Size, blackoutWorld, null);
        Assert(blackoutBitmap.GetPixel(10, 10).ToArgb() == Color.Black.ToArgb(),
            "unpowered factory rendered visible pixels outside the breaker lamp pool");
        var breakerLampPixel = blackoutBitmap.GetPixel(238, 52);
        Assert(breakerLampPixel.R > breakerLampPixel.B && breakerLampPixel.R > 100,
            "unpowered breaker emergency lamp did not remain visibly yellow");

        var hanging = IndustrialLight.CreateHanging(
            new Vector2(176f, 0f), 48f, 240f, 80f, 0.4f, Color.LightGoldenrodYellow);
        var startPosition = hanging.Position;
        for (var step = 0; step < 300; step++) hanging.Step(step * Dt);
        Assert(MathF.Abs(Vector2.Distance(hanging.Anchor, hanging.Position) - hanging.CableLength) < 0.01f,
            "idle lantern swing stretched its suspension cable");
        Assert(Vector2.DistanceSquared(startPosition, hanging.Position) > 0.05f,
            "hanging lantern did not produce idle swing motion");

        var occlusionGrid = new DestructibleGrid(11, 8, 32);
        var occlusionWorld = new BlobWorld(occlusionGrid);
        occlusionGrid.Set(5, 3, CellMaterial.Steel);
        var terrainHit = GameRenderer.TraceLightForDiagnostics(occlusionWorld, hanging, Vector2.UnitY);
        Assert(terrainHit < hanging.Range * 0.45f,
            "solid environment did not terminate the lantern light field");
        occlusionGrid.Set(5, 3, CellMaterial.Air);
        var unobstructed = GameRenderer.TraceLightForDiagnostics(occlusionWorld, hanging, Vector2.UnitY);
        Assert(unobstructed > hanging.Range * 0.9f,
            "cleared environment continued casting a stale lighting shadow");
        occlusionWorld.Conveyors.Add(new ConveyorBelt(new Vector2(145f, 130f), 96f, 28f, 0f));
        var machineryHit = GameRenderer.TraceLightForDiagnostics(occlusionWorld, hanging, Vector2.UnitY);
        Assert(machineryHit < hanging.Range * 0.6f,
            "machinery did not block lantern lighting");
    }

    private static void HoldingChamberReceivesAndReleasesBody()
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        var chamber = HoldingChamber.CreateProcessingStation();
        var line = new ProcessingLine(DestructibleGrid.ProcessingDeckRow * grid.CellSize);
        var world = new BlobWorld(grid) { HoldingChamber = chamber, ProcessingLine = line };
        world.Conveyors.AddRange(line.Belts);
        var body = BlobArchetype.ProcessingUnit.Create(chamber.SpawnPoint);
        chamber.Admit(body);
        world.Bodies.Add(body);
        for (var i = 0; i < 540; i++) world.Step(Dt);
        Assert(Vector2.Distance(body.Center, chamber.Center) < chamber.InnerRadius * 0.72f,
            $"incoming body did not settle inside the chamber ({body.Center})");
        Assert(body.Center.Y > chamber.Center.Y,
            "incoming body did not settle onto the closed hatch");
        Assert(world.PickBody(body.Center) is null,
            "body could be grabbed through the holding chamber");

        chamber.TriggerRelease();
        for (var i = 0; i < 360 && !chamber.HasExited(body); i++) world.Step(Dt);
        Assert(chamber.HasExited(body),
            $"released body remained trapped in the chamber ({body.Center})");
        Assert(ReferenceEquals(world.PickBody(body.Center), body),
            "body remained grab-locked after fully clearing the chamber");
        Assert(body.Center.Y < line.ReceivingTubBounds.Bottom,
            "released body passed through the receiving tub");

        var releasedX = body.Center.X;
        body.AddImpulse(new Vector2(260f, 0f), Dt);
        for (var i = 0; i < 120; i++) world.Step(Dt);
        Assert(body.Center.X > releasedX + 24f,
            $"released body could not move out of the chamber drop lane ({releasedX:0.0} -> {body.Center.X:0.0})");
    }

    private static void ReceivingTubReplacesTerrainTower()
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        for (var x = 2; x <= 7; x++)
        for (var y = DestructibleGrid.ProcessingDeckRow; y < grid.Rows - 1; y++)
            Assert(grid.Cell(x, y).Material == CellMaterial.Air,
                $"obsolete receiving-tower support remained at grid cell {x}/{y}");

        var line = new ProcessingLine(DestructibleGrid.ProcessingDeckRow * grid.CellSize);
        var world = new BlobWorld(grid) { ProcessingLine = line };
        world.Conveyors.AddRange(line.Belts);

        Assert(MathF.Abs(line.Belts[0].Width - 112f) < 0.01f,
            "receiving-tub revision changed the authored first-conveyor length");
        Assert(MathF.Abs(line.ReceivingTubBounds.Left - grid.CellSize) < 0.01f &&
               MathF.Abs(line.ReceivingTubBounds.Right - line.Belts[0].Position.X) < 0.01f,
            "receiving tub does not fit exactly between the wall and first conveyor");
        Assert(MathF.Abs(line.BloodShopBounds.Left - line.ReceivingTubBounds.Left) < 0.01f &&
               MathF.Abs(line.BloodShopBounds.Right - (line.Basin.Left - 20f)) < 0.01f,
            "blood exchange overlaps the wall or the basin endcap");
        for (var x = line.BloodShopBounds.Left; x <= line.BloodShopBounds.Right; x += 8f)
        {
            var top = line.BloodShopTopAt(x);
            Assert(!line.HitBloodShop(new Vector2(x, top - 0.5f)) &&
                   line.HitBloodShop(new Vector2(x, top + 0.5f)),
                $"blood exchange does not conform to the receiving-tub underside at x={x:0}");
        }
        Assert(line.ReceivingTubSurface.Count == 6 &&
               line.ReceivingTubSurface[0] == new Vector2(grid.CellSize, line.DeckY) &&
               line.ReceivingTubSurface[^1] == new Vector2(line.Belts[0].Position.X, line.DeckY) &&
               line.ReceivingTubSurface[1].X - line.ReceivingTubSurface[0].X ==
               line.ReceivingTubSurface[^1].X - line.ReceivingTubSurface[^2].X,
            "receiving-tub lip art and collision geometry are not aligned");
        var tubPath = Path.Combine(AppContext.BaseDirectory, "Assets", "ReceivingTub.png");
        using (var tubSprite = new Bitmap(tubPath))
            Assert(tubSprite.Width == 224 && tubSprite.Height == 48,
                $"receiving-tub sprite does not match its 224x48 collision bounds " +
                $"({tubSprite.Width}x{tubSprite.Height})");
        for (var x = 35; x < grid.Columns - 1; x++)
        for (var y = 19; y < grid.Rows - 1; y++)
            Assert(grid.Cell(x, y).IsSolid,
                $"unused void beneath the cart path remained open at {x}/{y}");

        var wallLipProxy = new Particle
        {
            Position = new Vector2(40f, line.DeckY - 1f),
            PreviousPosition = new Vector2(40f, line.DeckY - 1.2f),
            Radius = 2f,
            InverseMass = 1f
        };
        var wallLipContact = line.ResolveGranular(ref wallLipProxy, Dt, GranularKind.Tissue);
        Assert(wallLipContact.Hit && wallLipContact.Normal.Y < -0.95f,
            "receiving-tub wall lip left a physical gap beside the factory wall");

        var beltLipProxy = new Particle
        {
            Position = new Vector2(line.Belts[0].Position.X - 8f, line.DeckY - 1f),
            PreviousPosition = new Vector2(line.Belts[0].Position.X - 8f, line.DeckY - 1.2f),
            Radius = 2f,
            InverseMass = 1f
        };
        var beltLipContact = line.ResolveGranular(ref beltLipProxy, Dt, GranularKind.Tissue);
        Assert(beltLipContact.Hit && beltLipContact.Normal.Y < -0.95f,
            "receiving-tub conveyor lip left a physical gap at the first belt");

        var floorProxy = new Particle
        {
            Position = new Vector2(144f, line.DeckY + 25f),
            PreviousPosition = new Vector2(144f, line.DeckY + 22f),
            Radius = 2f,
            InverseMass = 1f
        };
        var floorContact = line.ResolveGranular(ref floorProxy, Dt, GranularKind.Tissue);
        Assert(floorContact.Hit && floorContact.Normal.Y < -0.95f,
            "receiving-tub flat floor did not collide with granular matter");
        var rampX = 224f;
        var rampY = line.DeckY + 26f - (rampX - 208f) * (26f / 32f);
        var rampProxy = new Particle
        {
            Position = new Vector2(rampX, rampY - 1f),
            PreviousPosition = new Vector2(rampX, rampY - 1.2f),
            Radius = 2f,
            InverseMass = 1f
        };
        var rampContact = line.ResolveGranular(ref rampProxy, Dt, GranularKind.Tissue);
        Assert(rampContact.Hit && rampContact.Normal.X < -0.5f && rampContact.Normal.Y < -0.5f,
            "receiving-tub exit ramp did not provide matching angled granular collision");

        var body = BlobArchetype.ProcessingUnit.Create(new Vector2(160f, line.DeckY - 96f));
        world.Bodies.Add(body);

        var sawContainedLanding = false;
        for (var step = 0; step < 420; step++)
        {
            world.Step(Dt);
            if (body.Center.X >= line.Belts[0].Position.X || body.Center.Y < line.DeckY - 48f) continue;
            var lowestPhysicalPoint = body.Particles
                .Where((_, index) => body.IsPhysicalParticle(index))
                .Max(particle => particle.Position.Y + particle.Radius);
            if (lowestPhysicalPoint <= line.DeckY + 29f) sawContainedLanding = true;
        }

        Assert(sawContainedLanding,
            "dropped station blob did not settle against the shallow receiving-tub floor");
        var restingX = body.Center.X;
        for (var step = 0; step < 240; step++) world.Step(Dt);
        Assert(!world.IsConveyorCommitted(body) && MathF.Abs(body.Center.X - restingX) < 8f,
            $"passive receiving tub drove its blob to the right ({restingX:0.0} -> {body.Center.X:0.0})");
    }

    private static void ReleasedBlobCannotReenterChamber()
    {
        var chamber = new HoldingChamber(new Vector2(250f, 210f), 82f);
        var externalParticle = new Particle
        {
            Position = chamber.Center + new Vector2(0f, chamber.InnerRadius + 2f),
            PreviousPosition = chamber.Center + new Vector2(0f, chamber.InnerRadius + 18f),
            Radius = 8f,
            InverseMass = 1f
        };
        var contact = chamber.ResolveParticle(ref externalParticle, Dt, admitted: false);
        var minimumDistance = chamber.InnerRadius + 5f + externalParticle.Radius;
        Assert(contact.Hit && Vector2.Distance(externalParticle.Position, chamber.Center) >= minimumDistance - 0.01f,
            "external blob particle crossed into the chamber shell");

        chamber.TriggerRelease();
        for (var step = 0; step < 60; step++) chamber.Step(Dt);
        externalParticle.Position = chamber.Center + new Vector2(0f, chamber.InnerRadius + 1f);
        externalParticle.PreviousPosition = chamber.Center + new Vector2(0f, chamber.InnerRadius + 16f);
        contact = chamber.ResolveParticle(ref externalParticle, Dt, admitted: false);
        Assert(contact.Hit && Vector2.Distance(externalParticle.Position, chamber.Center) >= minimumDistance - 0.01f,
            "open release hatch allowed a released blob back into the chamber");
    }

    private static void BloodTreatsChamberTubeAsEnvironment()
    {
        var chamber = new HoldingChamber(new Vector2(250f, 210f), 82f);
        var tube = chamber.FeedTubeBounds;
        var tubeParticle = new Particle
        {
            Position = new Vector2(tube.Left + tube.Width * 0.5f, tube.Top + tube.Height * 0.45f),
            PreviousPosition = new Vector2(tube.Left + tube.Width * 0.5f, tube.Top + tube.Height * 0.35f),
            Radius = 2.2f,
            InverseMass = 1f
        };
        Assert(chamber.ResolveGranularExterior(ref tubeParticle, Dt).Hit &&
               !chamber.IntersectsGranularObstacle(tubeParticle.Position, tubeParticle.Radius),
            "blood spawned behind the feed tube was not expelled from its foreground volume");

        var shellParticle = new Particle
        {
            Position = chamber.Center - Vector2.UnitY * (chamber.InnerRadius * 0.55f),
            PreviousPosition = chamber.Center - Vector2.UnitY * (chamber.InnerRadius * 0.70f),
            Radius = 2.2f,
            InverseMass = 1f
        };
        Assert(chamber.ResolveGranularExterior(ref shellParticle, Dt).Hit &&
               !chamber.IntersectsGranularObstacle(shellParticle.Position, shellParticle.Radius),
            "blood inside the glass shell was not rejected as foreground matter");

        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        var world = new BlobWorld(grid) { HoldingChamber = chamber };
        for (var i = 0; i < 24; i++)
        {
            var position = new Vector2(
                tube.Left + 8f + i % 8 * (tube.Width - 16f) / 7f,
                tube.Top + 12f + i / 8 * 18f);
            world.Granular.Particles.Add(new GranularParticle
            {
                Position = position,
                PreviousPosition = position - new Vector2((i % 3 - 1) * 2f, 4f),
                Radius = 1.6f + i % 3 * 0.25f,
                Lifetime = 10f,
                Kind = GranularKind.Blood
            });
        }
        for (var step = 0; step < 240; step++) world.Step(Dt);
        Assert(world.Granular.Particles.Where(particle => particle.Kind == GranularKind.Blood)
                .All(particle => !chamber.IntersectsGranularObstacle(particle.Position, particle.Radius)),
            "simulated blood was pushed back through the tube/chamber by loose-pixel contacts");
    }

    public static int RunContourBenchmark()
    {
        var bodies = new List<SoftBody>();
        for (var bodyIndex = 0; bodyIndex < 20; bodyIndex++)
        {
            var source = BlobArchetype.Standard.Create(new Vector2(160 + bodyIndex * 4, 180));
            DamageGestureProfile.Slice(
                source,
                source.Center - Vector2.UnitX * source.Radius * 1.25f,
                source.Center + Vector2.UnitX * source.Radius * 1.25f);
            bodies.Add(source.SplitDisconnectedComponents()
                .Where(piece => piece.AreaConstraints.Any(area => !area.Broken))
                .MaxBy(piece => piece.Particles.Length)!);
        }

        BlobContourBuilder.ResetDiagnostics();
        foreach (var body in bodies) BlobContourBuilder.BuildShell(body);
        var allocationStart = GC.GetAllocatedBytesForCurrentThread();
        var timer = Stopwatch.StartNew();
        const int frames = 600;
        for (var frame = 0; frame < frames; frame++)
        foreach (var body in bodies)
            BlobContourBuilder.BuildShell(body);
        timer.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
        Console.WriteLine(
            $"Contour benchmark: {bodies.Count * frames:N0} queries, {timer.Elapsed.TotalMilliseconds:0.00} ms, " +
            $"{allocated / 1024.0:0.0} KiB, {BlobContourBuilder.TopologyPlanBuildCount} topology builds.");
        return BlobContourBuilder.TopologyPlanBuildCount == bodies.Count ? 0 : 1;
    }

    public static int RunPaintBenchmark()
    {
        var grid = new DestructibleGrid(40, 23, 32);
        for (var y = 7; y < grid.Rows; y += 2)
        for (var x = 0; x < grid.Columns; x++)
            grid.Set(x, y, CellMaterial.Concrete);

        var world = new BlobWorld(grid);
        var conveyor = new ConveyorBelt(new Vector2(180f, 150f), 360f, 48f, 140f);
        world.Conveyors.Add(conveyor);
        var renderer = new GameRenderer();
        using var bitmap = new System.Drawing.Bitmap(1280, 736);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        for (var i = 0; i < 8; i++) renderer.Draw(graphics, bitmap.Size, world, null);
        const int frames = 180;
        var cleanTimer = Stopwatch.StartNew();
        for (var frame = 0; frame < frames; frame++) renderer.Draw(graphics, bitmap.Size, world, null);
        cleanTimer.Stop();

        for (var y = 7; y < grid.Rows; y += 2)
        for (var x = 0; x < grid.Columns; x++)
        for (var sample = 0; sample < 2; sample++)
        {
            grid.DepositBlood(
                x,
                y,
                new Vector2(x * grid.CellSize + 8f + sample * 16f, y * grid.CellSize),
                -Vector2.UnitY,
                0.08f);
        }
        for (var i = 0; i < 160; i++)
            conveyor.DepositBlood(conveyor.Position + new Vector2(3f + i * 2.1f, 2f + i % 4), -Vector2.UnitY, 0.055f);
        for (var i = 0; i < 8; i++) renderer.Draw(graphics, bitmap.Size, world, null);
        var allocationStart = GC.GetAllocatedBytesForCurrentThread();
        var timer = Stopwatch.StartNew();
        for (var frame = 0; frame < frames; frame++) renderer.Draw(graphics, bitmap.Size, world, null);
        timer.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
        for (var step = 0; step < 30; step++) world.Step(Dt);
        var simulationTimer = Stopwatch.StartNew();
        var maximumSimulationMs = 0d;
        const int simulationSteps = 600;
        for (var step = 0; step < simulationSteps; step++)
        {
            world.Step(Dt);
            maximumSimulationMs = Math.Max(maximumSimulationMs, world.LastSimulationMs);
        }
        simulationTimer.Stop();
        Console.WriteLine(
            $"Paint benchmark: {grid.StainedCellCount + conveyor.BloodStains.Count} marks, " +
            $"{timer.Elapsed.TotalMilliseconds / frames:0.00} ms/frame " +
            $"(clean {cleanTimer.Elapsed.TotalMilliseconds / frames:0.00}, " +
            $"paint +{(timer.Elapsed - cleanTimer.Elapsed).TotalMilliseconds / frames:0.00}), " +
            $"{allocated / 1024.0:0.0} KiB / {frames} frames; " +
            $"paint simulation {simulationTimer.Elapsed.TotalMilliseconds / simulationSteps:0.000} ms/step " +
            $"(max {maximumSimulationMs:0.000}).");
        return 0;
    }

    public static int RunRenderBenchmark()
    {
        var grid = new DestructibleGrid(41, 29, 32);
        grid.BuildSampleArena();
        var world = new BlobWorld(grid) { Gravity = Vector2.Zero };
        world.Lighting.ConfigureProcessingStation();
        for (var i = 0; i < 12; i++)
        {
            var body = BlobArchetype.Standard.Create(new Vector2(150f + i % 6 * 190f, 240f + i / 6 * 320f));
            DamageGestureProfile.Bite(body, body.Center + Vector2.UnitY * body.Radius * 0.72f);
            world.Bodies.Add(body);
        }
        for (var step = 0; step < 12; step++) world.Step(Dt);
        for (var x = 1; x < grid.Columns - 1; x++)
        for (var sample = 0; sample < 16; sample++)
            grid.DepositBlood(
                x,
                grid.Rows - 1,
                new Vector2(x * grid.CellSize + 1f + sample * 1.8f, (grid.Rows - 1) * grid.CellSize + 1f),
                -Vector2.UnitY,
                0.055f);

        using var bitmap = new Bitmap(1309, 920);
        using var graphics = Graphics.FromImage(bitmap);
        var renderer = new GameRenderer();
        const int frames = 180;
        double Measure(bool debug)
        {
            renderer.DebugDraw = debug;
            for (var i = 0; i < 12; i++) renderer.Draw(graphics, bitmap.Size, world, null);
            var timer = Stopwatch.StartNew();
            for (var frame = 0; frame < frames; frame++)
            {
                // Model the real 30 Hz swinging-lantern shadow refresh while
                // the game presents at approximately 60 Hz.
                if ((frame & 1) == 0) world.Lighting.NotifyEdited();
                renderer.Draw(graphics, bitmap.Size, world, null);
            }
            timer.Stop();
            return timer.Elapsed.TotalMilliseconds / frames;
        }

        var normalMs = Measure(false);
        var debugMs = Measure(true);
        Console.WriteLine(
            $"Render benchmark: {world.Bodies.Count} bodies, {grid.StainedCellCount} stains, " +
            $"{normalMs:0.00} ms/frame normal, {debugMs:0.00} ms/frame debug at {bitmap.Width}x{bitmap.Height}.");
        return normalMs < 16.7d && debugMs < 28d ? 0 : 1;
    }

    public static int RunStationRenderBenchmark()
    {
        const int frames = 180;
        var viewport = new Size(1280, 720);
        using var bitmap = new Bitmap(viewport.Width, viewport.Height);
        using var graphics = Graphics.FromImage(bitmap);

        static (BlobWorld World, ProcessingLine Line) CreateStation(bool withBody = false)
        {
            var grid = new DestructibleGrid(40, 22, 32);
            grid.BuildProcessingStation();
            var line = new ProcessingLine(DestructibleGrid.ProcessingDeckRow * grid.CellSize);
            var world = new BlobWorld(grid)
            {
                ProcessingLine = line,
                HoldingChamber = HoldingChamber.CreateProcessingStation(),
                Gravity = Vector2.Zero
            };
            world.Conveyors.AddRange(line.Belts);
            world.Lighting.ConfigureProcessingStation();
            world.Lighting.SetFactoryPower(true);
            const int basinSeedColumns = 64;
            for (var seed = 0; seed < basinSeedColumns; seed++)
                line.Basin.AddMaterial(
                    line.Basin.Left + (seed + 0.5f) / basinSeedColumns * line.Basin.Width,
                    line.Basin.FluidCapacity * 0.35f / basinSeedColumns,
                    downwardSpeed: 60f,
                    nutrition: 0f);
            if (withBody)
            {
                world.Bodies.Add(BlobArchetype.ProcessingUnit.Create(
                    new Vector2(line.Bays[0].CenterX, line.DeckY - 30f)));
                for (var step = 0; step < 8; step++) world.Step(Dt);
            }
            return (world, line);
        }

        var maximumFrameMs = 0d;
        void Measure(string label, BlobWorld world, Action<int>? beforeStep = null)
        {
            var renderer = new GameRenderer { ProfileStages = true };
            for (var warmup = 0; warmup < 24; warmup++)
            {
                beforeStep?.Invoke(warmup);
                world.Step(Dt);
                renderer.Draw(graphics, viewport, world, null);
            }
            var environment = 0d;
            var back = 0d;
            var matter = 0d;
            var front = 0d;
            var lighting = 0d;
            var ui = 0d;
            var timer = Stopwatch.StartNew();
            for (var frame = 0; frame < frames; frame++)
            {
                beforeStep?.Invoke(frame + 24);
                world.Step(Dt);
                renderer.Draw(graphics, viewport, world, null);
                environment += renderer.EnvironmentStageMs;
                back += renderer.MachineryBackStageMs;
                matter += renderer.MatterStageMs;
                front += renderer.MachineryFrontStageMs;
                lighting += renderer.LightingStageMs;
                ui += renderer.UiStageMs;
            }
            timer.Stop();
            var averageFrameMs = timer.Elapsed.TotalMilliseconds / frames;
            maximumFrameMs = Math.Max(maximumFrameMs, averageFrameMs);
            Console.WriteLine(
                $"  {label,-18} {averageFrameMs,6:0.00} ms | " +
                $"env {environment / frames,5:0.00}  back {back / frames,5:0.00}  " +
                $"matter {matter / frames,5:0.00}  front {front / frames,5:0.00}  " +
                $"light {lighting / frames,5:0.00}  ui {ui / frames,5:0.00}");
        }

        Console.WriteLine("Station render benchmark (1280x720, simulation advanced at 120 Hz):");
        var flowGrid = new DestructibleGrid(40, 22, 32);
        flowGrid.BuildProcessingStation();
        flowGrid.OpenContinuousConveyorPortals();
        var flowLine = new ProcessingLine(DestructibleGrid.ProcessingDeckRow * flowGrid.CellSize,
            continuousFlow: true);
        var flowWorld = new BlobWorld(flowGrid)
        {
            ProcessingLine = flowLine,
            TubeFeed = new OverheadTubeFeed(flowLine.DeckY)
            {
                MaximumBodiesInFactory = 4,
                EnableBlobPersonalities = true
            },
            EnableBlobPersonalities = true,
            Knife = new PhysicalKnife(flowLine.ContinuousToolRackCenter)
        };
        flowWorld.Conveyors.AddRange(flowLine.Belts);
        flowWorld.Lighting.ConfigureProcessingStation();
        flowWorld.Lighting.SetFactoryPower(true);
        Measure("continuous flow", flowWorld, _ =>
            flowWorld.TubeFeed!.Update(flowWorld.Bodies, Dt, BlobArchetype.ProcessingUnit.Create));

        var idle = CreateStation();
        Measure("powered idle", idle.World);

        var active = CreateStation(withBody: true);
        var parentId = active.World.Bodies[0].ParentId;
        SoftBody Largest() => active.World.Bodies
            .Where(body => body.ParentId == parentId)
            .OrderByDescending(body => body.Particles.Length)
            .First();
        SoftBody MoveToBay(int bayIndex)
        {
            var body = Largest();
            body.ApplyTranslation(new Vector2(
                active.Line.Bays[bayIndex].CenterX - body.Center.X,
                active.Line.DeckY - 30f - body.Center.Y), preserveVelocity: true);
            body.AddImpulse(-body.AverageVelocity(Dt), Dt);
            active.World.Step(Dt);
            return body;
        }

        Assert(active.Line.LockedBody is not null, "station benchmark could not capture crusher body");
        active.Line.SetCrusherButtonHeld(true);
        Measure("crusher held", active.World);
        active.Line.SetCrusherButtonHeld(false);
        for (var step = 0; step < 90; step++) active.World.Step(Dt);

        var drillBody = MoveToBay(1);
        Assert(ReferenceEquals(active.Line.DrillLockedBody, drillBody),
            "station benchmark could not capture drill body");
        active.Line.SetDrillLeverHeld(true);
        Measure("drill held", active.World);
        active.Line.SetDrillLeverHeld(false);
        for (var step = 0; step < 100; step++) active.World.Step(Dt);

        var pressBody = MoveToBay(2);
        Assert(ReferenceEquals(active.Line.PressLockedBody, pressBody),
            "station benchmark could not capture press body");
        for (var step = 0; step < 150 && active.Line.DrumLoading; step++) active.World.Step(Dt);
        Assert(active.Line.ActivatePressButton(), "station benchmark could not activate press");
        Measure("press cycle", active.World);

        var vacuumBody = MoveToBay(3);
        Assert(ReferenceEquals(active.Line.VacuumLockedBody, vacuumBody),
            "station benchmark could not capture vacuum body");
        Assert(active.Line.BeginVacuumDrag(active.Line.VacuumHose.NozzlePosition),
            "station benchmark could not grab vacuum nozzle");
        Measure("vacuum active", active.World, _ =>
        {
            if (active.Line.VacuumLockedBody is { } body)
                active.Line.DragVacuumNozzle(body.Center);
        });
        for (var step = 0; step < 360 && active.Line.VacuumLockedBody is not null; step++)
        {
            active.Line.DragVacuumNozzle(active.Line.VacuumLockedBody.Center);
            active.World.Step(Dt);
        }
        active.Line.EndVacuumDrag();

        var filterBody = MoveToBay(4);
        Assert(ReferenceEquals(active.Line.FilterLockedBody, filterBody),
            "station benchmark could not capture filter body");
        Assert(active.Line.BeginFilterDrag(active.Line.FilterKnobCenter),
            "station benchmark could not grab filter knob");
        var filterReleased = false;
        Measure("laser sweep", active.World, frame =>
        {
            if (filterReleased) return;
            var progress = Math.Clamp(frame / 90f, 0f, 1f);
            active.Line.DragFilterKnob(active.Line.Bays[4].CenterX + 34f - 68f * progress);
            if (progress < 1f) return;
            active.Line.EndFilterDrag();
            filterReleased = true;
        });

        return maximumFrameMs < 13.5d ? 0 : 1;
    }

    public static int RunAudioLoopBenchmark()
    {
        using var mixer = new SoundEffectMixer();
        var worstAverageMs = 0d;
        Console.WriteLine("Machinery audio-loop benchmark (120 repeated state updates):");
        foreach (var cue in new[]
                 { SoundCue.Crusher, SoundCue.Drill, SoundCue.Press, SoundCue.Vacuum, SoundCue.Filter })
        {
            var timer = Stopwatch.StartNew();
            for (var update = 0; update < 120; update++) mixer.SetLooping(cue, true);
            timer.Stop();
            mixer.SetLooping(cue, false);
            var averageMs = timer.Elapsed.TotalMilliseconds / 120d;
            worstAverageMs = Math.Max(worstAverageMs, averageMs);
            Console.WriteLine($"  {cue,-8} {timer.Elapsed.TotalMilliseconds,8:0.00} ms total, {averageMs,6:0.000} ms/update");
        }
        return worstAverageMs < 0.25d ? 0 : 1;
    }

    public static int RunGranularRenderBenchmark()
    {
        var grid = new DestructibleGrid(40, 23, 32);
        for (var y = 7; y < grid.Rows; y += 2)
        for (var x = 0; x < grid.Columns; x++)
            grid.Set(x, y, CellMaterial.Concrete);
        var world = new BlobWorld(grid);
        world.Bodies.Add(BlobArchetype.Standard.Create(new Vector2(260f, 180f)));

        for (var i = 0; i < 900; i++)
        {
            var position = new Vector2(24f + i % 100 * 12.3f, 90f + i / 100 * 37f);
            world.Granular.Particles.Add(new GranularParticle
            {
                Position = position,
                PreviousPosition = position,
                Radius = 1.6f + i % 4 * 0.25f,
                Lifetime = 20f,
                Kind = i % 7 == 0 ? GranularKind.Tissue : GranularKind.Blood
            });
        }
        for (var y = 7; y < grid.Rows; y += 2)
        for (var x = 0; x < grid.Columns; x++)
        for (var sample = 0; sample < 2; sample++)
            grid.DepositBlood(
                x,
                y,
                new Vector2(x * grid.CellSize + 7f + sample * 17f, y * grid.CellSize),
                -Vector2.UnitY,
                0.08f);

        using var bitmap = new Bitmap(2560, 720);
        using var graphics = Graphics.FromImage(bitmap);
        var renderer = new GameRenderer();
        for (var i = 0; i < 20; i++) renderer.Draw(graphics, bitmap.Size, world, null);
        const int frames = 240;
        var allocationStart = GC.GetAllocatedBytesForCurrentThread();
        var timer = Stopwatch.StartNew();
        for (var frame = 0; frame < frames; frame++) renderer.Draw(graphics, bitmap.Size, world, null);
        timer.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
        var millisecondsPerFrame = timer.Elapsed.TotalMilliseconds / frames;
        Console.WriteLine(
            $"Granular render benchmark: {world.Granular.Particles.Count} loose pixels, " +
            $"{grid.StainedCellCount} stains, {millisecondsPerFrame:0.00} ms/frame, " +
            $"{allocated / 1024.0:0.0} KiB / {frames} frames.");
        return millisecondsPerFrame <= 16.67 ? 0 : 1;
    }

    public static int RunGranularSimulationBenchmark()
    {
        var grid = new DestructibleGrid(40, 23, 32);
        grid.BuildProcessingStation();
        var world = new BlobWorld(grid);
        for (var i = 0; i < 7; i++)
        {
            var body = BlobArchetype.ProcessingUnit.Create(
                new Vector2(420f + i * 108f, 610f - i % 2 * 18f));
            world.Bodies.Add(body);
        }
        for (var step = 0; step < 480; step++) world.Step(Dt);

        const int baselineSteps = 240;
        var baselineAllocationStart = GC.GetAllocatedBytesForCurrentThread();
        var baselineTimer = Stopwatch.StartNew();
        for (var step = 0; step < baselineSteps; step++) world.Step(Dt);
        baselineTimer.Stop();
        var baselineAllocated = GC.GetAllocatedBytesForCurrentThread() - baselineAllocationStart;

        var ground = (grid.Rows - 1) * grid.CellSize;
        const int particleCount = 2100;
        for (var i = 0; i < particleCount; i++)
        {
            var column = i % 390;
            var layer = i / 390;
            var position = new Vector2(42f + column * 3.05f, ground - 1.8f - layer * 3.1f);
            world.Granular.Particles.Add(new GranularParticle
            {
                Position = position,
                PreviousPosition = position,
                Radius = 1.45f + i % 3 * 0.12f,
                Lifetime = 100f,
                RestFrames = (byte)(i < 1700 ? 20 : 0),
                Kind = i % 5 == 0 ? GranularKind.Tissue : GranularKind.Blood
            });
        }
        for (var step = 0; step < 60; step++) world.Step(Dt);

        const int measuredSteps = 360;
        var allocationStart = GC.GetAllocatedBytesForCurrentThread();
        var timer = Stopwatch.StartNew();
        var maximumMs = 0d;
        for (var step = 0; step < measuredSteps; step++)
        {
            world.Step(Dt);
            maximumMs = Math.Max(maximumMs, world.LastSimulationMs);
        }
        timer.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
        var averageMs = timer.Elapsed.TotalMilliseconds / measuredSteps;
        var settledPixels = world.Granular.Particles.Count(particle => particle.RestFrames > 12);
        Console.WriteLine(
            $"Granular simulation benchmark: {world.Bodies.Count} bodies, " +
            $"{world.Granular.Particles.Count} pixels ({settledPixels} settled), {averageMs:0.000} ms/step " +
            $"(max {maximumMs:0.000}), {allocated / 1024.0:0.0} KiB / {measuredSteps} steps.");
        Console.WriteLine(
            $"  Bodies-only baseline: {baselineTimer.Elapsed.TotalMilliseconds / baselineSteps:0.000} ms/step, " +
            $"{baselineAllocated / 1024.0:0.0} KiB / {baselineSteps} steps; " +
            $"last body {world.LastBodyPhysicsMs:0.000} ms, granular {world.LastGranularSimulationMs:0.000} ms.");
        Console.WriteLine(
            $"  Granular split: buckets {world.Granular.LastBucketBuildMs:0.000} ms, " +
            $"integrate/collide {world.Granular.LastIntegrationMs:0.000} ms, " +
            $"pixel contacts {world.Granular.LastContactSolveMs:0.000} ms.");
        return 0;
    }

    public static int RunFactoryStressBenchmark()
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        grid.OpenContinuousConveyorPortals();
        var line = new ProcessingLine(DestructibleGrid.ProcessingDeckRow * grid.CellSize,
            continuousFlow: true);
        var world = new BlobWorld(grid)
        {
            ProcessingLine = line,
            EnableBlobPersonalities = true,
            Knife = new PhysicalKnife(line.ContinuousToolRackCenter)
        };
        world.Conveyors.AddRange(line.Belts);
        world.Lighting.ConfigureProcessingStation();
        world.Lighting.SetFactoryPower(true);

        // Reproduce the late-play state rather than the inexpensive spawn state:
        // a compressed, mutually contacting pile with several damaged bodies.
        for (var index = 0; index < 16; index++)
        {
            var column = index % 5;
            var row = index / 5;
            world.Bodies.Add(BlobArchetype.ProcessingUnit.Create(new Vector2(
                115f + column * 55f + row * 8f,
                line.DeckY - 42f - row * 54f)));
        }
        for (var step = 0; step < 180; step++) world.Step(Dt);
        for (var index = 0; index < 9; index++)
        {
            var body = world.Bodies[index];
            DamageGestureProfile.Bite(body, body.Center + new Vector2(
                (index % 3 - 1) * body.Radius * 0.35f,
                body.Radius * 0.52f));
        }
        for (var step = 0; step < 24; step++) world.Step(Dt);

        const int loosePixels = 1450;
        for (var index = 0; index < loosePixels; index++)
        {
            var column = index % 370;
            var layer = index / 370;
            var position = new Vector2(56f + column * 3.05f,
                line.DeckY - 3f - layer * 3.15f);
            world.Granular.Particles.Add(new GranularParticle
            {
                Position = position,
                PreviousPosition = position - new Vector2(0f, index % 4),
                Radius = 1.45f + index % 3 * 0.15f,
                Lifetime = 100f,
                RestFrames = (byte)(index < 980 ? 18 : 0),
                Kind = index % 5 == 0 ? GranularKind.Tissue : GranularKind.Blood
            });
        }
        for (var index = 0; index < 360; index++)
            line.Belts[0].DepositBlood(
                line.Belts[0].Position + new Vector2(8f + index * 3.2f, index % 5),
                -Vector2.UnitY,
                0.055f);

        using var bitmap = new Bitmap(1280, 720);
        using var graphics = Graphics.FromImage(bitmap);
        var renderer = new GameRenderer { ProfileStages = true };
        for (var warmup = 0; warmup < 20; warmup++)
        {
            world.Step(Dt);
            world.Step(Dt);
            renderer.Draw(graphics, bitmap.Size, world, null);
        }
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        const int frames = 180;
        var simulationTicks = 0d;
        var renderTicks = 0d;
        var simulationAllocated = 0L;
        var renderAllocated = 0L;
        var maximumFrameMs = 0d;
        var environmentMs = 0d;
        var machineryBackMs = 0d;
        var matterMs = 0d;
        var machineryFrontMs = 0d;
        var lightingMs = 0d;
        var uiMs = 0d;
        var conveyorMs = 0d;
        var basinMs = 0d;
        var stainMs = 0d;
        var looseRenderMs = 0d;
        var blobRenderMs = 0d;
        var dynamicBuildStart = renderer.DynamicLightingBuildMsTotal;
        var dynamicRaycastStart = renderer.DynamicLightingRaycastMsTotal;
        var dynamicRasterStart = renderer.DynamicLightingRasterMsTotal;
        var dynamicBuildCountStart = renderer.DynamicLightingBuildCount;
        using var benchmarkProcess = Process.GetCurrentProcess();
        var processCpuStart = benchmarkProcess.TotalProcessorTime;
        for (var frame = 0; frame < frames; frame++)
        {
            var allocationStart = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            world.Step(Dt);
            world.Step(Dt);
            var afterSimulation = Stopwatch.GetTimestamp();
            var simulationAllocationEnd = GC.GetAllocatedBytesForCurrentThread();
            renderer.Draw(graphics, bitmap.Size, world, null);
            var end = Stopwatch.GetTimestamp();
            var allocationEnd = GC.GetAllocatedBytesForCurrentThread();
            simulationTicks += afterSimulation - start;
            renderTicks += end - afterSimulation;
            simulationAllocated += simulationAllocationEnd - allocationStart;
            renderAllocated += allocationEnd - simulationAllocationEnd;
            environmentMs += renderer.EnvironmentStageMs;
            machineryBackMs += renderer.MachineryBackStageMs;
            matterMs += renderer.MatterStageMs;
            machineryFrontMs += renderer.MachineryFrontStageMs;
            lightingMs += renderer.LightingStageMs;
            uiMs += renderer.UiStageMs;
            conveyorMs += renderer.ConveyorStageMs;
            basinMs += renderer.BasinBackStageMs;
            stainMs += renderer.StainStageMs;
            looseRenderMs += renderer.GranularStageMs;
            blobRenderMs += renderer.BlobStageMs;
            maximumFrameMs = Math.Max(maximumFrameMs,
                Stopwatch.GetElapsedTime(start, end).TotalMilliseconds);
        }
        var processCpuMs =
            (benchmarkProcess.TotalProcessorTime - processCpuStart).TotalMilliseconds / frames;

        var tickToMs = 1000d / Stopwatch.Frequency;
        var simulationMs = simulationTicks * tickToMs / frames;
        var renderMs = renderTicks * tickToMs / frames;
        var totalMs = simulationMs + renderMs;
        Console.WriteLine("Late factory stress benchmark (2x 120 Hz steps + 1x 1280x720 render):");
        Console.WriteLine(
            $"  {world.Bodies.Count} blobs, {world.Granular.Particles.Count} loose pixels, " +
            $"{world.Grid.StainedCellCount + line.Belts.Sum(belt => belt.BloodStains.Count)} stains");
        Console.WriteLine(
            $"  total {totalMs:0.00} ms/frame (sim {simulationMs:0.00}, render {renderMs:0.00}, " +
            $"max {maximumFrameMs:0.00}, process CPU {processCpuMs:0.00})");
        Console.WriteLine(
            $"  alloc {simulationAllocated / 1024d / frames:0.0} KiB/frame sim + " +
            $"{renderAllocated / 1024d / frames:0.0} KiB/frame render");
        Console.WriteLine(
            $"  last body {world.LastBodyPhysicsMs:0.00} ms, granular {world.LastGranularSimulationMs:0.00} ms; " +
            $"contacts {world.ContactsThisStep}, blob {world.BlobContactsThisStep}");
        Console.WriteLine(
            $"  collision split: particle/hash {world.LastBlobParticleCollisionMs:0.00} ms, " +
            $"hull guard {world.LastHullCollisionMs:0.00} ms");
        Console.WriteLine(
            $"  render env {environmentMs / frames:0.00}, back {machineryBackMs / frames:0.00}, " +
            $"matter {matterMs / frames:0.00}, front {machineryFrontMs / frames:0.00}, " +
            $"light {lightingMs / frames:0.00}, ui {uiMs / frames:0.00}");
        Console.WriteLine(
            $"  render split: conveyor {conveyorMs / frames:0.00}, basin {basinMs / frames:0.00}, " +
            $"stains {stainMs / frames:0.00}, granular {looseRenderMs / frames:0.00}, " +
            $"blobs {blobRenderMs / frames:0.00}");
        var dynamicBuilds = renderer.DynamicLightingBuildCount - dynamicBuildCountStart;
        if (dynamicBuilds > 0)
        {
            Console.WriteLine(
                $"  dynamic light rebuilds: {dynamicBuilds}, avg " +
                $"{(renderer.DynamicLightingBuildMsTotal - dynamicBuildStart) / dynamicBuilds:0.00} ms " +
                $"(rays {(renderer.DynamicLightingRaycastMsTotal - dynamicRaycastStart) / dynamicBuilds:0.00}, " +
                $"raster {(renderer.DynamicLightingRasterMsTotal - dynamicRasterStart) / dynamicBuilds:0.00})");
        }
        const double frameBudgetMs = 25.0;
        const double simulationAllocationBudgetKiB = 128.0;
        const double renderAllocationBudgetKiB = 32.0;
        var simulationAllocationKiB = simulationAllocated / 1024d / frames;
        var renderAllocationKiB = renderAllocated / 1024d / frames;
        var passed = processCpuMs <= frameBudgetMs &&
                     simulationAllocationKiB <= simulationAllocationBudgetKiB &&
                     renderAllocationKiB <= renderAllocationBudgetKiB;
        Console.WriteLine(
            $"  regression budget: {(passed ? "PASS" : "FAIL")} " +
            $"(<= {frameBudgetMs:0.0} ms process CPU, <= {simulationAllocationBudgetKiB:0} KiB sim, " +
            $"<= {renderAllocationBudgetKiB:0} KiB render)");
        return passed ? 0 : 1;
    }

    public static int RunSpreadPopulationBenchmark()
    {
        const int bodyCount = 192;
        const int columns = 24;
        const float spacing = 220f;
        var bodies = new List<SoftBody>(bodyCount);
        for (var bodyIndex = 0; bodyIndex < bodyCount; bodyIndex++)
        {
            bodies.Add(BlobArchetype.ProcessingUnit.Create(new Vector2(
                bodyIndex % columns * spacing,
                bodyIndex / columns * spacing)));
        }

        var particleHash = new BlobParticleSpatialHash();
        for (var warmup = 0; warmup < 16; warmup++)
        {
            particleHash.BuildAndResolve(bodies, Dt);
            BlobHullCollision.ResolveAll(bodies, Dt);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        const int passes = 120;
        var particleTicks = 0L;
        var hullTicks = 0L;
        var contacts = 0;
        var allocationStart = GC.GetAllocatedBytesForCurrentThread();
        for (var pass = 0; pass < passes; pass++)
        {
            var start = Stopwatch.GetTimestamp();
            contacts += particleHash.BuildAndResolve(bodies, Dt);
            var afterParticles = Stopwatch.GetTimestamp();
            contacts += BlobHullCollision.ResolveAll(bodies, Dt);
            var end = Stopwatch.GetTimestamp();
            particleTicks += afterParticles - start;
            hullTicks += end - afterParticles;
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
        var tickToMs = 1000d / Stopwatch.Frequency;
        var legacyPairsPerGuard = bodyCount * (bodyCount - 1) / 2;
        Console.WriteLine(
            $"Spread population benchmark: {bodyCount} awake blobs, " +
            $"{legacyPairsPerGuard:N0} legacy pairs/guard, {passes} passes");
        Console.WriteLine(
            $"  particle/hash + center guard {particleTicks * tickToMs / passes:0.000} ms/pass, " +
            $"hull guard {hullTicks * tickToMs / passes:0.000} ms/pass");
        Console.WriteLine(
            $"  managed allocation {allocated / 1024d / passes:0.000} KiB/pass, contacts {contacts}");
        return contacts == 0 ? 0 : 1;
    }


    public static int WriteStationSnapshot(string outputPath)
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        grid.OpenContinuousConveyorPortals();
        var line = new ProcessingLine(DestructibleGrid.ProcessingDeckRow * grid.CellSize,
            continuousFlow: true);
        var world = new BlobWorld(grid)
        {
            ProcessingLine = line,
            TubeFeed = new OverheadTubeFeed(line.DeckY) { MaximumBodiesInFactory = 5 },
            Knife = new PhysicalKnife(line.ContinuousToolRackCenter),
            WeaponDumbwaiter = new WeaponDumbwaiter(line.ContinuousToolRackCenter)
        };
        world.WeaponDumbwaiter.PrepareInitialDelivery(-1, world.Knife);
        world.Lighting.ConfigureProcessingStation();
        world.Lighting.SetFactoryPower(true);
        world.Conveyors.AddRange(line.Belts);
        for (var i = 0; i < 1200; i++)
        {
            world.TubeFeed.Update(world.Bodies, Dt, BlobArchetype.ProcessingUnit.Create);
            world.Step(Dt);
        }
        // Seed the diagnostic render with a representative, non-gameplay basin load so the
        // translucent filtered surface and live volume gauge are visible while Diego is dormant.
        for (var i = 0; i < 220; i++)
        {
            line.Basin.AddMaterial(
                line.Basin.Left + 55f + i % 70 * 10.5f,
                fluidVolume: 46f,
                downwardSpeed: 28f + i % 9,
                nutrition: 0.045f);
        }
        for (var deposit = 0; deposit < 14; deposit++)
            line.Belts[0].DepositBlood(
                line.Belts[0].Position + new Vector2(560f, 0f),
                -Vector2.UnitY,
                0.20f);
        for (var i = 0; i < 180; i++) world.Step(Dt);
        var impactX = line.Basin.Left + line.Basin.Width * 0.73f;
        line.Basin.AddSuspendedMaterial(
            impactX,
            line.Basin.SurfaceYAt(impactX) - 7f,
            fluidVolume: 18f,
            downwardSpeed: 185f,
            nutrition: 0f,
            radius: 3.1f);
        for (var i = 0; i < 4; i++) world.Step(Dt);
        using var bitmap = new Bitmap(1280, 720);
        using var graphics = Graphics.FromImage(bitmap);
        new GameRenderer().Draw(graphics, bitmap.Size, world, null, null,
            line.ContinuousToolRackCenter);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine($"Station snapshot: {Path.GetFullPath(outputPath)}");
        return 0;
    }

    public static int WriteDumbwaiterSnapshot(string outputPath)
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        grid.OpenContinuousConveyorPortals();
        var line = new ProcessingLine(
            DestructibleGrid.ProcessingDeckRow * grid.CellSize,
            powered: true,
            continuousFlow: true);
        var tool = new PhysicalKnife(line.ContinuousToolRackCenter);
        var dumbwaiter = new WeaponDumbwaiter(line.ContinuousToolRackCenter);
        var world = new BlobWorld(grid)
        {
            ProcessingLine = line,
            Knife = tool,
            WeaponDumbwaiter = dumbwaiter
        };
        world.Conveyors.AddRange(line.Belts);
        world.Lighting.ConfigureProcessingStation();
        world.Lighting.SetFactoryPower(true);
        dumbwaiter.PrepareInitialDelivery(-1, tool);

        using var comparison = new Bitmap(1280 * 6, 720);
        using var frame = new Bitmap(1280, 720);
        using var comparisonGraphics = Graphics.FromImage(comparison);
        var renderer = new GameRenderer();

        void Capture(int panel)
        {
            using (var frameGraphics = Graphics.FromImage(frame))
            {
                frameGraphics.Clear(Color.Black);
                renderer.Draw(frameGraphics, frame.Size, world, null, null,
                    line.ContinuousToolRackCenter);
            }
            comparisonGraphics.DrawImageUnscaled(frame, panel * 1280, 0);
        }

        Capture(0); // fully closed, initial weapon hidden
        for (var step = 0; step < 30; step++) world.Step(Dt);
        Capture(1); // opening
        for (var step = 0; step < 40; step++) world.Step(Dt);
        Capture(2); // open, weapon available
        Assert(tool.BeginGrab(tool.Position), "snapshot could not take the presented weapon");
        dumbwaiter.NotifyWeaponTaken();
        tool.SetGrabTarget(tool.Position - new Vector2(150f, 0f));
        for (var step = 0; step < 26; step++) world.Step(Dt);
        Capture(3); // weapon taken, closing
        for (var step = 0; step < 50; step++) world.Step(Dt);
        Capture(4); // closed while weapon remains in play
        dumbwaiter.TrySpawnDebugToken(new Vector2(360f, line.DeckY - 58f));
        for (var step = 0; step < 26; step++) world.Step(Dt);
        Capture(5); // physical token on the conveyor

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        comparison.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine($"Dumbwaiter state snapshot: {Path.GetFullPath(outputPath)}");
        return 0;
    }

    public static int WriteBloodShipmentSnapshot(string outputPath)
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        grid.OpenContinuousConveyorPortals();
        var line = new ProcessingLine(
            DestructibleGrid.ProcessingDeckRow * grid.CellSize,
            powered: false,
            continuousFlow: true);
        var world = new BlobWorld(grid) { ProcessingLine = line };
        world.Conveyors.AddRange(line.Belts);
        line.Basin.AddMaterial(
            line.Basin.Left + line.Basin.Width * 0.5f,
            line.Basin.FluidCapacity * 0.76f,
            0f,
            0f);
        var shipment = new BloodShipmentSequence(
            line.Basin,
            GameProgression.BaseBloodRatePerGallon,
            processedBlobs: 7,
            processedRate: GameProgression.BaseProcessedBlobRate);
        var renderer = new GameRenderer { BloodShipment = shipment };

        using var comparison = new Bitmap(3840, 720);
        using var graphics = Graphics.FromImage(comparison);

        void DrawPanel(int panel)
        {
            var state = graphics.Save();
            graphics.TranslateTransform(panel * 1280f, 0f);
            graphics.SetClip(new Rectangle(0, 0, 1280, 720));
            renderer.Draw(graphics, new Size(1280, 720), world, null);
            graphics.Restore(state);
        }

        for (var i = 0; i < 54; i++) shipment.Update(Dt);
        DrawPanel(0);
        for (var i = 0; i < 2400 &&
             (shipment.Stage != BloodShipmentStage.LoadingTruck ||
              shipment.CurrentTruckFill01 < 0.42f); i++)
            shipment.Update(Dt);
        DrawPanel(1);
        for (var i = 0; i < 2400 &&
             shipment.Stage != BloodShipmentStage.TruckDeparting; i++)
            shipment.Update(Dt);
        DrawPanel(2);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        comparison.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine($"Blood shipment snapshot: {Path.GetFullPath(outputPath)}");
        return 0;
    }

    public static int WriteArsenalMenuSnapshot(string outputPath)
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        grid.OpenContinuousConveyorPortals();
        var line = new ProcessingLine(DestructibleGrid.ProcessingDeckRow * grid.CellSize,
            continuousFlow: true);
        var world = new BlobWorld(grid)
        {
            ProcessingLine = line,
            Knife = new PhysicalKnife(line.ContinuousToolRackCenter)
        };
        world.Lighting.ConfigureProcessingStation();
        world.Lighting.SetFactoryPower(true);
        world.Conveyors.AddRange(line.Belts);
        var target = BlobArchetype.ProcessingUnit.Create(
            line.ContinuousToolRackCenter + new Vector2(215f, 0f));
        world.Bodies.Add(target);
        world.Knife.SelectArsenalVisual(2);
        world.Knife.Equip(world.Knife.Position, world.Knife.Position);
        var shotgunRotationOrigin = world.Knife.Position;
        world.Knife.BeginRotationAdjust(shotgunRotationOrigin);
        world.Knife.UpdateRotationAdjust(shotgunRotationOrigin + new Vector2(60f, 0f));
        world.Knife.EndRotationAdjust();
        world.Knife.SetGrabTarget(line.ContinuousToolRackCenter + new Vector2(82f, 0f));
        for (var step = 0; step < 18; step++)
            world.Knife.Step(Dt, Vector2.Zero, world.Conveyors, world.Bodies, 1280f, 720f,
                world.TubeFeed);
        world.Knife.BeginPrimaryAction();
        world.Knife.Step(Dt, Vector2.Zero, world.Conveyors, world.Bodies, 1280f, 720f,
            world.TubeFeed);
        world.Knife.EndPrimaryAction();
        world.Knife.BeginRotationAdjust(world.Knife.Position);
        world.Knife.UpdateRotationAdjust(world.Knife.Position + new Vector2(58f, -34f));

        using var bitmap = new Bitmap(3840, 720);
        using var graphics = Graphics.FromImage(bitmap);
        var renderer = new GameRenderer
        {
            ArsenalMenuOpen = true,
            ArsenalMenuSelection = 3
        };
        graphics.SetClip(new Rectangle(0, 0, 1280, 720));
        renderer.Draw(graphics, new Size(1280, 720), world, null);
        graphics.ResetClip();
        graphics.TranslateTransform(1280f, 0f);
        graphics.SetClip(new Rectangle(0, 0, 1280, 720));
        renderer.ArsenalMenuOpen = false;
        renderer.Draw(graphics, new Size(1280, 720), world, null);
        world.Knife.EndRotationAdjust();
        world.Knife.SelectArsenalVisual(11);
        world.Knife.Equip(world.Knife.Position, world.Knife.Position);
        var grenadeRotationOrigin = world.Knife.Position;
        world.Knife.BeginRotationAdjust(grenadeRotationOrigin);
        world.Knife.UpdateRotationAdjust(grenadeRotationOrigin + new Vector2(55f, 0f));
        world.Knife.EndRotationAdjust();
        world.Knife.SetGrabTarget(grenadeRotationOrigin);
        world.Knife.BeginPrimaryAction();
        world.Knife.SetGrabTarget(grenadeRotationOrigin + new Vector2(145f, 74f));
        for (var step = 0; step < 8; step++)
            world.Knife.Step(Dt, new Vector2(0f, 980f), world.Conveyors, world.Bodies,
                1280f, 720f, world.TubeFeed, grid);
        graphics.ResetClip();
        graphics.TranslateTransform(1280f, 0f);
        graphics.SetClip(new Rectangle(0, 0, 1280, 720));
        renderer.DebugDraw = true;
        renderer.Draw(graphics, new Size(1280, 720), world, null);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine($"Arsenal menu snapshot: {Path.GetFullPath(outputPath)}");
        return 0;
    }

    public static int WriteFaceSnapshot(string outputPath)
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        var world = new BlobWorld(grid) { Gravity = Vector2.Zero };
        var neutral = BlobArchetype.ProcessingUnit.Create(new Vector2(510f, 330f));
        var blinking = BlobArchetype.ProcessingUnit.Create(new Vector2(640f, 330f));
        var hurt = BlobArchetype.ProcessingUnit.Create(new Vector2(770f, 330f));
        for (var step = 0; step < 720 && blinking.FaceExpression != BlobFaceExpression.Blink; step++)
            blinking.AdvanceFaceAnimation(Dt);
        hurt.DamageBonds(hurt.Center, hurt.Radius * 2f, 0.01f);
        world.Bodies.Add(neutral);
        world.Bodies.Add(blinking);
        world.Bodies.Add(hurt);

        using var bitmap = new Bitmap(1280, 720);
        using var graphics = Graphics.FromImage(bitmap);
        new GameRenderer().Draw(graphics, bitmap.Size, world, null);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine($"Face snapshot: {Path.GetFullPath(outputPath)}");
        return 0;
    }

    public static int WriteBasinOverflowSnapshot(string outputPath)
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        grid.OpenContinuousConveyorPortals();
        var line = new ProcessingLine(
            DestructibleGrid.ProcessingDeckRow * grid.CellSize,
            continuousFlow: true);
        var world = new BlobWorld(grid) { ProcessingLine = line };
        world.Conveyors.AddRange(line.Belts);
        world.Lighting.ConfigureProcessingStation();
        world.Lighting.SetFactoryPower(true);
        line.Basin.AddMaterial(
            line.Basin.Left + line.Basin.Width * 0.5f,
            line.Basin.FluidCapacity,
            180f,
            0f);
        for (var i = 0; i < 18; i++)
        {
            var x = line.Basin.Left + line.Basin.Width * (0.12f + i % 6 * 0.15f);
            var particle = new GranularParticle
            {
                Position = new Vector2(x, line.Basin.FluidTop + 2f),
                PreviousPosition = new Vector2(x, line.Basin.FluidTop - 3f - i % 4),
                Radius = 1.8f + i % 3 * 0.35f,
                Lifetime = 12f,
                Kind = GranularKind.Blood,
                SplatterOnImpact = true
            };
            line.TryCollectBasinInflow(ref particle, Dt);
            world.Granular.Particles.Add(particle);
        }
        for (var step = 0; step < 360; step++) world.Step(Dt);

        using var bitmap = new Bitmap(1280, 720);
        using var graphics = Graphics.FromImage(bitmap);
        new GameRenderer().Draw(graphics, bitmap.Size, world, null);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine($"Full-basin drain/overflow snapshot: {Path.GetFullPath(outputPath)}");
        return 0;
    }

    public static int WriteGranularOverflowSnapshot(string outputPath)
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        grid.OpenContinuousConveyorPortals();
        var line = new ProcessingLine(
            DestructibleGrid.ProcessingDeckRow * grid.CellSize,
            continuousFlow: true);
        var world = new BlobWorld(grid)
        {
            ProcessingLine = line,
            Gravity = Vector2.Zero
        };
        world.Conveyors.AddRange(line.Belts);
        world.Lighting.ConfigureProcessingStation();
        world.Lighting.SetFactoryPower(true);
        var pileCenters = new[] { 210f, 600f, 1045f };
        for (var pileIndex = 0; pileIndex < pileCenters.Length; pileIndex++)
        for (var i = 0; i < 76; i++)
        {
            var position = new Vector2(
                pileCenters[pileIndex] + i % 8 * 2.4f,
                line.DeckY - 10f - i / 8 % 4 * 2.1f);
            world.Granular.Particles.Add(new GranularParticle
            {
                Position = position,
                PreviousPosition = position,
                Radius = 1.8f + i % 3 * 0.25f,
                Lifetime = 30f,
                RestFrames = 40,
                ForegroundSupportFrames = 18,
                Kind = (i + pileIndex) % 3 == 0
                    ? GranularKind.Tissue
                    : GranularKind.Blood,
                Appearance = (GranularAppearance)((i + pileIndex) % 3)
            });
        }
        for (var step = 0; step < 84; step++) world.Step(Dt);

        using var bitmap = new Bitmap(1280, 720);
        using var graphics = Graphics.FromImage(bitmap);
        new GameRenderer().Draw(graphics, bitmap.Size, world, null);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine(
            $"Dense granular foreground-overflow snapshot ({world.Granular.ForegroundSpills.Count} active): " +
            Path.GetFullPath(outputPath));
        return 0;
    }

    public static int WriteCleaverEffectsSnapshot(string outputPath)
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        grid.OpenContinuousConveyorPortals();
        var line = new ProcessingLine(DestructibleGrid.ProcessingDeckRow * grid.CellSize,
            continuousFlow: true);
        var knife = new PhysicalKnife(new Vector2(640f, 300f));
        var world = new BlobWorld(grid)
        {
            ProcessingLine = line,
            Knife = knife,
            Gravity = Vector2.Zero
        };
        world.Conveyors.AddRange(line.Belts);
        world.Lighting.ConfigureProcessingStation();
        world.Lighting.SetFactoryPower(true);
        var body = BlobArchetype.ProcessingUnit.Create(new Vector2(606f, 350f));
        world.Bodies.Add(body);
        var cursor = knife.Position;
        if (!knife.Equip(knife.Position, cursor) || !knife.BeginPrimaryAction()) return 1;
        for (var step = 0; step < 24; step++)
        {
            knife.SetGrabTarget(cursor);
            world.Step(Dt);
        }

        using var partialCharge = new Bitmap(1280, 720);
        using (var partialGraphics = Graphics.FromImage(partialCharge))
            new GameRenderer().Draw(partialGraphics, partialCharge.Size, world, null);

        for (var step = 0; step < 72; step++)
        {
            knife.SetGrabTarget(cursor);
            world.Step(Dt);
        }

        using var charge = new Bitmap(1280, 720);
        using (var chargeGraphics = Graphics.FromImage(charge))
            new GameRenderer().Draw(chargeGraphics, charge.Size, world, null);

        if (!knife.EndPrimaryAction()) return 1;
        for (var step = 0; step < 70 && !knife.HeavyImpactActive; step++)
        {
            knife.SetGrabTarget(cursor);
            world.Step(Dt);
        }
        if (!knife.HeavyImpactActive) return 1;
        for (var step = 0; step < 5; step++)
        {
            knife.SetGrabTarget(cursor);
            world.Step(Dt);
        }

        using var impact = new Bitmap(1280, 720);
        using (var impactGraphics = Graphics.FromImage(impact))
            new GameRenderer().Draw(impactGraphics, impact.Size, world, null);
        using var comparison = new Bitmap(3840, 720);
        using (var graphics = Graphics.FromImage(comparison))
        {
            graphics.DrawImageUnscaled(partialCharge, 0, 0);
            graphics.DrawImageUnscaled(charge, 1280, 0);
            graphics.DrawImageUnscaled(impact, 2560, 0);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        comparison.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine($"Cleaver charge/impact snapshot: {Path.GetFullPath(outputPath)}");
        return 0;
    }

    public static int WriteDrillSnapshot(string outputPath)
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        var line = new ProcessingLine(DestructibleGrid.ProcessingDeckRow * grid.CellSize);
        var world = new BlobWorld(grid) { ProcessingLine = line, Gravity = Vector2.Zero };
        world.Lighting.ConfigureProcessingStation();
        world.Conveyors.AddRange(line.Belts);
        var body = BlobArchetype.ProcessingUnit.Create(
            new Vector2(line.Bays[0].CenterX, line.DeckY - 30f));
        var parentId = body.ParentId;
        world.Bodies.Add(body);
        world.Step(Dt);
        line.SetCrusherButtonHeld(true);
        for (var i = 0; i < 100; i++) world.Step(Dt);
        line.SetCrusherButtonHeld(false);
        for (var i = 0; i < 80; i++) world.Step(Dt);

        body = world.Bodies.Where(candidate => candidate.ParentId == parentId)
            .OrderByDescending(candidate => candidate.Particles.Length)
            .First();
        body.ApplyTranslation(new Vector2(line.Bays[1].CenterX - body.Center.X,
            line.DeckY - 30f - body.Center.Y), preserveVelocity: true);
        body.AddImpulse(-body.AverageVelocity(Dt), Dt);
        world.Step(Dt);
        line.SetDrillLeverHeld(true);
        for (var i = 0; i < 150; i++)
        {
            world.Step(Dt);
            if (line.DrillDamagePulses > 0 && line.DrillRecoil > 0.9f) break;
        }

        using var bitmap = new Bitmap(1280, 720);
        using var graphics = Graphics.FromImage(bitmap);
        new GameRenderer().Draw(graphics, bitmap.Size, world, null);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine($"Drill impact snapshot: {Path.GetFullPath(outputPath)}");
        return 0;
    }

    private static (BlobWorld World, ProcessingLine Line) CreateDrumIntakeSnapshotScenario()
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        var line = new ProcessingLine(DestructibleGrid.ProcessingDeckRow * grid.CellSize);
        var world = new BlobWorld(grid) { ProcessingLine = line };
        world.Lighting.ConfigureProcessingStation();
        world.Conveyors.AddRange(line.Belts);
        var seed = BlobArchetype.ProcessingUnit.Create(new Vector2(line.Bays[0].CenterX, line.DeckY - 30f));
        var parentId = seed.ParentId;
        world.Bodies.Add(seed);
        world.Step(Dt);
        line.SetCrusherButtonHeld(true);
        for (var i = 0; i < 100; i++) world.Step(Dt);
        line.SetCrusherButtonHeld(false);
        for (var i = 0; i < 80; i++) world.Step(Dt);

        SoftBody Largest() => world.Bodies.Where(body => body.ParentId == parentId)
            .OrderByDescending(body => body.Particles.Length).First();
        var body = Largest();
        body.ApplyTranslation(new Vector2(line.Bays[1].CenterX - body.Center.X,
            line.DeckY - 30f - body.Center.Y), preserveVelocity: true);
        body.AddImpulse(-body.AverageVelocity(Dt), Dt);
        world.Step(Dt);
        line.SetDrillLeverHeld(true);
        for (var i = 0; i < 125; i++) world.Step(Dt);
        line.SetDrillLeverHeld(false);
        for (var i = 0; i < 90; i++) world.Step(Dt);

        body = Largest();
        body.ApplyTranslation(new Vector2(line.Bays[2].CenterX - body.Center.X,
            line.DeckY - 30f - body.Center.Y), preserveVelocity: true);
        body.AddImpulse(-body.AverageVelocity(Dt), Dt);
        world.Step(Dt);
        return (world, line);
    }

    public static int WriteDrumLoadingSnapshot(string outputPath)
    {
        var (world, line) = CreateDrumIntakeSnapshotScenario();
        for (var i = 0; i < 56 && line.DrumLoading; i++) world.Step(Dt);
        using var bitmap = new Bitmap(1280, 720);
        using var graphics = Graphics.FromImage(bitmap);
        new GameRenderer().Draw(graphics, bitmap.Size, world, null);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine($"Drum loading snapshot: {Path.GetFullPath(outputPath)}");
        return 0;
    }

    public static int WriteDrumSnapshot(string outputPath)
    {
        var (world, line) = CreateDrumIntakeSnapshotScenario();
        for (var i = 0; i < 150 && line.DrumLoading; i++) world.Step(Dt);
        line.BeginDrumWheelDrag(line.DrumWheelCenter + new Vector2(20f, 0f));
        for (var i = 1; i <= 36; i++)
        {
            var angle = i * MathF.Tau / 24f;
            line.DragDrumWheel(line.DrumWheelCenter +
                               new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 20f);
            world.Step(Dt);
        }
        using var bitmap = new Bitmap(1280, 720);
        using var graphics = Graphics.FromImage(bitmap);
        new GameRenderer().Draw(graphics, bitmap.Size, world, null);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine($"Drum snapshot: {Path.GetFullPath(outputPath)}");
        return 0;
    }

    public static int WriteWorkerSnapshot(string outputPath)
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        var line = new ProcessingLine(DestructibleGrid.ProcessingDeckRow * grid.CellSize);
        var world = new BlobWorld(grid) { ProcessingLine = line };
        world.Lighting.ConfigureProcessingStation();
        world.Conveyors.AddRange(line.Belts);
        line.Basin.AddMaterial(line.Basin.Left + 20f, ProcessingLine.FactoryWorkerCost + 6_000f,
            180f, 0f);
        var shop = line.BloodShopItemBounds(0);
        line.TryActivateBloodShop(new Vector2(shop.Left + shop.Width * 0.5f,
            shop.Top + shop.Height * 0.5f));
        world.Bodies.Add(BlobArchetype.ProcessingUnit.Create(
            new Vector2(line.Bays[0].CenterX, line.DeckY - 30f)));
        for (var i = 0; i < 2_100 && line.FactoryWorkers[0].Activity != FactoryWorkerActivity.Operating; i++)
            world.Step(Dt);

        using var bitmap = new Bitmap(1280, 720);
        using var graphics = Graphics.FromImage(bitmap);
        new GameRenderer().Draw(graphics, bitmap.Size, world, null);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine($"Worker snapshot: {Path.GetFullPath(outputPath)}");
        return 0;
    }

    public static int WritePipeSnapshot(string outputPath)
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        var line = new ProcessingLine(DestructibleGrid.ProcessingDeckRow * grid.CellSize);
        var world = new BlobWorld(grid) { ProcessingLine = line };
        world.Lighting.ConfigureProcessingStation();
        world.Conveyors.AddRange(line.Belts);
        var deposit = line.Basin.FluidCapacity * 0.82f / (line.Bays.Count * 12f);
        for (var pass = 0; pass < 12; pass++)
        for (var bay = 0; bay < line.Bays.Count; bay++)
            line.Basin.AddMaterial(line.Bays[bay].CenterX, deposit, 115f, 0f);
        for (var i = 0; i < 180; i++) world.Step(Dt);

        using var bitmap = new Bitmap(1280, 720);
        using var graphics = Graphics.FromImage(bitmap);
        new GameRenderer().Draw(graphics, bitmap.Size, world, null);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine($"Pipe snapshot: {Path.GetFullPath(outputPath)}");
        return 0;
    }

    public static int WriteBloodSnapshot(string outputPath)
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        var world = new BlobWorld(grid);
        for (var i = 0; i < 240; i++)
        {
            var position = new Vector2(410f + i % 80 * 9.5f, 510f - i / 80 * 8f);
            world.Granular.Particles.Add(new GranularParticle
            {
                Position = position,
                PreviousPosition = position - new Vector2((i % 7 - 3) * 0.18f, 2f + i % 5),
                Radius = 1.5f + i % 3 * 0.2f,
                Lifetime = 30f,
                Kind = GranularKind.Blood,
                SplatterOnImpact = i % 9 == 0
            });
        }
        for (var step = 0; step < 480; step++) world.Step(Dt);
        using var bitmap = new Bitmap(1280, 720);
        using var graphics = Graphics.FromImage(bitmap);
        new GameRenderer().Draw(graphics, bitmap.Size, world, null);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
        var drips = grid.BloodStains.Where(mark => mark.IsDrip).ToArray();
        Console.WriteLine(
            $"Blood snapshot: {Path.GetFullPath(outputPath)}; {world.Granular.Particles.Count} pixels, " +
            $"{grid.BloodStains.Count} stains, {drips.Length} trails, " +
            $"longest {drips.Select(mark => mark.VisibleTrailLength).DefaultIfEmpty(0f).Max():0.0}px");
        return 0;
    }

    public static int WriteDynamicRunoffSnapshot(string outputPath)
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        const int floorRow = 21;
        for (var deposit = 0; deposit < 54; deposit++)
        {
            var positionX = 520f + deposit % 18 * 8.5f;
            grid.DepositBlood(
                (int)(positionX / grid.CellSize),
                floorRow,
                new Vector2(positionX, floorRow * grid.CellSize),
                -Vector2.UnitY,
                0.14f);
        }
        for (var step = 0; step < 480; step++) grid.BeginStep(Dt);
        for (var step = 0; step < 18000; step++) grid.BeginStep(Dt);
        var dried = grid.BloodStains.Where(mark => mark.IsDrip)
            .Select(mark => (mark.Position, mark.Radius))
            .ToArray();

        for (var deposit = 0; deposit < 54; deposit++)
        {
            var positionX = 520f + deposit % 18 * 8.5f;
            grid.DepositBlood(
                (int)(positionX / grid.CellSize),
                floorRow,
                new Vector2(positionX, floorRow * grid.CellSize),
                -Vector2.UnitY,
                0.14f);
        }
        for (var step = 0; step < 240; step++) grid.BeginStep(Dt);

        var world = new BlobWorld(grid);
        using var bitmap = new Bitmap(1280, 720);
        using var graphics = Graphics.FromImage(bitmap);
        new GameRenderer().Draw(graphics, bitmap.Size, world, null);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
        var wet = grid.BloodStains.Where(mark => mark.IsDrip && mark.Wetness > 0.12f).ToArray();
        var newLanes = wet.Count(mark => dried.All(old => MathF.Abs(old.Position.X - mark.Position.X) >= 5f));
        var widened = wet.Count(mark => dried.Any(old =>
            MathF.Abs(old.Position.X - mark.Position.X) < 2f && mark.Radius >= old.Radius + 0.75f));
        Console.WriteLine(
            $"Dynamic runoff snapshot: {Path.GetFullPath(outputPath)}; " +
            $"{dried.Length} dry lanes, {newLanes} new wet lanes, {widened} widened lanes");
        return 0;
    }

    private static void StandardArchetypeSpawnsConsistently()
    {
        var archetype = BlobArchetype.Standard;
        var first = archetype.Create(new Vector2(100, 100));
        var second = archetype.Create(new Vector2(350, 220));
        Assert(first.Particles.Length == second.Particles.Length, "standard spawns have different tissue resolution");
        Assert(first.Constraints.Count == second.Constraints.Count, "standard spawns have different bond counts");
        Assert(first.AreaConstraints.Count == second.AreaConstraints.Count, "standard spawns have different local-area counts");
        Assert(MathF.Abs(first.ParticleSpacing - second.ParticleSpacing) < 0.0001f, "standard spawns have different particle spacing");
        Assert(MathF.Abs(first.Radius - second.Radius) < 0.0001f, "standard spawns have different physical radii");
        Assert(first.Mode == second.Mode, "standard spawns start in different simulation modes");
    }

    private static void ProcessingUnitUsesCompactScale()
    {
        var compact = BlobArchetype.ProcessingUnit;
        Assert(compact.Radius <= BlobArchetype.Standard.Radius * 0.47f,
            $"processing radius {compact.Radius:0.0} is not less than half of the former scale");
        Assert(compact.TargetTissueParticles == BlobArchetype.Standard.TargetTissueParticles,
            "compact unit changed the original tissue topology instead of scaling it");
        var body = compact.Create(new Vector2(180f, 140f));
        var standard = BlobArchetype.Standard.Create(new Vector2(420f, 140f));
        Assert(body.Radius == compact.Radius, "compact archetype did not reach the physical body");
        Assert(body.Particles.Length == standard.Particles.Length,
            "compact body generated a different tissue lattice than the original body");
        Assert(body.Constraints.Count == standard.Constraints.Count,
            "compact body generated a different bond topology than the original body");
        for (var i = 0; i < body.Particles.Length; i++)
        {
            var compactPoint = (body.Particles[i].Position - body.Center) / body.Radius;
            var standardPoint = (standard.Particles[i].Position - standard.Center) / standard.Radius;
            Assert(Vector2.Distance(compactPoint, standardPoint) < 0.001f,
                "compact body silhouette is not a scaled copy of the original body");
        }
    }

    private static void FilledTissueLattice()
    {
        var blob = new SoftBody(new Vector2(200, 120), 64, 55);
        Assert(blob.Particles.Length >= 25, $"only {blob.Particles.Length} tissue particles generated");
        Assert(blob.Constraints.Count >= blob.Particles.Length, "lattice does not have a connected bond network");
        Assert(blob.AreaConstraints.Count >= blob.Particles.Length / 2, "lattice has too few local-area cells");
        Assert(blob.Particles.Any(p => Vector2.DistanceSquared(p.Position, blob.Center) < 1f), "body has no interior center particle");
    }

    private static void AreaPreservation()
    {
        var blob = new SoftBody(new Vector2(220, 100), 60, 45);
        for (var i = 0; i < blob.Particles.Length; i++)
        {
            if (blob.Particles[i].Position.X < blob.Center.X)
            {
                blob.Particles[i].Position += new Vector2(22f, 12f);
                blob.Particles[i].PreviousPosition = blob.Particles[i].Position;
            }
        }
        var world = new BlobWorld(FlatGrid());
        world.Bodies.Add(blob);
        for (var i = 0; i < 240; i++) world.Step(Dt);
        Assert(blob.AreaRatio is > 0.78f and < 1.22f, $"local area ratio {blob.AreaRatio:0.000}");
    }

    private static void FlatFloorRest()
    {
        var world = new BlobWorld(FlatGrid());
        var blob = new SoftBody(new Vector2(250, 100), 52, 37);
        world.Bodies.Add(blob);
        for (var i = 0; i < 1800; i++) world.Step(Dt);
        Assert(blob.IsSleeping, $"tissue never slept (avg={blob.LastAverageSpeed:0.00}, center={blob.LastCenterSpeed:0.00}, support={blob.LastSupportedParticles})");
        AssertNoPostSleepDrift(world, blob);
    }

    private static void DeformedTissueRest()
    {
        var world = new BlobWorld(FlatGrid());
        var blob = new SoftBody(new Vector2(280, 130), 58, 45);
        var center = blob.Center;
        for (var i = 0; i < blob.Particles.Length; i++)
        {
            if (blob.Particles[i].Position.Y < center.Y)
            {
                blob.Particles[i].Position += new Vector2(-16f, 20f);
                blob.Particles[i].PreviousPosition = blob.Particles[i].Position;
            }
        }
        world.Bodies.Add(blob);
        for (var i = 0; i < 2200; i++) world.Step(Dt);
        Assert(blob.IsSleeping, $"deformed tissue never slept (avg={blob.LastAverageSpeed:0.00}, center={blob.LastCenterSpeed:0.00}, support={blob.LastSupportedParticles})");
        AssertNoPostSleepDrift(world, blob);
    }

    private static void AssertNoPostSleepDrift(BlobWorld world, SoftBody blob)
    {
        var before = blob.Particles.Select(p => p.Position).ToArray();
        for (var i = 0; i < 240; i++) world.Step(Dt);
        var maxDrift = before.Zip(blob.Particles, (a, p) => Vector2.Distance(a, p.Position)).Max();
        Assert(maxDrift < 0.0001f, $"sleeping tissue drifted {maxDrift:0.000000}px");
    }

    private static void GrabPromotesFullTissue()
    {
        var blob = new SoftBody(new Vector2(100, 100), 45, 31);
        blob.Sleep();
        blob.BeginGrab(new Vector2(100, 70));
        Assert(!blob.IsSleeping, "grab did not wake tissue");
        Assert(blob.Mode == SimulationMode.FullTissue, "grab did not request full-tissue representation");
        Assert(blob.GrabbedParticle >= 0, "grab did not select a tissue particle");
    }

    private static void RepresentationSchedulerPrioritizesAndCapsActiveTissue()
    {
        var defaults = new RepresentationScheduler();
        Assert(defaults.ReducedTissueBudget == 12,
            "ordinary active-tissue budget did not default to twelve bodies");

        var scheduler = new RepresentationScheduler
        {
            FullTissueBudget = 3,
            ReducedTissueBudget = 2
        };
        var bodies = new List<SoftBody>();
        for (var i = 0; i < 4; i++)
            bodies.Add(new SoftBody(new Vector2(80f + i * 90f, 100f), 45f, 31));

        var grabbed = new SoftBody(new Vector2(500f, 100f), 45f, 31);
        grabbed.BeginGrab(grabbed.Center);
        var topologyDirty = new SoftBody(new Vector2(590f, 100f), 45f, 31);
        var broken = topologyDirty.DamageBonds(
            topologyDirty.Center,
            topologyDirty.ParticleSpacing * 0.8f,
            2f);
        Assert(broken > 0 && topologyDirty.TopologyDirty,
            "scheduler priority regression could not create dirty topology");
        var impacted = new SoftBody(new Vector2(680f, 100f), 45f, 31)
        {
            LastImpact = 131f
        };
        bodies.Add(grabbed);
        bodies.Add(topologyDirty);
        bodies.Add(impacted);

        scheduler.Apply(bodies);

        Assert(grabbed.Mode == SimulationMode.FullTissue &&
               topologyDirty.Mode == SimulationMode.FullTissue &&
               impacted.Mode == SimulationMode.FullTissue,
            "critical active bodies did not retain full-tissue priority");
        Assert(bodies.Count(body => body.Mode == SimulationMode.ReducedTissue) == 2,
            "ordinary reduced-tissue bodies exceeded their configured budget");
        Assert(bodies.Count(body => body.Mode == SimulationMode.ShapeProxy) == 2,
            "ordinary bodies beyond the active budget were not demoted to shape proxies");
    }

    private static void ImpactDamageDestroysCell()
    {
        var grid = new DestructibleGrid(4, 4, 32);
        grid.Set(2, 2, CellMaterial.Glass);
        grid.BeginStep();
        var destroyed = grid.ApplyImpactDamage(2, 2, 500f);
        Assert(destroyed && !grid.Cell(2, 2).IsSolid, "glass survived a destructive impact");
    }

    private static void TissueBondsBreak()
    {
        var blob = new SoftBody(new Vector2(100, 100), 50, 37);
        var broken = blob.DamageBonds(blob.Center, blob.ParticleSpacing * 0.8f, 2f);
        Assert(broken > 0, "damage did not break tissue bonds");
        Assert(blob.TopologyDirty, "bond break did not queue topology work");
    }

    private static void BlobFacesBlinkAndReactToDamage()
    {
        var blob = new SoftBody(new Vector2(100f, 100f), 50f, 37);
        var blinkObserved = false;
        for (var step = 0; step < 720; step++)
        {
            blob.AdvanceFaceAnimation(Dt);
            blinkObserved |= blob.FaceExpression == BlobFaceExpression.Blink;
        }
        Assert(blinkObserved, "deterministic face timer never produced an occasional blink");

        blob.DamageBonds(blob.Center, blob.Radius * 2f, 0.01f);
        Assert(blob.FaceExpression == BlobFaceExpression.Hurt,
            "contact-local tissue damage did not immediately override the blink with a hurt face");
        for (var step = 0; step < 72; step++) blob.AdvanceFaceAnimation(Dt);
        Assert(blob.FaceExpression != BlobFaceExpression.Hurt,
            "hurt expression did not release after its short reaction window");
    }

    private static void InteriorPointDamageRedirects()
    {
        var blob = new SoftBody(new Vector2(240, 180), 64, 55);
        var center = blob.Center;
        var broken = blob.DamageBonds(center, 5f, 2f);
        Assert(broken > 0, "center point hit did not damage any tissue");

        var closestBrokenMidpoint = blob.Constraints
            .Where(constraint => constraint.Broken)
            .Select(constraint => Vector2.Distance(center,
                (blob.Particles[constraint.A].Position + blob.Particles[constraint.B].Position) * 0.5f))
            .DefaultIfEmpty(0f)
            .Min();
        Assert(closestBrokenMidpoint > blob.ParticleSpacing * 1.5f,
            $"center point hit remained hidden in the core ({closestBrokenMidpoint:0.0}px from center)");
    }

    private static void OutsidePointDamageIsIgnored()
    {
        var blob = new SoftBody(new Vector2(240, 180), 64, 55);
        var brokenBefore = blob.BrokenLinkCount;
        var outside = blob.Center + new Vector2(blob.Radius + blob.ParticleSpacing * 2f, 0f);
        var broken = blob.DamageBonds(outside, 7f, 2f);
        Assert(broken == 0, $"empty-space click broke {broken} tissue bonds");
        Assert(blob.BrokenLinkCount == brokenBefore, "empty-space click changed blob topology");
    }

    private static void SinglePointHitStaysLocal()
    {
        var world = new BlobWorld(FlatGrid()) { Gravity = Vector2.Zero };
        var blob = BlobArchetype.Standard.Create(new Vector2(260, 190));
        var originalCount = blob.Particles.Length;
        world.Bodies.Add(blob);
        blob.DamageLine(blob.Center, blob.Center, 7f, 1.05f);
        world.Step(Dt);

        var coherent = world.Bodies
            .Where(body => !body.IsDetachedDebris)
            .Select(body => body.Particles.Length)
            .DefaultIfEmpty(0)
            .Max();
        Assert(coherent >= originalCount * 0.82f,
            $"one point hit removed {originalCount - coherent}/{originalCount} coherent tissue particles");
    }

    private static void PointBiteCannotLeaveGhostChunk()
    {
        var world = new BlobWorld(FlatGrid()) { Gravity = Vector2.Zero };
        var blob = BlobArchetype.Standard.Create(new Vector2(260f, 190f));
        world.Bodies.Add(blob);
        DamageGestureProfile.Bite(blob, blob.Center + Vector2.UnitX * blob.Radius * 0.72f);
        for (var step = 0; step < 4; step++) world.Step(Dt);

        Assert(world.Bodies.Where(body => body.IsDetachedDebris)
                .All(body => body.AreaConstraints.Any(area => !area.Broken) || body.IsCrumbling),
            "point bite left a non-crumbling detached component with no visible tissue cell");
    }

    private static void DamageGesturesStayDistinct()
    {
        var biteBlob = BlobArchetype.Standard.Create(new Vector2(240, 180));
        var sliceBlob = BlobArchetype.Standard.Create(new Vector2(240, 180));
        var biteBroken = DamageGestureProfile.Bite(biteBlob, biteBlob.Center);
        var sliceBroken = DamageGestureProfile.Slice(
            sliceBlob,
            sliceBlob.Center - Vector2.UnitY * sliceBlob.Radius * 1.2f,
            sliceBlob.Center + Vector2.UnitY * sliceBlob.Radius * 1.2f);

        static float BrokenVerticalSpan(SoftBody body)
        {
            var midpoints = body.Constraints.Where(constraint => constraint.Broken)
                .Select(constraint => (body.Particles[constraint.A].Position + body.Particles[constraint.B].Position) * 0.5f)
                .ToArray();
            return midpoints.Length == 0 ? 0f : midpoints.Max(point => point.Y) - midpoints.Min(point => point.Y);
        }

        var biteSpan = BrokenVerticalSpan(biteBlob);
        var sliceSpan = BrokenVerticalSpan(sliceBlob);
        Assert(biteBroken > 0 && sliceBroken > 0, "bite or slice gesture caused no tissue damage");
        Assert(DamageGestureProfile.SliceThickness < DamageGestureProfile.BiteRadius * 0.35f,
            "slice profile is not materially thinner than bite profile");
        Assert(sliceSpan > biteSpan * 1.8f,
            $"slice damage span {sliceSpan:0.0}px was not cleaner/longer than bite span {biteSpan:0.0}px");
    }

    private static void StraightSliceCreatesFlatSurfaces()
    {
        var blob = BlobArchetype.Standard.Create(new Vector2(240, 180));
        var cutY = blob.Center.Y;
        DamageGestureProfile.Slice(
            blob,
            blob.Center - Vector2.UnitX * blob.Radius * 1.2f,
            blob.Center + Vector2.UnitX * blob.Radius * 1.2f);
        var pieces = blob.SplitDisconnectedComponents();
        Assert(pieces.Count >= 2, "horizontal slice did not create matching components");

        var projectedCutSurfacePoints = new List<Vector2>();
        foreach (var piece in pieces)
        for (var particleIndex = 0; particleIndex < piece.Particles.Length; particleIndex++)
        {
            if (!piece.IsDamageAdjacentParticle(particleIndex)) continue;
            var position = piece.Particles[particleIndex].Position;
            if (piece.TryProjectToCutSurface(position, out var projected)) projectedCutSurfacePoints.Add(projected);
        }
        Assert(projectedCutSurfacePoints.Count >= 4,
            $"only {projectedCutSurfacePoints.Count} boundary particles map to the slice surface");
        var maximumPlaneError = projectedCutSurfacePoints.Max(point => MathF.Abs(point.Y - cutY));
        Assert(maximumPlaneError < 0.01f,
            $"straight slice projection retained {maximumPlaneError:0.000}px of lattice denting");
        foreach (var piece in pieces)
        foreach (var area in piece.AreaConstraints.Where(area => !area.Broken))
        {
            var currentArea = AreaConstraint.SignedArea(
                piece.Particles[area.A].Position,
                piece.Particles[area.B].Position,
                piece.Particles[area.C].Position);
            Assert(MathF.Abs(currentArea) > MathF.Abs(area.RestArea) * 0.7f,
                "slice collapsed a surviving tissue triangle");
            Assert(MathF.Sign(currentArea) == MathF.Sign(area.RestArea),
                "slice inverted a surviving tissue triangle");
        }
    }

    private static void CutSegmentsStayInsideChildSilhouette()
    {
        var blob = BlobArchetype.Standard.Create(new Vector2(240, 180));
        DamageGestureProfile.Slice(
            blob,
            blob.Center - Vector2.UnitX * blob.Radius * 1.25f,
            blob.Center + Vector2.UnitX * blob.Radius * 1.25f);
        var pieces = blob.SplitDisconnectedComponents();
        Assert(pieces.Count >= 2, "slice did not create child components");

        foreach (var piece in pieces.Where(piece => piece.AreaConstraints.Any(area => !area.Broken)))
        {
            var cohesive = new bool[piece.Particles.Length];
            foreach (var area in piece.AreaConstraints.Where(area => !area.Broken))
            {
                cohesive[area.A] = true;
                cohesive[area.B] = true;
                cohesive[area.C] = true;
            }
            var points = piece.Particles.Where((_, index) => cohesive[index]).Select(particle => particle.Position).ToArray();
            foreach (var segment in piece.CurrentWorldCutSegments)
            foreach (var endpoint in new[] { segment.Start, segment.End })
            {
                var nearestTissueDistance = points.Min(point => Vector2.Distance(point, endpoint));
                Assert(nearestTissueDistance <= piece.ParticleSpacing * 1.1f,
                    $"child cut endpoint {endpoint} is {nearestTissueDistance:0.0}px beyond cohesive tissue");
            }
        }
    }

    private static void DamagedCollisionNodesBelongToVisibleTissue()
    {
        var blob = BlobArchetype.Standard.Create(new Vector2(240, 180));
        DamageGestureProfile.Slice(
            blob,
            blob.Center - Vector2.UnitX * blob.Radius * 1.25f,
            blob.Center + Vector2.UnitX * blob.Radius * 1.25f);
        var pieces = blob.SplitDisconnectedComponents();
        Assert(pieces.Count >= 2, "slice did not create child components");

        foreach (var piece in pieces.Where(piece => piece.AreaConstraints.Any(area => !area.Broken)))
        {
            var cohesive = new bool[piece.Particles.Length];
            foreach (var area in piece.AreaConstraints.Where(area => !area.Broken))
            {
                cohesive[area.A] = true;
                cohesive[area.B] = true;
                cohesive[area.C] = true;
            }
            for (var particleIndex = 0; particleIndex < piece.Particles.Length; particleIndex++)
            {
                if (!piece.IsDamageAdjacentParticle(particleIndex)) continue;
                Assert(cohesive[particleIndex],
                    $"damaged particle {particleIndex} collides without belonging to visible tissue");
            }
        }
    }

    private static void DamagedShellContainsPhysicalCenters()
    {
        for (var angleIndex = 0; angleIndex < 12; angleIndex++)
        {
            var blob = BlobArchetype.Standard.Create(new Vector2(240, 180));
            var angle = angleIndex * MathF.Tau / 12f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            DamageGestureProfile.Slice(
                blob,
                blob.Center - direction * blob.Radius * 1.3f,
                blob.Center + direction * blob.Radius * 1.3f);
            var pieces = blob.SplitDisconnectedComponents();
            foreach (var piece in pieces.Where(piece => piece.PhysicalParticleCount >= 3 &&
                                                        piece.AreaConstraints.Any(area => !area.Broken)))
            {
                var contour = BlobContourBuilder.BuildShell(piece);
                var shell = contour.Points;
                Assert(shell.Length >= 3, "damaged component has no visual shell");
                using var renderedPath = GameRenderer.BuildMaterialPath(
                    shell.Select(point => new System.Drawing.PointF(point.X, point.Y)).ToArray(),
                    contour.WoundPoints,
                    piece,
                    contour.ParticleIndices);
                using var boundaryTolerance = new System.Drawing.Pen(System.Drawing.Color.Black, 3f);
                for (var particleIndex = 0; particleIndex < piece.Particles.Length; particleIndex++)
                {
                    if (!piece.IsPhysicalParticle(particleIndex)) continue;
                    var position = piece.Particles[particleIndex].Position;
                    Assert(PointInOrOnPolygon(position, shell, 0.15f),
                        $"angle {angleIndex}, physical contact center {particleIndex} at " +
                        $"{position} lies outside its damaged visual shell");
                    Assert(renderedPath.IsVisible(position.X, position.Y) ||
                           renderedPath.IsOutlineVisible(position.X, position.Y, boundaryTolerance),
                        $"angle {angleIndex}, physical contact center {particleIndex} at {position} lies outside " +
                        $"the rendered curve (path points {renderedPath.PointCount})");
                }
            }
        }
    }

    private static void RepeatedPointBitesContainContacts()
    {
        for (var scenario = 0; scenario < 10; scenario++)
        {
            var world = new BlobWorld(FlatGrid()) { Gravity = Vector2.Zero };
            world.Bodies.Add(BlobArchetype.Standard.Create(new Vector2(260f, 190f)));
            for (var hit = 0; hit < 12; hit++)
            {
                var target = world.Bodies
                    .Where(body => !body.IsDetachedDebris && body.IsPickable)
                    .MaxBy(body => body.PhysicalParticleCount);
                if (target is null) break;
                var angle = (scenario * 0.73f + hit * 1.91f) % MathF.Tau;
                var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                var radiusScale = 0.48f + (hit % 4) * 0.10f;
                DamageGestureProfile.Bite(target, target.Center + direction * target.Radius * radiusScale);
                for (var step = 0; step < 3; step++) world.Step(Dt);

                foreach (var body in world.Bodies.Where(body => body.PhysicalParticleCount >= 3 &&
                                                                 body.AreaConstraints.Any(area => !area.Broken)))
                {
                    var contour = BlobContourBuilder.BuildShell(body);
                    var shell = contour.Points;
                    Assert(shell.Length >= 3,
                        $"scenario {scenario}, hit {hit}: physical body has no visual shell");
                    using var renderedPath = GameRenderer.BuildMaterialPath(
                        shell.Select(point => new System.Drawing.PointF(point.X, point.Y)).ToArray(),
                        contour.WoundPoints,
                        body,
                        contour.ParticleIndices);
                    using var boundaryTolerance = new System.Drawing.Pen(System.Drawing.Color.Black, 3f);
                    for (var particleIndex = 0; particleIndex < body.Particles.Length; particleIndex++)
                    {
                        if (!body.IsPhysicalParticle(particleIndex)) continue;
                        var position = body.Particles[particleIndex].Position;
                        Assert(PointInOrOnPolygon(position, shell, 0.15f),
                            $"scenario {scenario}, hit {hit}: contact {particleIndex} at {position} " +
                            "escaped the point-damaged visual shell");
                        Assert(renderedPath.IsVisible(position.X, position.Y) ||
                               renderedPath.IsOutlineVisible(position.X, position.Y, boundaryTolerance),
                            $"scenario {scenario}, hit {hit}: contact {particleIndex} at {position} " +
                            "escaped the final rendered point-damaged shape");
                    }
                }
            }
        }
    }

    private static bool PointInOrOnPolygon(Vector2 point, ReadOnlySpan<Vector2> polygon, float tolerance)
    {
        var inside = false;
        for (var i = 0; i < polygon.Length; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Length];
            var edge = b - a;
            var lengthSquared = edge.LengthSquared();
            if (lengthSquared > 0.0001f)
            {
                var t = Math.Clamp(Vector2.Dot(point - a, edge) / lengthSquared, 0f, 1f);
                if (Vector2.DistanceSquared(point, a + edge * t) <= tolerance * tolerance) return true;
            }
            if ((a.Y > point.Y) == (b.Y > point.Y)) continue;
            var crossingX = (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X;
            if (point.X < crossingX) inside = !inside;
        }
        return inside;
    }

    private static void DamagedContoursAreStable()
    {
        var blob = BlobArchetype.Standard.Create(new Vector2(240, 180));
        DamageGestureProfile.Slice(
            blob,
            blob.Center - Vector2.UnitX * blob.Radius * 1.25f,
            blob.Center + Vector2.UnitX * blob.Radius * 1.25f);
        var pieces = blob.SplitDisconnectedComponents();
        Assert(pieces.Count >= 2, "slice did not create contour-bearing children");

        foreach (var piece in pieces.Where(piece => piece.AreaConstraints.Any(area => !area.Broken)))
        {
            var first = BlobContourBuilder.BuildShell(piece);
            var second = BlobContourBuilder.BuildShell(piece);
            Assert(first.Points.Length >= 3 && BlobContourBuilder.IsValid(first.Points),
                "damaged material contour is open, crossed, or degenerate");
            Assert(first.Points.Length == second.Points.Length,
                "unchanged topology produced a different contour vertex count");
            for (var i = 0; i < first.Points.Length; i++)
                Assert(Vector2.Distance(first.Points[i], second.Points[i]) < 0.001f,
                    "unchanged topology produced a moving/nondeterministic contour");

            var active = piece.Particles.Where((_, index) => !piece.IsConvertedParticle(index)).Select(particle => particle.Position).ToArray();
            var minimum = new Vector2(active.Min(point => point.X), active.Min(point => point.Y));
            var maximum = new Vector2(active.Max(point => point.X), active.Max(point => point.Y));
            var skin = piece.ParticleSpacing * 0.55f;
            foreach (var point in first.Points)
                Assert(point.X >= minimum.X - skin && point.X <= maximum.X + skin &&
                       point.Y >= minimum.Y - skin && point.Y <= maximum.Y + skin,
                    $"material contour protrudes beyond supported tissue at {point}; particles {minimum}..{maximum}, skin {skin:0.0}");
            for (var i = 0; i < first.Points.Length; i++)
            {
                var edgeLength = Vector2.Distance(first.Points[i], first.Points[(i + 1) % first.Points.Length]);
                Assert(edgeLength <= piece.ParticleSpacing * 2.4f,
                    $"material contour contains a {edgeLength:0.0}px tail edge");
            }
        }
    }

    private static void DenseDragSamplingIsInvariant()
    {
        var single = BlobArchetype.Standard.Create(new Vector2(240, 180));
        var dense = BlobArchetype.Standard.Create(new Vector2(240, 180));
        var start = single.Center - Vector2.UnitX * single.Radius * 1.25f;
        var end = single.Center + Vector2.UnitX * single.Radius * 1.25f;
        DamageGestureProfile.Slice(single, start, end);
        var path = Enumerable.Range(0, 101).Select(index => Vector2.Lerp(start, end, index / 100f)).ToArray();
        DamageGestureProfile.SlicePath(dense, path);

        Assert(single.Constraints.Count(constraint => constraint.Broken) ==
               dense.Constraints.Count(constraint => constraint.Broken),
            "mouse sample count changed the number of severed tissue bonds");
        Assert(single.AreaConstraints.Count(area => area.Broken) ==
               dense.AreaConstraints.Count(area => area.Broken),
            "mouse sample count changed the removed material cells");
        var singlePieces = single.SplitDisconnectedComponents().OrderBy(piece => piece.Particles.Length).ToArray();
        var densePieces = dense.SplitDisconnectedComponents().OrderBy(piece => piece.Particles.Length).ToArray();
        Assert(singlePieces.Select(piece => piece.Particles.Length)
                .SequenceEqual(densePieces.Select(piece => piece.Particles.Length)),
            "mouse sample count changed the resulting component topology");
    }

    private static void ContourTopologyIsEventCached()
    {
        var blob = BlobArchetype.Standard.Create(new Vector2(240, 180));
        DamageGestureProfile.Slice(
            blob,
            blob.Center - Vector2.UnitX * blob.Radius * 1.25f,
            blob.Center + Vector2.UnitX * blob.Radius * 1.25f);
        var piece = blob.SplitDisconnectedComponents()
            .Where(candidate => candidate.AreaConstraints.Any(area => !area.Broken))
            .MaxBy(candidate => candidate.Particles.Length)!;
        BlobContourBuilder.ResetDiagnostics();
        for (var query = 0; query < 400; query++)
            Assert(BlobContourBuilder.BuildShell(piece).Points.Length >= 3,
                "cached damaged contour disappeared");
        Assert(BlobContourBuilder.TopologyPlanBuildCount == 1,
            $"unchanged contour rebuilt topology {BlobContourBuilder.TopologyPlanBuildCount} times");
    }

    private static void DamagedContourExtentDoesNotPop()
    {
        var source = BlobArchetype.Standard.Create(new Vector2(260, 190));
        DamageGestureProfile.Slice(
            source,
            source.Center - Vector2.UnitX * source.Radius * 1.25f,
            source.Center + Vector2.UnitX * source.Radius * 1.25f);
        var pieces = source.SplitDisconnectedComponents()
            .Where(piece => piece.AreaConstraints.Any(area => !area.Broken))
            .ToArray();
        Assert(pieces.Length >= 2, "slice produced no damaged contours to monitor");
        var world = new BlobWorld(FlatGrid()) { Gravity = Vector2.Zero };
        world.Bodies.AddRange(pieces);

        var previousExtents = new Dictionary<int, Vector2>();
        var maximumDelta = 0f;
        for (var step = 0; step < 360; step++)
        {
            world.Step(Dt);
            foreach (var piece in pieces)
            {
                var contour = BlobContourBuilder.BuildShell(piece).Points;
                if (contour.Length < 3) continue;
                var extent = new Vector2(
                    contour.Max(point => point.X) - contour.Min(point => point.X),
                    contour.Max(point => point.Y) - contour.Min(point => point.Y));
                if (step >= 60 && previousExtents.TryGetValue(piece.Id, out var previous))
                    maximumDelta = MathF.Max(maximumDelta, Vector2.Distance(extent, previous));
                previousExtents[piece.Id] = extent;
            }
        }
        Assert(maximumDelta <= source.ParticleSpacing * 0.08f,
            $"undisturbed damaged contour jumped {maximumDelta:0.00}px between frames");
    }

    private static void HeavilyDamagedRestCannotSizeFlip()
    {
        var body = BlobArchetype.Standard.Create(new Vector2(300, 180));
        DamageGestureProfile.Slice(
            body,
            body.Center - Vector2.UnitY * body.Radius * 1.3f,
            body.Center + Vector2.UnitY * body.Radius * 1.3f);
        body = body.SplitDisconnectedComponents()
            .Where(piece => piece.AreaConstraints.Any(area => !area.Broken))
            .MaxBy(piece => piece.Particles.Length)!;
        DamageGestureProfile.Slice(
            body,
            body.Center - Vector2.UnitX * body.Radius * 1.3f,
            body.Center + Vector2.UnitX * body.Radius * 1.3f);
        body = body.SplitDisconnectedComponents()
            .Where(piece => piece.AreaConstraints.Any(area => !area.Broken))
            .MaxBy(piece => piece.Particles.Length)!;
        Assert(body.Particles.Length < BlobArchetype.Standard.TargetTissueParticles * 0.65f,
            "stress body was not damaged enough to exercise small-form contour policy");

        var world = new BlobWorld(FlatGrid());
        world.Bodies.Add(body);
        for (var step = 0; step < 1800; step++) world.Step(Dt);
        Assert(body.IsSleeping, "heavily damaged supported tissue never reached deterministic rest");

        var previousExtent = Vector2.Zero;
        var previousDelta = Vector2.Zero;
        var maximumStep = 0f;
        var reversals = 0;
        for (var step = 0; step < 360; step++)
        {
            world.Step(Dt);
            var contour = BlobContourBuilder.BuildShell(body).Points;
            Assert(contour.Length >= 3 && BlobContourBuilder.IsValid(contour),
                "resting damaged body lost its stable material contour");
            var extent = new Vector2(
                contour.Max(point => point.X) - contour.Min(point => point.X),
                contour.Max(point => point.Y) - contour.Min(point => point.Y));
            if (step > 0)
            {
                var delta = extent - previousExtent;
                maximumStep = MathF.Max(maximumStep, delta.Length());
                if (delta.Length() > body.ParticleSpacing * 0.04f &&
                    previousDelta.Length() > body.ParticleSpacing * 0.04f &&
                    Vector2.Dot(delta, previousDelta) < 0f) reversals++;
                previousDelta = delta;
            }
            previousExtent = extent;
        }
        Assert(maximumStep < 0.01f,
            $"resting damaged contour changed size by {maximumStep:0.000}px without an event");
        Assert(reversals == 0, $"resting damaged contour alternated size {reversals} times");
    }

    private static void CollisionHullUsesMaterialContour()
    {
        var blob = BlobArchetype.Standard.Create(new Vector2(240, 180));
        DamageGestureProfile.Slice(
            blob,
            blob.Center - Vector2.UnitX * blob.Radius * 1.25f,
            blob.Center + Vector2.UnitX * blob.Radius * 1.25f);
        var piece = blob.SplitDisconnectedComponents()
            .Where(candidate => candidate.AreaConstraints.Any(area => !area.Broken))
            .MaxBy(candidate => candidate.Particles.Length)!;
        var material = BlobContourBuilder.BuildShell(piece).Points;
        var collision = BlobHullCollision.BuildHull(piece);
        Assert(collision.Length >= 3, "damaged body produced no collision hull");
        foreach (var point in collision)
            Assert(material.Any(materialPoint => Vector2.DistanceSquared(materialPoint, point) < 0.001f),
                $"collision invented geometry outside the material contour at {point}");
    }

    private static void CutSurfaceFollowsBodyTransform()
    {
        var blob = BlobArchetype.Standard.Create(new Vector2(220, 180));
        var originalCenter = blob.Center;
        DamageGestureProfile.Slice(
            blob,
            originalCenter - Vector2.UnitX * blob.Radius * 1.2f,
            originalCenter + Vector2.UnitX * blob.Radius * 1.2f);
        blob.ApplyTranslation(new Vector2(150f, 70f), preserveVelocity: true);
        var movedCenter = blob.Center;
        Assert(blob.TryProjectToCutSurface(movedCenter, out var movedProjection) &&
               Vector2.Distance(movedProjection, movedCenter) < 0.01f,
            "translated cut surface did not follow its tissue body");
        Assert(!blob.TryProjectToCutSurface(originalCenter, out _),
            "cut surface collider remained at its original world position");

        for (var i = 0; i < blob.Particles.Length; i++)
        {
            var relativePosition = blob.Particles[i].Position - movedCenter;
            var relativePrevious = blob.Particles[i].PreviousPosition - movedCenter;
            blob.Particles[i].Position = movedCenter + new Vector2(-relativePosition.Y, relativePosition.X);
            blob.Particles[i].PreviousPosition = movedCenter + new Vector2(-relativePrevious.Y, relativePrevious.X);
        }
        var pointOnRotatedCut = movedCenter + Vector2.UnitY * 24f;
        Assert(blob.TryProjectToCutSurface(pointOnRotatedCut, out var rotatedProjection) &&
               Vector2.Distance(rotatedProjection, pointOnRotatedCut) < 0.01f,
            "rotated cut surface did not rotate with its tissue body");
    }

    private static void CutGeometryRenders()
    {
        var world = new BlobWorld(FlatGrid());
        var blob = BlobArchetype.Standard.Create(new Vector2(300, 300));
        world.Bodies.Add(blob);
        for (var step = 0; step < 600; step++) world.Step(Dt);
        var cutY = blob.Center.Y;
        var cutStart = blob.Center.X - blob.Radius * 1.25f;
        var cutEnd = blob.Center.X + blob.Radius * 1.25f;
        for (var x = cutStart; x < cutEnd; x += 20f)
            DamageGestureProfile.Slice(blob, new Vector2(x, cutY), new Vector2(MathF.Min(x + 20f, cutEnd), cutY));
        using var bitmap = new Bitmap(1280, 720);
        using var graphics = Graphics.FromImage(bitmap);
        var renderer = new GameRenderer();
        for (var step = 0; step < 120; step++)
        {
            world.Step(Dt);
            renderer.Draw(graphics, bitmap.Size, world, null);
        }
    }

    private static void CutCreatesComponents()
    {
        var blob = new SoftBody(new Vector2(200, 160), 64, 55);
        var originalCount = blob.Particles.Length;
        var center = blob.Center;
        blob.DamageLine(
            center - new Vector2(0f, blob.Radius * 1.2f),
            center + new Vector2(0f, blob.Radius * 1.2f),
            3f,
            2f);
        var pieces = blob.SplitDisconnectedComponents();
        Assert(pieces.Count >= 2, "full cut did not create multiple connected components");
        Assert(pieces.Sum(p => p.Particles.Length) == originalCount, "particle mass was lost during split");
        Assert(pieces.All(p => p.ParentId == blob.ParentId), "split pieces lost lineage identity");
        Assert(pieces.All(p => p.Constraints.All(c => c.A < p.Particles.Length && c.B < p.Particles.Length)), "split produced invalid bond indices");
        Assert(pieces.All(HasNoDanglingBridge), "split left tissue hanging from a single structural bond");
    }

    private static void FreshCutPiecesDoNotInterlock()
    {
        var world = new BlobWorld(FlatGrid()) { Gravity = Vector2.Zero };
        var blob = BlobArchetype.Standard.Create(new Vector2(300, 200));
        world.Bodies.Add(blob);
        DamageGestureProfile.Slice(
            blob,
            blob.Center - Vector2.UnitX * blob.Radius * 1.2f,
            blob.Center + Vector2.UnitX * blob.Radius * 1.2f);
        world.Step(Dt);
        var parent = world.Bodies.First(body => !body.IsDetachedDebris);
        var chunk = world.Bodies.Where(body => body.IsDetachedDebris)
            .MaxBy(body => body.ActiveParticleCount)!;
        for (var step = 0; step < 180; step++) world.Step(Dt);

        var overlapping = BlobHullCollision.TryGetPenetration(parent, chunk, out _, out var depth);
        Assert(!overlapping || depth <= 1.25f,
            $"fresh parent/chunk hulls remained interlocked by {depth:0.00}px");
        Assert(parent.AreaRatio is > 0.72f and < 1.28f,
            $"retained cut body area distorted to {parent.AreaRatio:0.00}");
        Assert(chunk.AreaRatio is > 0.72f and < 1.28f,
            $"detached cut body area distorted to {chunk.AreaRatio:0.00}");
    }

    private static void DetachedChunkLifecycle()
    {
        var world = new BlobWorld(FlatGrid()) { Gravity = Vector2.Zero };
        var blob = new SoftBody(new Vector2(200, 160), 64, 55);
        var originalCount = blob.Particles.Length;
        world.Bodies.Add(blob);
        var center = blob.Center;
        blob.DamageLine(
            center - new Vector2(0f, blob.Radius * 1.2f),
            center + new Vector2(0f, blob.Radius * 1.2f),
            3f,
            2f);
        world.Step(Dt);
        Assert(world.Bodies.Count > 1, "detached component disappeared instead of remaining visible");
        Assert(world.DetachedChunkCount > 0, "detached component was not registered as a physical chunk");
        Assert(world.Bodies.Any(body => body.IsDetachedDebris && !body.IsPickable), "detached chunk is still pickable");
        Assert(world.TotalTopologySplits == 1, $"expected one topology event, got {world.TotalTopologySplits}");

        var detached = world.Bodies.First(body => body.IsDetachedDebris);
        var maximumRestError = detached.Constraints
            .Where(constraint => !constraint.Broken)
            .Select(constraint => MathF.Abs(
                Vector2.Distance(detached.Particles[constraint.A].Position, detached.Particles[constraint.B].Position) -
                constraint.RestLength))
            .DefaultIfEmpty(0f)
            .Max();
        Assert(maximumRestError < 0.001f,
            $"detached component did not preserve its exact deformed cut-time shape (max bond error {maximumRestError:F6})");
        Assert(detached.AverageVelocity(Dt).Length() < 220f, "detached component inherited an artificial launch velocity");

        // Even tiny scraps must exist as visible physical debris before a real
        // impact; creation itself is not permission to erase them into pixels.
        for (var i = 0; i < 30; i++) world.Step(Dt);
        Assert(world.Bodies.Where(body => body.IsDetachedDebris)
                .All(body => body.ActiveParticleCount == body.Particles.Length),
            "a fresh airborne fragment began disappearing without an impact");
        var initialMicroPixels = world.Granular.TissuePixelCount;
        for (var i = 0; i < 240; i++) world.Step(Dt);
        Assert(world.DetachedChunkCount > 0, "airborne chunk disintegrated without a landing or impact");
        Assert(world.Granular.TissuePixelCount == initialMicroPixels, "airborne chunk became pixels before impact");

        world.Gravity = new Vector2(0f, 980f);
        var progressiveErosionObserved = false;
        var contactFirstErosionObserved = false;
        var previousTissuePixels = world.Granular.TissuePixelCount;
        var maximumTissuePixelsInOneStep = 0;
        var tissueEmissionSteps = 0;
        var maximumImpactOrigins = 0;
        for (var i = 0; i < 480; i++)
        {
            world.Step(Dt);
            maximumImpactOrigins = Math.Max(maximumImpactOrigins, world.ActiveCrumbleOriginCount);
            var emittedThisStep = Math.Max(0, world.Granular.TissuePixelCount - previousTissuePixels);
            previousTissuePixels = world.Granular.TissuePixelCount;
            maximumTissuePixelsInOneStep = Math.Max(maximumTissuePixelsInOneStep, emittedThisStep);
            if (emittedThisStep > 0) tissueEmissionSteps++;
            foreach (var body in world.Bodies.Where(body =>
                         body.IsDetachedDebris && body.ActiveParticleCount > 0 && body.ActiveParticleCount < body.Particles.Length))
            {
                progressiveErosionObserved = true;
                var convertedY = body.Particles
                    .Where((_, index) => body.IsConvertedParticle(index))
                    .Average(particle => particle.Position.Y);
                var remainingY = body.Particles
                    .Where((_, index) => !body.IsConvertedParticle(index))
                    .Average(particle => particle.Position.Y);
                contactFirstErosionObserved |= convertedY > remainingY;
            }
        }
        Assert(progressiveErosionObserved, "detached chunk converted all at once instead of eroding from its impact point");
        Assert(contactFirstErosionObserved, "detached chunk erosion did not travel upward from its ground contact");
        Assert(maximumTissuePixelsInOneStep <= 3,
            $"detached erosion emitted a visible batch of {maximumTissuePixelsInOneStep} pixels in one step");
        Assert(tissueEmissionSteps >= 20,
            $"detached erosion completed in only {tissueEmissionSteps} emission steps instead of flowing continuously");
        Assert(maximumImpactOrigins >= 2,
            $"wide detached chunk registered only {maximumImpactOrigins} breakup contact origin");
        Assert(world.DetachedChunkCount == 0, "landed chunk never completed impact-triggered granular conversion");
        Assert(world.Granular.TissuePixelCount > initialMicroPixels, "landed detached tissue did not become material pixels");
        Assert(world.Bodies.Where(body => !body.IsDetachedDebris).Sum(p => p.Particles.Length) + world.Granular.SourceTissueConvertedTotal == originalCount,
            "topology conversion lost source tissue mass");
    }

    private static void AirborneChunkRetainsShape()
    {
        var world = new BlobWorld(FlatGrid()) { Gravity = Vector2.Zero };
        var blob = BlobArchetype.Standard.Create(new Vector2(280, 180));
        world.Bodies.Add(blob);
        DamageGestureProfile.Slice(
            blob,
            blob.Center - Vector2.UnitX * blob.Radius * 1.25f,
            blob.Center + Vector2.UnitX * blob.Radius * 1.25f);
        world.Step(Dt);
        var chunk = world.Bodies.Where(body => body.IsDetachedDebris)
            .MaxBy(body => body.ActiveParticleCount)!;
        var initialContour = BlobContourBuilder.BuildShell(chunk).Points;
        Assert(chunk.Mode == SimulationMode.ShapeProxy,
            $"coherent airborne chunk was downgraded to {chunk.Mode}");
        var initialArea = MathF.Abs(BlobContourBuilder.PolygonArea(initialContour));
        var initialPerimeter = PolygonPerimeter(initialContour);
        var previousArea = initialArea;
        var maximumFrameAreaChange = 0f;
        for (var step = 0; step < 240; step++)
        {
            world.Step(Dt);
            if (chunk.ActiveParticleCount >= 4)
                Assert(chunk.Mode == SimulationMode.ShapeProxy,
                    "scheduler downgraded coherent airborne chunk to loose-fragment solving");
            var frameContour = BlobContourBuilder.BuildShell(chunk).Points;
            var frameArea = MathF.Abs(BlobContourBuilder.PolygonArea(frameContour));
            maximumFrameAreaChange = MathF.Max(maximumFrameAreaChange,
                MathF.Abs(frameArea / MathF.Max(1f, previousArea) - 1f));
            previousArea = frameArea;
        }
        var finalContour = BlobContourBuilder.BuildShell(chunk).Points;
        var finalArea = MathF.Abs(BlobContourBuilder.PolygonArea(finalContour));
        var finalPerimeter = PolygonPerimeter(finalContour);
        Assert(!chunk.IsCrumbling, "airborne chunk began granulating without an impact");
        Assert(MathF.Abs(finalArea / initialArea - 1f) < 0.035f,
            $"airborne chunk material area drifted by {MathF.Abs(finalArea / initialArea - 1f):P1}");
        Assert(MathF.Abs(finalPerimeter / initialPerimeter - 1f) < 0.035f,
            $"airborne chunk perimeter drifted by {MathF.Abs(finalPerimeter / initialPerimeter - 1f):P1}");
        Assert(maximumFrameAreaChange < 0.025f,
            $"airborne chunk changed visible area {maximumFrameAreaChange:P1} in one frame");
        foreach (var area in chunk.AreaConstraints.Where(area => !area.Broken))
        {
            var current = AreaConstraint.SignedArea(
                chunk.Particles[area.A].Position,
                chunk.Particles[area.B].Position,
                chunk.Particles[area.C].Position);
            Assert(MathF.Sign(current) == MathF.Sign(area.RestArea), "airborne chunk inverted a tissue cell into a spike");
        }
    }

    private static void CrumblingContourCannotCrown()
    {
        var blob = BlobArchetype.Standard.Create(new Vector2(240, 180));
        DamageGestureProfile.Slice(
            blob,
            blob.Center - Vector2.UnitX * blob.Radius * 1.25f,
            blob.Center + Vector2.UnitX * blob.Radius * 1.25f);
        var chunk = blob.SplitDisconnectedComponents()
            .Where(piece => piece.AreaConstraints.Any(area => !area.Broken))
            .MinBy(piece => piece.Particles.Length)!;
        chunk.MarkDetachedDebris(Dt);
        chunk.BeginCrumbling();
        Assert(chunk.ActiveParticleCount < 4 || chunk.Mode == SimulationMode.ShapeProxy,
            "coherent crumbling chunk started in loose-fragment solving");
        var world = new BlobWorld(FlatGrid()) { Gravity = Vector2.Zero };
        world.Bodies.Add(chunk);
        var maximumUnforcedAreaChange = 0f;
        var order = Enumerable.Range(0, chunk.Particles.Length)
            .OrderBy(index => chunk.Particles[index].Position.Y)
            .ThenBy(index => index)
            .ToArray();
        foreach (var particleIndex in order.Take(Math.Max(1, order.Length / 3)))
        {
            chunk.MarkParticleConverted(particleIndex);
            if (chunk.ActiveParticleCount < 3) break;
            var contour = BlobContourBuilder.BuildShell(chunk).Points;
            var previousArea = MathF.Abs(BlobContourBuilder.PolygonArea(contour));
            Assert(BlobContourBuilder.IsValid(contour), "progressive crumble produced a crossed/crowned contour");
            var sign = 0f;
            for (var i = 0; i < contour.Length; i++)
            {
                var a = contour[i];
                var b = contour[(i + 1) % contour.Length];
                var c = contour[(i + 2) % contour.Length];
                var cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
                if (MathF.Abs(cross) < 0.001f) continue;
                if (sign == 0f) sign = MathF.Sign(cross);
                Assert(MathF.Sign(cross) == sign, "crumbling contour formed a concave lattice crown");
            }
            for (var step = 0; step < 18; step++)
            {
                world.Step(Dt);
                if (chunk.ActiveParticleCount >= 4)
                    Assert(chunk.Mode == SimulationMode.ShapeProxy,
                        "scheduler downgraded coherent crumbling material to loose-fragment solving");
                var frameContour = BlobContourBuilder.BuildShell(chunk).Points;
                var frameArea = MathF.Abs(BlobContourBuilder.PolygonArea(frameContour));
                maximumUnforcedAreaChange = MathF.Max(maximumUnforcedAreaChange,
                    MathF.Abs(frameArea / MathF.Max(1f, previousArea) - 1f));
                previousArea = frameArea;
            }
        }
        Assert(maximumUnforcedAreaChange < 0.06f,
            $"crumbling chunk size-pumped {maximumUnforcedAreaChange:P1} between erosion events");
    }

    private static void CrumblingCannotLeaveGhostNodes()
    {
        var blob = BlobArchetype.Standard.Create(new Vector2(240, 180));
        DamageGestureProfile.Slice(
            blob,
            blob.Center - Vector2.UnitX * blob.Radius * 1.25f,
            blob.Center + Vector2.UnitX * blob.Radius * 1.25f);
        var chunk = blob.SplitDisconnectedComponents()
            .Where(piece => piece.AreaConstraints.Any(area => !area.Broken))
            .MinBy(piece => piece.Particles.Length)!;
        chunk.MarkDetachedDebris(Dt);
        chunk.BeginCrumbling();

        var erosionOrder = Enumerable.Range(0, chunk.Particles.Length)
            .OrderByDescending(index => chunk.IsSurfaceParticle(index))
            .ThenByDescending(index => chunk.Particles[index].Position.Y)
            .ToArray();
        foreach (var particleIndex in erosionOrder)
        {
            chunk.MarkParticleConverted(particleIndex);
            if (Enumerable.Range(0, chunk.Particles.Length).Any(chunk.IsReleasedParticle)) break;
        }

        var released = Enumerable.Range(0, chunk.Particles.Length)
            .Where(chunk.IsReleasedParticle)
            .ToArray();
        Assert(released.Length > 0, "erosion setup did not expose a cell-less boundary node");
        foreach (var particleIndex in released)
        {
            Assert(!chunk.IsPhysicalParticle(particleIndex), "released material source remained physical");
            Assert(!chunk.IsConvertedParticle(particleIndex),
                "released source lost its remaining granular material budget");
            Assert(chunk.Constraints.All(bond => bond.Broken ||
                (bond.A != particleIndex && bond.B != particleIndex)),
                "released node retained a live hanging bond");
            Assert(chunk.AreaConstraints.All(area => area.Broken ||
                (area.A != particleIndex && area.B != particleIndex && area.C != particleIndex)),
                "released node retained a live tissue cell");
        }

        for (var particleIndex = 0; particleIndex < chunk.Particles.Length; particleIndex++)
        {
            if (!chunk.IsPhysicalParticle(particleIndex)) continue;
            Assert(chunk.AreaConstraints.Any(area => !area.Broken &&
                (area.A == particleIndex || area.B == particleIndex || area.C == particleIndex)),
                "a visible/collidable node exists outside all intact material cells");
        }
        Assert(chunk.ActiveParticleCount > chunk.PhysicalParticleCount,
            "released sources were not retained for budgeted pixel emission");
    }

    private static void SleepingCutPartnersDoNotCrumble()
    {
        var world = new BlobWorld(FlatGrid()) { Gravity = Vector2.Zero };
        var blob = BlobArchetype.Standard.Create(new Vector2(300, 180));
        world.Bodies.Add(blob);
        DamageGestureProfile.Slice(
            blob,
            blob.Center - Vector2.UnitX * blob.Radius * 1.25f,
            blob.Center + Vector2.UnitX * blob.Radius * 1.25f);
        world.Step(Dt);
        Assert(world.DetachedChunkCount > 0, "horizontal cut created no detached partner");
        var coherentPartner = world.Bodies.Where(body => body.IsDetachedDebris)
            .MaxBy(body => body.ActiveParticleCount)!;
        var initialDetached = coherentPartner.ActiveParticleCount;
        Assert(initialDetached >= 4, "horizontal cut produced only micro debris");
        for (var step = 0; step < 360; step++) world.Step(Dt);
        Assert(world.DetachedChunkCount > 0,
            "same-lineage support was mistaken for a terrain landing");
        Assert(world.Bodies.Contains(coherentPartner) && coherentPartner.ActiveParticleCount == initialDetached,
            "airborne cut partner lost material without terrain contact");
    }

    private static void MultipleBlobsSeparate()
    {
        var world = new BlobWorld(FlatGrid()) { Gravity = Vector2.Zero };
        var a = new SoftBody(new Vector2(220, 160), 48, 31);
        var b = new SoftBody(new Vector2(260, 160), 48, 31);
        world.Bodies.Add(a);
        world.Bodies.Add(b);
        var before = Vector2.Distance(a.Center, b.Center);
        for (var i = 0; i < 90; i++) world.Step(Dt);
        var after = Vector2.Distance(a.Center, b.Center);
        Assert(world.BlobContactsThisStep > 0 || after > before + 8f, "blob contact solver produced no separation");
        Assert(after > before + 8f, $"overlapping blobs only separated {after - before:0.00}px (a={a.Center}, b={b.Center})");
    }

    private static void GrabTargetIsBounded()
    {
        var world = new BlobWorld(FlatGrid());
        var blob = new SoftBody(new Vector2(200, 150), 52, 37);
        world.Bodies.Add(blob);
        blob.BeginGrab(blob.Center);
        var bounded = world.ConstrainGrabTarget(blob, new Vector2(-1000f, 2000f));
        var anchor = blob.Particles[blob.GrabbedParticle].Position;
        var minProjectedX = blob.Particles.Min(p => bounded.X + p.Position.X - anchor.X - p.Radius);
        var maxProjectedY = blob.Particles.Max(p => bounded.Y + p.Position.Y - anchor.Y + p.Radius);
        var compressionAllowance = MathF.Min(blob.Radius * 0.32f, blob.ParticleSpacing * 1.55f);
        Assert(minProjectedX >= world.Grid.CellSize - compressionAllowance - 0.01f,
            "grab target exceeded its bounded wall-compression allowance");
        Assert(maxProjectedY <= (world.Grid.Rows - 1) * world.Grid.CellSize + 0.01f, "grab target projected tissue through the ground");
        var before = blob.GrabTarget;
        blob.UpdateGrabTarget(bounded, Dt);
        Assert(Vector2.Distance(before, blob.GrabTarget) <= 1900f * Dt + 0.01f, "grab target ignored manipulation speed limit");
    }

    private static void HeldBlobSquishesAgainstSideWall()
    {
        var world = new BlobWorld(FlatGrid()) { Gravity = Vector2.Zero };
        var blob = new SoftBody(new Vector2(150f, 190f), 56f, 43);
        world.Bodies.Add(blob);
        var initialWidth = blob.Particles.Max(particle => particle.Position.X + particle.Radius) -
                           blob.Particles.Min(particle => particle.Position.X - particle.Radius);
        blob.BeginGrab(blob.Center);
        var desired = world.ConstrainGrabTarget(blob, new Vector2(-1000f, blob.Center.Y));
        for (var step = 0; step < 240; step++)
        {
            blob.UpdateGrabTarget(desired, Dt);
            world.Step(Dt);
        }

        var minimumX = blob.Particles.Where((_, index) => blob.IsPhysicalParticle(index))
            .Min(particle => particle.Position.X - particle.Radius);
        var compressedWidth = blob.Particles.Where((_, index) => blob.IsPhysicalParticle(index))
                                  .Max(particle => particle.Position.X + particle.Radius) -
                              minimumX;
        Assert(minimumX >= world.Grid.CellSize - 0.01f,
            $"wall squish pushed tissue {world.Grid.CellSize - minimumX:0.00}px through the arena");
        Assert(compressedWidth < initialWidth * 0.97f,
            $"held side-wall pressure compressed width only from {initialWidth:0.0}px to {compressedWidth:0.0}px");
        Assert(compressedWidth > initialWidth * 0.68f,
            $"held side-wall pressure over-compressed width from {initialWidth:0.0}px to {compressedWidth:0.0}px");
        Assert(blob.Particles.Any(particle => particle.Contacting &&
                                                  particle.Position.X - particle.Radius <= world.Grid.CellSize + 1f),
            "side-wall contacts were not exposed to diagnostics");
    }

    private static void HeldPressureCannotEmbedBlobInFloor()
    {
        var world = new BlobWorld(FlatGrid());
        var pusher = BlobArchetype.Standard.Create(new Vector2(260f, 270f));
        var passive = BlobArchetype.Standard.Create(new Vector2(260f, 385f));
        world.Bodies.Add(pusher);
        world.Bodies.Add(passive);
        for (var step = 0; step < 180; step++) world.Step(Dt);
        static Vector2 PhysicalExtent(SoftBody body)
        {
            var physical = body.Particles.Where((_, index) => body.IsPhysicalParticle(index)).ToArray();
            return new Vector2(
                physical.Max(particle => particle.Position.X + particle.Radius) -
                physical.Min(particle => particle.Position.X - particle.Radius),
                physical.Max(particle => particle.Position.Y + particle.Radius) -
                physical.Min(particle => particle.Position.Y - particle.Radius));
        }
        var passiveBaseline = PhysicalExtent(passive);
        var maximumPassiveWidth = passiveBaseline.X;
        var minimumPassiveHeight = passiveBaseline.Y;
        pusher.BeginGrab(pusher.Center);

        var floor = (world.Grid.Rows - 1) * world.Grid.CellSize;
        var maximumFloorPenetration = 0f;
        var pusherPenetration = 0f;
        var passivePenetration = 0f;
        for (var step = 0; step < 480; step++)
        {
            var desired = world.ConstrainGrabTarget(pusher, new Vector2(pusher.Center.X, floor + 500f));
            pusher.UpdateGrabTarget(desired, Dt);
            world.Step(Dt);
            var passiveExtent = PhysicalExtent(passive);
            maximumPassiveWidth = MathF.Max(maximumPassiveWidth, passiveExtent.X);
            minimumPassiveHeight = MathF.Min(minimumPassiveHeight, passiveExtent.Y);
            foreach (var body in world.Bodies.Where(body => !body.IsDetachedDebris))
            foreach (var particle in body.Particles.Where((_, index) => body.IsPhysicalParticle(index)))
            {
                var penetration = particle.Position.Y + particle.Radius - floor;
                maximumFloorPenetration = MathF.Max(maximumFloorPenetration,
                    penetration);
                if (ReferenceEquals(body, pusher)) pusherPenetration = MathF.Max(pusherPenetration, penetration);
                if (ReferenceEquals(body, passive)) passivePenetration = MathF.Max(passivePenetration, penetration);
            }
        }

        Assert(maximumFloorPenetration <= 0.05f,
            $"blob-on-blob pressure embedded tissue {maximumFloorPenetration:0.00}px into the floor " +
            $"(pusher {pusherPenetration:0.00}, passive {passivePenetration:0.00})");
        Assert(passive.AreaRatio > 0.68f,
            $"floor-pinned passive blob collapsed to {passive.AreaRatio:P0} of its material area");
        var pressureDeformation = MathF.Max(
            maximumPassiveWidth / passiveBaseline.X - 1f,
            1f - minimumPassiveHeight / passiveBaseline.Y);
        Assert(pressureDeformation > 0.18f,
            $"terrain-safe blob pressure produced only {pressureDeformation:P1} visible passive deformation");
        var overlapping = BlobHullCollision.TryGetPenetration(pusher, passive, out _, out var finalDepth);
        var intentionalCompression = MathF.Min(pusher.ParticleSpacing, passive.ParticleSpacing) *
                                     BlobHullCollision.GrabCompressionFraction;
        Assert(!overlapping || finalDepth <= intentionalCompression + 1.35f,
            $"held and floor-supported blobs remained interlocked by {finalDepth:0.00}px");
    }

    private static void HeldPressureCannotStretchAtWall()
    {
        var world = new BlobWorld(FlatGrid());
        var held = BlobArchetype.Standard.Create(new Vector2(145f, 255f));
        var passive = BlobArchetype.Standard.Create(new Vector2(92f, 382f));
        world.Bodies.Add(held);
        world.Bodies.Add(passive);
        for (var step = 0; step < 180; step++) world.Step(Dt);

        static Vector2 Extent(SoftBody body)
        {
            var physical = body.Particles.Where((_, index) => body.IsPhysicalParticle(index)).ToArray();
            return new Vector2(
                physical.Max(particle => particle.Position.X + particle.Radius) -
                physical.Min(particle => particle.Position.X - particle.Radius),
                physical.Max(particle => particle.Position.Y + particle.Radius) -
                physical.Min(particle => particle.Position.Y - particle.Radius));
        }

        var heldBaseline = Extent(held);
        var passiveBaseline = Extent(passive);
        var maximumHeldStretch = 1f;
        var maximumPassiveStretch = 1f;
        var floor = (world.Grid.Rows - 1) * world.Grid.CellSize;
        var wall = world.Grid.CellSize;
        var maximumEnvironmentPenetration = 0f;
        held.BeginGrab(held.Center);
        for (var step = 0; step < 600; step++)
        {
            var desired = world.ConstrainGrabTarget(held, new Vector2(-500f, floor + 500f));
            held.UpdateGrabTarget(desired, Dt);
            world.Step(Dt);
            var heldExtent = Extent(held);
            var passiveExtent = Extent(passive);
            maximumHeldStretch = MathF.Max(maximumHeldStretch,
                MathF.Max(heldExtent.X / heldBaseline.X, heldExtent.Y / heldBaseline.Y));
            maximumPassiveStretch = MathF.Max(maximumPassiveStretch,
                MathF.Max(passiveExtent.X / passiveBaseline.X, passiveExtent.Y / passiveBaseline.Y));
            foreach (var body in new[] { held, passive })
            foreach (var particle in body.Particles.Where((_, index) => body.IsPhysicalParticle(index)))
                maximumEnvironmentPenetration = MathF.Max(maximumEnvironmentPenetration,
                    MathF.Max(wall - (particle.Position.X - particle.Radius),
                        particle.Position.Y + particle.Radius - floor));
        }

        Assert(maximumEnvironmentPenetration <= 0.05f,
            $"wall/floor pressure embedded a blob by {maximumEnvironmentPenetration:0.00}px");
        Assert(maximumHeldStretch < 1.48f,
            $"held blob stretched to {maximumHeldStretch:P0} of its pre-pressure extent");
        Assert(maximumPassiveStretch < 1.42f,
            $"passive blob stretched to {maximumPassiveStretch:P0} of its pre-pressure extent");
        Assert(held.AreaRatio > 0.62f && passive.AreaRatio > 0.62f,
            $"pressure collapsed material area (held {held.AreaRatio:P0}, passive {passive.AreaRatio:P0})");
    }

    private static void GrabStaysSynchronized()
    {
        var world = new BlobWorld(FlatGrid()) { Gravity = Vector2.Zero };
        var blob = new SoftBody(new Vector2(180, 170), 52, 37);
        world.Bodies.Add(blob);
        blob.BeginGrab(blob.Center);
        var desired = world.ConstrainGrabTarget(blob, new Vector2(430, 170));
        for (var step = 0; step < 30; step++)
        {
            blob.UpdateGrabTarget(desired, Dt);
            world.Step(Dt);
        }
        Assert(Vector2.Distance(blob.Particles[blob.GrabbedParticle].Position, blob.GrabTarget) < 8f,
            "grabbed tissue anchor visibly lagged behind its pointer target");
    }

    private static void ThrowRetainsMomentum()
    {
        var world = new BlobWorld(FlatGrid()) { Gravity = Vector2.Zero };
        var blob = new SoftBody(new Vector2(180, 170), 46, 31);
        world.Bodies.Add(blob);
        blob.BeginGrab(blob.Center);
        blob.EndGrab(new Vector2(700f, 0f), Dt);
        var initialSpeed = blob.AverageVelocity(Dt).Length();
        for (var step = 0; step < 60; step++) world.Step(Dt);
        var retainedSpeed = blob.AverageVelocity(Dt).Length();
        Assert(initialSpeed > 400f, $"throw transferred only {initialSpeed:0.0}px/s into the blob mass");
        Assert(retainedSpeed > initialSpeed * 0.72f,
            $"airborne throw retained only {retainedSpeed / initialSpeed:P0} of its momentum");
    }

    private static void ArenaWallsRejectWholeBlob()
    {
        var world = new BlobWorld(FlatGrid()) { Gravity = Vector2.Zero };
        var blob = new SoftBody(new Vector2(100, 180), 52, 37);
        world.Bodies.Add(blob);
        blob.ApplyTranslation(new Vector2(-95f, 0f), preserveVelocity: true);
        world.Step(Dt);
        var minimumX = blob.Particles
            .Where((_, index) => !blob.IsConvertedParticle(index))
            .Min(particle => particle.Position.X - particle.Radius);
        Assert(minimumX >= world.Grid.CellSize - 0.01f,
            $"blob remained embedded {world.Grid.CellSize - minimumX:0.00}px into the left wall");
    }

    private static void SustainedPressureDoesNotLaunch()
    {
        var world = new BlobWorld(FlatGrid()) { Gravity = Vector2.Zero };
        var pusher = BlobArchetype.Standard.Create(new Vector2(320, 330));
        var passive = BlobArchetype.Standard.Create(new Vector2(500, 330));
        world.Bodies.Add(pusher);
        world.Bodies.Add(passive);
        pusher.BeginGrab(pusher.Center);

        var maximumPassiveSpeed = 0f;
        var maximumPassiveVelocity = Vector2.Zero;
        for (var i = 0; i < 600; i++)
        {
            var target = world.ConstrainGrabTarget(pusher, new Vector2(590, 330));
            pusher.UpdateGrabTarget(target, Dt);
            world.Step(Dt);
            var passiveVelocity = passive.AverageVelocity(Dt);
            if (passiveVelocity.Length() > maximumPassiveSpeed)
            {
                maximumPassiveSpeed = passiveVelocity.Length();
                maximumPassiveVelocity = passiveVelocity;
            }
        }
        Assert(maximumPassiveSpeed < 240f,
            $"sustained pressure launched passive blob at {maximumPassiveSpeed:0.0}px/s ({maximumPassiveVelocity.X:0.0}, {maximumPassiveVelocity.Y:0.0})");
        Assert(passive.AverageVelocity(Dt).Length() < 45f, "passive blob retained launch velocity after pressure stabilized");
    }

    private static void HeldBlobPressureSquishesAndRecoils()
    {
        var world = new BlobWorld(FlatGrid()) { Gravity = Vector2.Zero };
        var pusher = BlobArchetype.Standard.Create(new Vector2(380f, 250f));
        var passive = BlobArchetype.Standard.Create(new Vector2(550f, 250f));
        world.Bodies.Add(pusher);
        world.Bodies.Add(passive);

        static Vector2 Extent(SoftBody body)
        {
            var physical = body.Particles.Where((_, index) => body.IsPhysicalParticle(index)).ToArray();
            return new Vector2(
                physical.Max(particle => particle.Position.X + particle.Radius) -
                physical.Min(particle => particle.Position.X - particle.Radius),
                physical.Max(particle => particle.Position.Y + particle.Radius) -
                physical.Min(particle => particle.Position.Y - particle.Radius));
        }

        var pusherBaseline = Extent(pusher);
        var baseline = Extent(passive);
        pusher.BeginGrab(pusher.Center);
        for (var step = 0; step < 360; step++)
        {
            pusher.UpdateGrabTarget(world.ConstrainGrabTarget(pusher, new Vector2(620f, 250f)), Dt);
            world.Step(Dt);
        }

        var pusherCompressed = Extent(pusher);
        var compressed = Extent(passive);
        var pusherCompression = MathF.Max(
            pusherCompressed.Y / pusherBaseline.Y - 1f,
            1f - pusherCompressed.X / pusherBaseline.X);
        var passiveCompression = MathF.Max(
            compressed.Y / baseline.Y - 1f,
            1f - compressed.X / baseline.X);
        var compression = MathF.Max(pusherCompression, passiveCompression);
        Assert(compression > 0.12f,
            $"held/passive bodies deformed only {pusherCompression:P1}/{passiveCompression:P1} " +
            $"(centers {pusher.Center.X:0.0}/{passive.Center.X:0.0})");
        Assert(passiveCompression > 0.06f,
            $"wall-backed passive blob yielded only {passiveCompression:P1} under another blob");
        var pressedDistance = Vector2.Distance(pusher.Center, passive.Center);

        pusher.EndGrab(Vector2.Zero, Dt);
        var peakSeparation = pressedDistance;
        var peakRecoverySpeed = 0f;
        for (var step = 0; step < 120; step++)
        {
            world.Step(Dt);
            peakSeparation = MathF.Max(peakSeparation, Vector2.Distance(pusher.Center, passive.Center));
            peakRecoverySpeed = MathF.Max(peakRecoverySpeed, passive.AverageVelocity(Dt).Length());
        }

        Assert(peakSeparation > pressedDistance + 4f,
            $"compressed blobs separated only {peakSeparation - pressedDistance:0.0}px after release");
        Assert(peakRecoverySpeed > 8f,
            $"passive blob produced only {peakRecoverySpeed:0.0}px/s of release recoil");
    }

    private static void HeldSideContactPreservesGravity()
    {
        var world = new BlobWorld(FlatGrid());
        var held = new SoftBody(new Vector2(245, 150), 46, 31);
        var passive = new SoftBody(new Vector2(332, 150), 46, 31);
        world.Bodies.Add(held);
        world.Bodies.Add(passive);
        held.BeginGrab(held.Center);
        var initialY = passive.Center.Y;

        for (var step = 0; step < 90; step++)
        {
            held.UpdateGrabTarget(world.ConstrainGrabTarget(held, held.GrabTarget), Dt);
            world.Step(Dt);
        }

        Assert(passive.Center.Y > initialY + 28f,
            $"side contact glued passive blob against gravity (fell only {passive.Center.Y - initialY:0.0}px)");
    }

    private static void RepeatedJammingCannotIntertwine()
    {
        var world = new BlobWorld(FlatGrid());
        var pusher = BlobArchetype.Standard.Create(new Vector2(330, 335));
        var pinned = BlobArchetype.Standard.Create(new Vector2(515, 335));
        world.Bodies.Add(pusher);
        world.Bodies.Add(pinned);
        pusher.BeginGrab(pusher.Center);

        var worstPenetration = 0f;
        for (var i = 0; i < 900; i++)
        {
            var desired = world.ConstrainGrabTarget(pusher, new Vector2(590, 335));
            pusher.UpdateGrabTarget(desired, Dt);
            world.Step(Dt);
            if (BlobHullCollision.TryGetPenetration(pusher, pinned, out _, out var depth))
                worstPenetration = MathF.Max(worstPenetration, depth);
        }

        var stillOverlapping = BlobHullCollision.TryGetPenetration(pusher, pinned, out _, out var finalDepth);
        var intentionalCompression = MathF.Min(pusher.ParticleSpacing, pinned.ParticleSpacing) *
                                     BlobHullCollision.GrabCompressionFraction;
        Assert(!stillOverlapping || finalDepth <= intentionalCompression + 1.25f,
            $"jammed hulls remain interpenetrated by {finalDepth:0.00}px");
        Assert(worstPenetration <= intentionalCompression + 2f,
            $"jam produced a transient {worstPenetration:0.00}px hull interpenetration");
    }

    private static void StackedBlobsSettle()
    {
        var world = new BlobWorld(FlatGrid());
        var lower = new SoftBody(new Vector2(260, 310), 46, 31);
        var upper = new SoftBody(new Vector2(260, 205), 46, 31);
        world.Bodies.Add(lower);
        world.Bodies.Add(upper);
        for (var i = 0; i < 3600; i++) world.Step(Dt);
        Assert(lower.IsSleeping, $"lower stacked blob did not sleep (speed={lower.LastAverageSpeed:0.00})");
        Assert(upper.IsSleeping, $"upper stacked blob did not sleep (speed={upper.LastAverageSpeed:0.00}, support={upper.LastSupportedParticles}, center={upper.Center}, lower={lower.Center}, area={upper.AreaRatio:0.00})");
    }

    private static void DenseBlobPileSettles()
    {
        var world = new BlobWorld(FlatGrid());
        var spawnPoints = new[]
        {
            new Vector2(190, 305), new Vector2(275, 305), new Vector2(360, 305),
            new Vector2(230, 205), new Vector2(315, 205), new Vector2(272, 110)
        };
        foreach (var point in spawnPoints) world.Bodies.Add(new SoftBody(point, 46, 31));

        var settleStep = -1;
        for (var step = 0; step < 3600; step++)
        {
            world.Step(Dt);
            if (settleStep < 0 && world.Bodies.All(body => body.IsSleeping)) settleStep = step;
        }

        var sleeping = world.Bodies.Count(body => body.IsSleeping);
        Assert(sleeping == world.Bodies.Count,
            $"only {sleeping}/{world.Bodies.Count} blobs in a dense pile reached sleep; " +
            string.Join(", ", world.Bodies.Select(body =>
                $"v={body.LastAverageSpeed:0.0}/c={body.LastCenterSpeed:0.0}/s={body.LastSupportedParticles}")));
        Assert(settleStep >= 0 && settleStep < 1800, $"dense pile took {settleStep * Dt:0.0}s to settle");
        var lateMotion = 0f;
        var awakeDuringRest = 0;
        for (var step = 0; step < 240; step++)
        {
            world.Step(Dt);
            lateMotion = MathF.Max(lateMotion, world.Bodies.Max(body => body.AverageVelocity(Dt).Length()));
            awakeDuringRest = Math.Max(awakeDuringRest, world.Bodies.Count(body => !body.IsSleeping));
        }
        Assert(lateMotion < 1f,
            $"settled pile retained {lateMotion:0.00}px/s late-frame chatter with {awakeDuringRest} bodies reawakened; " +
            string.Join(", ", world.Bodies.Select(body =>
                $"sleep={body.IsSleeping}/v={body.AverageVelocity(Dt).Length():0.0}/impact={body.LastImpact:0.0}")));
    }

    private static void GroundedBlobKeepsRolling()
    {
        var world = new BlobWorld(FlatGrid());
        var blob = new SoftBody(new Vector2(180, 340), 46, 31);
        world.Bodies.Add(blob);
        for (var step = 0; step < 900; step++) world.Step(Dt);

        var startX = blob.Center.X;
        blob.AddImpulse(new Vector2(240f, 0f), Dt);
        for (var step = 0; step < 120; step++) world.Step(Dt);
        Assert(blob.Center.X > startX + 38f,
            $"ground contact killed rolling motion after only {blob.Center.X - startX:0.0}px");
    }

    private static void BlobFallsThroughGap()
    {
        var grid = new DestructibleGrid(20, 15, 32);
        for (var x = 0; x < grid.Columns; x++)
            if (x is < 8 or > 10) grid.Set(x, grid.Rows - 1, CellMaterial.Steel);
        for (var y = 0; y < grid.Rows; y++)
        {
            grid.Set(0, y, CellMaterial.Steel);
            grid.Set(grid.Columns - 1, y, CellMaterial.Steel);
        }

        var world = new BlobWorld(grid);
        var blob = new SoftBody(new Vector2(9.5f * grid.CellSize, 350f), 46, 31);
        world.Bodies.Add(blob);
        for (var step = 0; step < 300; step++) world.Step(Dt);
        Assert(blob.Center.Y > grid.Rows * grid.CellSize,
            $"blob slept or bridged over an open gap at y={blob.Center.Y:0.0}");
        Assert(!blob.IsSleeping, "unsupported falling blob entered sleep");
    }

    private static void WoundsEmitBlood()
    {
        var world = new BlobWorld(FlatGrid()) { Gravity = Vector2.Zero };
        var blob = new SoftBody(new Vector2(200, 150), 52, 37);
        world.Bodies.Add(blob);
        blob.DamageBonds(blob.Center, blob.ParticleSpacing * 0.75f, 2f);
        world.Step(Dt);
        Assert(world.Granular.BloodCount > 0, "broken tissue emitted no blood particles");
    }

    private static void BleedingSlowsAndClots()
    {
        var world = new BlobWorld(FlatGrid()) { Gravity = Vector2.Zero };
        var blob = new SoftBody(new Vector2(200, 150), 52, 37);
        world.Bodies.Add(blob);
        var bond = blob.Constraints.First(constraint => !constraint.Broken);
        var woundPoint = (blob.Particles[bond.A].Position + blob.Particles[bond.B].Position) * 0.5f;
        blob.DamageBonds(woundPoint, 1.5f, 2f);
        world.Step(Dt);
        Assert(world.ActiveWoundCount > 0, "bond wound did not create an active wound emitter");

        var earlyBlood = 0;
        for (var i = 0; i < 120; i++)
        {
            world.Step(Dt);
            earlyBlood += world.Granular.BloodSpawnedThisStep;
        }
        for (var i = 0; i < 600; i++) world.Step(Dt);
        var lateBlood = 0;
        for (var i = 0; i < 120; i++)
        {
            world.Step(Dt);
            lateBlood += world.Granular.BloodSpawnedThisStep;
        }
        Assert(earlyBlood > lateBlood, $"bleeding did not slow (early={earlyBlood}, late={lateBlood})");
        for (var i = 0; i < 1000; i++) world.Step(Dt);
        Assert(world.ActiveWoundCount == 0, "wound did not finish clotting");
    }

    private static void BloodEmissionIsWoundLike()
    {
        var granular = new GranularMaterialSystem();
        granular.BeginStep();
        var emitted = granular.EmitBlood(
            new WoundEvent(new Vector2(100, 100), -Vector2.UnitY, 4f),
            Dt,
            20,
            speedScale: 1f);
        Assert(emitted <= GranularMaterialSystem.BloodSpawnBudgetPerStep, "blood emitter exceeded its step budget");
        var fastestUpward = granular.Particles.Min(p => ((p.Position - p.PreviousPosition) / Dt).Y);
        Assert(fastestUpward >= -72.1f, $"blood launched upward at {-fastestUpward:0.0}px/s");
    }

    private static void BloodCannotRemainInsideBlob()
    {
        var grid = FlatGrid();
        var world = new BlobWorld(grid) { Gravity = Vector2.Zero };
        var blob = BlobArchetype.Standard.Create(new Vector2(300f, 220f));
        world.Bodies.Add(blob);
        var center = blob.Center;
        for (var index = 0; index < 9; index++)
        {
            var angle = index / 9f * MathF.Tau;
            var position = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (index % 3) * 5f;
            world.Granular.Particles.Add(new GranularParticle
            {
                Position = position,
                PreviousPosition = position + new Vector2(index - 4f, 2f),
                Radius = 2f,
                Lifetime = 10f,
                Kind = GranularKind.Blood
            });
        }

        world.Step(Dt);
        var contour = BlobContourBuilder.BuildShell(blob).Points;
        Assert(world.Granular.Particles.Where(particle => particle.Kind == GranularKind.Blood)
                .All(particle => !BlobContourBuilder.ContainsPoint(contour, particle.Position)),
            "one or more blood particles remained trapped inside the visible blob contour");

        for (var step = 0; step < 120; step++) world.Step(Dt);
        contour = BlobContourBuilder.BuildShell(blob).Points;
        Assert(world.Granular.Particles.Where(particle => particle.Kind == GranularKind.Blood)
                .All(particle => !BlobContourBuilder.ContainsPoint(contour, particle.Position)),
            "ejected blood re-entered and became trapped inside blob tissue");
    }

    private static void BloodPaintsAndDriesOnTerrain()
    {
        var supportGrid = new DestructibleGrid(8, 7, 32);
        supportGrid.Set(2, 5, CellMaterial.Steel);
        supportGrid.Set(4, 5, CellMaterial.Steel);
        supportGrid.DepositBlood(2, 5, new Vector2(3.5f * 32f, 5f * 32f), -Vector2.UnitY, 0.16f);
        Assert(supportGrid.StainedCellCount == 0,
            "a stain whose actual painted coordinate was over air remained bound to its source tile");
        supportGrid.DepositBlood(2, 5, new Vector2(2.5f * 32f, 5f * 32f), -Vector2.UnitY, 0.16f);
        Assert(supportGrid.StainedCellCount == 1 && supportGrid.BloodStains[0].CellX == 2,
            "valid localized surface blood was rejected by support binding");

        var clippingGrid = new DestructibleGrid(8, 7, 32);
        clippingGrid.Set(2, 5, CellMaterial.Steel);
        clippingGrid.Set(4, 5, CellMaterial.Steel);
        var clippingWorld = new BlobWorld(clippingGrid);
        var clippingRenderer = new GameRenderer();
        using var cleanGap = new System.Drawing.Bitmap(256, 224);
        using (var graphics = System.Drawing.Graphics.FromImage(cleanGap))
            clippingRenderer.Draw(graphics, cleanGap.Size, clippingWorld, null);
        for (var deposit = 0; deposit < 12; deposit++)
            clippingGrid.DepositBlood(2, 5, new Vector2(3f * 32f - 0.25f, 5f * 32f), -Vector2.UnitY, 0.22f);
        for (var step = 0; step < 360; step++) clippingGrid.BeginStep(Dt);
        using var paintedGap = new System.Drawing.Bitmap(256, 224);
        using (var graphics = System.Drawing.Graphics.FromImage(paintedGap))
            clippingRenderer.Draw(graphics, paintedGap.Size, clippingWorld, null);
        for (var y = 5 * 32; y < 6 * 32; y += 2)
        for (var x = 3 * 32; x < 4 * 32; x += 2)
            Assert(cleanGap.GetPixel(x, y).ToArgb() == paintedGap.GetPixel(x, y).ToArgb(),
                $"persistent stain rendering changed an unsupported air pixel at ({x},{y})");

        var splatterGrid = new DestructibleGrid(12, 8, 32);
        for (var x = 0; x < splatterGrid.Columns; x++) splatterGrid.Set(x, 5, CellMaterial.Steel);
        var splatterSystem = new GranularMaterialSystem();
        splatterSystem.BeginStep();
        splatterSystem.Particles.Add(new GranularParticle
        {
            Position = new Vector2(192f, 154f),
            PreviousPosition = new Vector2(192f, 144f),
            Radius = 2.2f,
            Lifetime = 5f,
            Kind = GranularKind.Blood,
            SplatterOnImpact = true
        });
        splatterGrid.BeginStep(Dt);
        splatterSystem.Step(Dt, Vector2.Zero, splatterGrid, Array.Empty<SoftBody>());
        Assert(splatterGrid.BloodStains.Count >= 4,
            $"impact splash painted only {splatterGrid.BloodStains.Count} localized marks");
        var splashSpan = splatterGrid.BloodStains.Max(mark => mark.Position.X) -
                         splatterGrid.BloodStains.Min(mark => mark.Position.X);
        Assert(splashSpan >= 14f,
            $"impact splash covered only {splashSpan:0.0}px instead of a broad painted pattern");

        var wallGrid = new DestructibleGrid(10, 10, 32);
        for (var y = 1; y < 9; y++) wallGrid.Set(5, y, CellMaterial.Steel);
        for (var deposit = 0; deposit < 12; deposit++)
            wallGrid.DepositBlood(5, 3, new Vector2(5f * 32f, 3.5f * 32f), -Vector2.UnitX, 0.20f);
        for (var step = 0; step < 360; step++) wallGrid.BeginStep(Dt);
        Assert(wallGrid.BloodStains.Any(mark => mark.IsDrip && MathF.Abs(mark.SurfaceNormal.X) > 0.55f),
            "dense blood on an exposed wall never formed a downward wall trail");

        var wallImpactGrid = new DestructibleGrid(10, 10, 32);
        for (var y = 1; y < 9; y++) wallImpactGrid.Set(5, y, CellMaterial.Steel);
        var wallImpactSystem = new GranularMaterialSystem();
        wallImpactSystem.BeginStep();
        wallImpactSystem.Particles.Add(new GranularParticle
        {
            Position = new Vector2(157.6f, 112f),
            PreviousPosition = new Vector2(157.35f, 112f),
            Radius = 2.2f,
            Lifetime = 5f,
            Kind = GranularKind.Blood,
            SplatterOnImpact = true
        });
        wallImpactGrid.BeginStep(Dt);
        wallImpactSystem.Step(Dt, Vector2.Zero, wallImpactGrid, Array.Empty<SoftBody>());
        Assert(wallImpactSystem.BloodSplatteredThisStep == 1,
            "a designated moderate side-wall impact bounced instead of splattering");
        var paintedWallMarks = wallImpactGrid.BloodStains
            .Where(mark => MathF.Abs(mark.SurfaceNormal.X) > 0.65f)
            .ToArray();
        Assert(paintedWallMarks.Length >= 4,
            $"side-wall splash painted only {paintedWallMarks.Length} localized marks");
        var wallSplashSpan = paintedWallMarks.Max(mark => mark.Position.Y) -
                             paintedWallMarks.Min(mark => mark.Position.Y);
        Assert(wallSplashSpan >= 14f,
            $"side-wall splash covered only {wallSplashSpan:0.0}px vertically");

        var bouncingWallSystem = new GranularMaterialSystem();
        bouncingWallSystem.BeginStep();
        bouncingWallSystem.Particles.Add(new GranularParticle
        {
            Position = new Vector2(157.6f, 208f),
            PreviousPosition = new Vector2(157.1f, 208f),
            Radius = 2.2f,
            Lifetime = 5f,
            Kind = GranularKind.Blood,
            SplatterOnImpact = false
        });
        wallImpactGrid.BeginStep(Dt);
        bouncingWallSystem.Step(Dt, Vector2.Zero, wallImpactGrid, Array.Empty<SoftBody>());
        Assert(bouncingWallSystem.BloodSplatteredThisStep == 0 && bouncingWallSystem.Particles.Count == 1,
            "an ordinary moderate wall droplet splattered instead of remaining in the bouncing majority");
        Assert(wallImpactGrid.BloodStains.Any(mark =>
                MathF.Abs(mark.SurfaceNormal.X) > 0.65f && MathF.Abs(mark.Position.Y - 208f) < 6f),
            "an ordinary bouncing wall droplet left no localized wall stain");

        var symmetricWallGrid = new DestructibleGrid(12, 10, 32);
        for (var y = 0; y < symmetricWallGrid.Rows; y++)
        {
            symmetricWallGrid.Set(0, y, CellMaterial.Steel);
            symmetricWallGrid.Set(symmetricWallGrid.Columns - 1, y, CellMaterial.Steel);
        }
        var symmetricWallWorld = new BlobWorld(symmetricWallGrid);
        var symmetricWallRenderer = new GameRenderer();
        var symmetricWallSystem = new GranularMaterialSystem();
        using var cleanWalls = new System.Drawing.Bitmap(384, 320);
        using (var graphics = System.Drawing.Graphics.FromImage(cleanWalls))
            symmetricWallRenderer.Draw(graphics, cleanWalls.Size, symmetricWallWorld, null);

        AddWallBurst(true);
        AddWallBurst(false);
        using var firstWallPass = new System.Drawing.Bitmap(384, 320);
        using (var graphics = System.Drawing.Graphics.FromImage(firstWallPass))
            symmetricWallRenderer.Draw(graphics, firstWallPass.Size, symmetricWallWorld, null);
        var leftFirstPixels = ChangedWallPixels(cleanWalls, firstWallPass, 0, 32);
        var rightFirstPixels = ChangedWallPixels(cleanWalls, firstWallPass, 352, 384);
        Assert(leftFirstPixels > 20 && rightFirstPixels > 20,
            $"wall splash was asymmetric: left={leftFirstPixels} pixels, right={rightFirstPixels} pixels");
        var firstRatio = Math.Min(leftFirstPixels, rightFirstPixels) /
                         (float)Math.Max(leftFirstPixels, rightFirstPixels);
        Assert(firstRatio > 0.45f,
            $"left/right wall coverage diverged by too much ({leftFirstPixels} vs {rightFirstPixels})");

        AddWallBurst(true);
        AddWallBurst(false);
        using var secondWallPass = new System.Drawing.Bitmap(384, 320);
        using (var graphics = System.Drawing.Graphics.FromImage(secondWallPass))
            symmetricWallRenderer.Draw(graphics, secondWallPass.Size, symmetricWallWorld, null);
        var leftRepeatPixels = ChangedWallPixels(firstWallPass, secondWallPass, 0, 32);
        var rightRepeatPixels = ChangedWallPixels(firstWallPass, secondWallPass, 352, 384);
        Assert(leftRepeatPixels > 8 && rightRepeatPixels > 8,
            $"repeat spray was visually absorbed: left={leftRepeatPixels}, right={rightRepeatPixels}");

        void AddWallBurst(bool left)
        {
            symmetricWallSystem.BeginStep();
            for (var particleIndex = 0; particleIndex < 9; particleIndex++)
            {
                var positionX = left ? 34.4f : 349.6f;
                var previousX = left ? 35.4f : 348.6f;
                symmetricWallSystem.Particles.Add(new GranularParticle
                {
                    Position = new Vector2(positionX, 66f + particleIndex * 10f),
                    PreviousPosition = new Vector2(previousX, 66f + particleIndex * 10f),
                    Radius = 2.2f,
                    Lifetime = 5f,
                    Kind = GranularKind.Blood,
                    SplatterOnImpact = particleIndex % 3 == 0
                });
            }
            symmetricWallGrid.BeginStep(Dt);
            symmetricWallSystem.Step(Dt, Vector2.Zero, symmetricWallGrid, Array.Empty<SoftBody>());
        }

        static int ChangedWallPixels(
            System.Drawing.Bitmap before,
            System.Drawing.Bitmap after,
            int minimumX,
            int maximumX)
        {
            var changed = 0;
            for (var y = 0; y < after.Height; y += 2)
            for (var x = minimumX; x < maximumX; x += 2)
                if (before.GetPixel(x, y).ToArgb() != after.GetPixel(x, y).ToArgb()) changed++;
            return changed;
        }

        var grid = new DestructibleGrid(10, 8, 32);
        for (var x = 0; x < grid.Columns; x++) grid.Set(x, 5, CellMaterial.Steel);
        var granular = new GranularMaterialSystem();
        granular.BeginStep();
        granular.EmitBlood(
            new WoundEvent(new Vector2(144f, 95f), Vector2.UnitY, 1f),
            Dt,
            18,
            1f);
        var splatterCandidates = granular.Particles.Count(particle => particle.SplatterOnImpact);
        Assert(splatterCandidates is > 0 and < 9,
            $"splatter mixture selected {splatterCandidates} of 18 blood pixels instead of a minority");

        var impactSplatters = 0;
        for (var step = 0; step < 360; step++)
        {
            grid.BeginStep(Dt);
            granular.BeginStep();
            granular.Step(Dt, new Vector2(0f, 980f), grid, Array.Empty<SoftBody>());
            impactSplatters += granular.BloodSplatteredThisStep;
        }
        Assert(impactSplatters > 0, "splatter-designated blood never converted into impact paint");
        Assert(grid.StainedCellCount > 0, "simulated blood struck terrain without painting it");
        var stained = grid.BloodStains.ToArray();
        Assert(stained.Length > 0 && stained.Any(mark => mark.Wetness > 0f),
            "fresh terrain blood has no wet phase");
        var initialStainTotal = stained.Sum(mark => mark.Amount);
        var initialWetness = stained.Max(mark => mark.Wetness);

        for (var step = 0; step < 1800; step++) grid.BeginStep(Dt);
        var dried = grid.BloodStains.ToArray();
        Assert(dried.Length > 0, "blood stain vanished instead of drying persistently");
        Assert(dried.Max(mark => mark.Wetness) < initialWetness,
            "terrain blood never transitioned from wet to dry");
        Assert(dried.Sum(mark => mark.Amount) <= initialStainTotal + 0.001f,
            "total stain mass grew without additional blood");

        var renderGrid = new DestructibleGrid(10, 8, 32);
        renderGrid.Set(5, 4, CellMaterial.Concrete);
        var renderWorld = new BlobWorld(renderGrid);
        var renderer = new GameRenderer();
        using var clean = new System.Drawing.Bitmap(320, 256);
        using (var graphics = System.Drawing.Graphics.FromImage(clean))
            renderer.Draw(graphics, clean.Size, renderWorld, null);
        renderGrid.DepositBlood(5, 4, new Vector2(176f, 128f), -Vector2.UnitY, 0.18f);
        using var painted = new System.Drawing.Bitmap(320, 256);
        using (var graphics = System.Drawing.Graphics.FromImage(painted))
            renderer.Draw(graphics, painted.Size, renderWorld, null);
        var changedPixels = 0;
        for (var y = 4 * 32; y < 5 * 32; y += 2)
        for (var x = 5 * 32; x < 6 * 32; x += 2)
            if (clean.GetPixel(x, y).ToArgb() != painted.GetPixel(x, y).ToArgb()) changedPixels++;
        Assert(changedPixels is >= 3 and <= 45,
            $"localized blood changed {changedPixels} sampled pixels instead of only its impact spot");
        Assert(clean.GetPixel(190, 156).ToArgb() == painted.GetPixel(190, 156).ToArgb(),
            "localized blood recolored an untouched corner of the terrain tile");

        for (var deposit = 0; deposit < 12; deposit++)
            renderGrid.DepositBlood(5, 4, new Vector2(176f, 129f), -Vector2.UnitY, 0.22f);
        for (var step = 0; step < 360; step++) renderGrid.BeginStep(Dt);
        var proceduralDrips = renderGrid.BloodStains.Where(mark => mark.IsDrip).ToArray();
        Assert(proceduralDrips.Length > 0,
            "dense wet blood never created a gravity-driven surface trail");
        Assert(proceduralDrips.All(mark => MathF.Abs(mark.Position.Y - 129f) < 2f),
            "2.5D drip origin was shifted below the actual pooled surface");
        Assert(proceduralDrips.Length >= 2,
            $"a heavily pooled stain produced only {proceduralDrips.Length} runoff trail");
        var visualExtents = proceduralDrips
            .Select(mark => mark.VisibleTrailLength)
            .ToArray();
        Assert(visualExtents.Max() - visualExtents.Min() > 2.5f,
            "dense-pool trails converged to one uniform visual size");
        for (var first = 0; first < proceduralDrips.Length; first++)
        for (var second = first + 1; second < proceduralDrips.Length; second++)
            Assert(MathF.Abs(proceduralDrips[first].Position.X - proceduralDrips[second].Position.X) >= 5f,
                "dense-pool runoff lanes collapsed into the same thin origin");

        var dripDrops = new List<BloodDripEmission>();
        for (var step = 0; step < 900; step++)
        {
            renderGrid.BeginStep(Dt);
            renderGrid.DrainBloodDrops(dripDrops);
        }
        Assert(dripDrops.Count is > 0 and < 24,
            $"active trails emitted {dripDrops.Count} transient pixels instead of occasional drips");
        Assert(dripDrops.Select(drop => MathF.Round(drop.Radius * 10f)).Distinct().Count() > 1 ||
               dripDrops.Select(drop => MathF.Round(drop.Velocity.Y)).Distinct().Count() > 1,
            "falling trail pixels had no size or speed variation");

        var multiCellGrid = new DestructibleGrid(7, 8, 32);
        for (var y = 2; y <= 5; y++) multiCellGrid.Set(3, y, CellMaterial.Steel);
        var multiCellWorld = new BlobWorld(multiCellGrid);
        var multiCellRenderer = new GameRenderer();
        using var cleanMultiCell = new System.Drawing.Bitmap(224, 256);
        using (var graphics = System.Drawing.Graphics.FromImage(cleanMultiCell))
            multiCellRenderer.Draw(graphics, cleanMultiCell.Size, multiCellWorld, null);
        // Use dispersed contacts like real blood particles instead of repeatedly
        // depositing one exact coordinate, which would merge into one artificial
        // super-mark and fail to exercise the production pooling planner.
        for (var deposit = 0; deposit < 48; deposit++)
        {
            var localX = 3f + (deposit % 9) * 3.2f;
            multiCellGrid.DepositBlood(
                3,
                2,
                new Vector2(3f * 32f + localX, 2f * 32f),
                -Vector2.UnitY,
                0.18f + (deposit % 4) * 0.015f);
        }
        for (var step = 0; step < 720; step++) multiCellGrid.BeginStep(Dt);
        var longestTrail = multiCellGrid.BloodStains
            .Where(mark => mark.IsDrip)
            .Select(mark => mark.VisibleTrailLength)
            .DefaultIfEmpty(0f)
            .Max();
        Assert(longestTrail > multiCellGrid.CellSize * 2f + 2f,
            $"severely pooled blood produced only a {longestTrail:0.0}px trail instead of crossing two cells");
        using var paintedMultiCell = new System.Drawing.Bitmap(224, 256);
        using (var graphics = System.Drawing.Graphics.FromImage(paintedMultiCell))
            multiCellRenderer.Draw(graphics, paintedMultiCell.Size, multiCellWorld, null);
        var changedThirdCellPixels = 0;
        for (var y = 4 * 32 + 2; y < 5 * 32; y += 2)
        for (var x = 3 * 32; x < 4 * 32; x += 2)
            if (cleanMultiCell.GetPixel(x, y).ToArgb() != paintedMultiCell.GetPixel(x, y).ToArgb())
                changedThirdCellPixels++;
        Assert(changedThirdCellPixels > 0,
            "heavy pooled runoff did not visibly cross two connected cells");
        Assert(renderGrid.StainedCellCount <= DestructibleGrid.PersistentStainSoftLimit,
            "ordinary terrain painting exceeded its coalescing threshold");
    }

    private static void DriedStainsWaitForCleaning()
    {
        var grid = new DestructibleGrid(8, 7, 32);
        grid.Set(3, 5, CellMaterial.Steel);
        var position = new Vector2(3.5f * 32f, 5f * 32f);
        grid.DepositBlood(3, 5, position, -Vector2.UnitY, 0.05f);
        var original = grid.BloodStains.Single();
        for (var step = 0; step < 18000; step++) grid.BeginStep(Dt);
        Assert(grid.BloodStains.Count == 1,
            "terrain stain auto-deleted after drying instead of waiting for cleaning");
        var dried = grid.BloodStains[0];
        Assert(dried.Wetness <= 0.001f && dried.Amount >= 0.01f &&
               Vector2.DistanceSquared(dried.Position, original.Position) < 0.001f,
            "terrain stain did not remain as stable dry pigment");

        var conveyor = new ConveyorBelt(new Vector2(100f, 100f), 320f, 38f, 0f);
        conveyor.DepositBlood(conveyor.Position + new Vector2(90f, 0f), -Vector2.UnitY, 0.05f);
        for (var step = 0; step < 18000; step++) conveyor.Step(Dt);
        Assert(conveyor.BloodStains.Count >= 1 &&
               conveyor.BloodStains.All(mark => mark.Wetness <= 0.001f) &&
               conveyor.BloodStains.Sum(mark => mark.Amount) >= 0.01f,
            "conveyor stain auto-deleted after drying instead of waiting for cleaning");
    }

    private static void FreshBloodDiversifiesDriedRunoff()
    {
        var grid = new DestructibleGrid(12, 8, 32);
        for (var x = 3; x <= 7; x++) grid.Set(x, 5, CellMaterial.Steel);
        var origin = new Vector2(5.5f * 32f, 5f * 32f);
        for (var deposit = 0; deposit < 18; deposit++)
            grid.DepositBlood(5, 5, origin + new Vector2((deposit % 5 - 2) * 1.5f, 0f), -Vector2.UnitY, 0.16f);
        for (var step = 0; step < 480; step++) grid.BeginStep(Dt);
        for (var step = 0; step < 18000; step++) grid.BeginStep(Dt);
        var before = grid.BloodStains.Where(mark => mark.IsDrip)
            .Select(mark => (mark.Position, mark.Radius))
            .ToArray();
        Assert(before.Length > 0 && grid.BloodStains.Where(mark => mark.IsDrip).All(mark => mark.Wetness <= 0.001f),
            "test runoff did not fully dry before the fresh spill");

        for (var deposit = 0; deposit < 18; deposit++)
            grid.DepositBlood(5, 5, origin + new Vector2((deposit % 5 - 2) * 1.5f, 0f), -Vector2.UnitY, 0.16f);
        for (var step = 0; step < 240; step++) grid.BeginStep(Dt);

        var after = grid.BloodStains.Where(mark => mark.IsDrip).ToArray();
        var createdNewLane = after.Any(mark => mark.Wetness > 0.12f &&
            before.All(old => MathF.Abs(old.Position.X - mark.Position.X) >= 5f));
        var widenedOldLane = after.Any(mark => mark.Wetness > 0.12f && before.Any(old =>
            MathF.Abs(old.Position.X - mark.Position.X) < 2f && mark.Radius >= old.Radius + 0.75f));
        Assert(createdNewLane || widenedOldLane,
            "fresh blood merely switched an old dry trail back on without rerouting or widening it");
        Assert(before.All(old => after.Any(mark => Vector2.DistanceSquared(mark.Position, old.Position) < 0.01f)),
            "dynamic rerouting removed a persistent dried trail");
    }

    private static void TerrainRunoffStaysInFrontOfTileInteriors()
    {
        var grid = new DestructibleGrid(7, 8, 32);
        for (var y = 2; y <= 6; y++) grid.Set(3, y, CellMaterial.Steel);
        var world = new BlobWorld(grid);
        var renderer = new GameRenderer();
        using var clean = new Bitmap(224, 256);
        using (var graphics = Graphics.FromImage(clean))
            renderer.Draw(graphics, clean.Size, world, null);

        for (var deposit = 0; deposit < 48; deposit++)
        {
            var localX = 4f + deposit % 7 * 3.6f;
            grid.DepositBlood(
                3,
                2,
                new Vector2(3f * 32f + localX, 2f * 32f),
                -Vector2.UnitY,
                0.18f);
        }
        for (var step = 0; step < 720; step++) grid.BeginStep(Dt);
        var leader = grid.BloodStains.Where(mark => mark.IsDrip)
            .MaxBy(mark => mark.VisibleTrailLength);
        Assert(leader.IsDrip && leader.VisibleTrailLength > 32f,
            "test pooling did not create runoff across a tile interior");

        using var painted = new Bitmap(224, 256);
        using (var graphics = Graphics.FromImage(painted))
            renderer.Draw(graphics, painted.Size, world, null);
        var startY = (int)MathF.Round(leader.Position.Y);
        var endY = Math.Min(7 * grid.CellSize - 1,
            (int)MathF.Floor(leader.Position.Y + leader.VisibleTrailLength));
        for (var y = startY; y <= endY; y += 2)
        {
            var visibleOnRow = false;
            var minimumX = Math.Max(3 * grid.CellSize, (int)leader.Position.X - 18);
            var maximumX = Math.Min(4 * grid.CellSize - 1, (int)leader.Position.X + 18);
            for (var x = minimumX; x <= maximumX; x++)
            {
                if (clean.GetPixel(x, y).ToArgb() == painted.GetPixel(x, y).ToArgb()) continue;
                visibleOnRow = true;
                break;
            }
            Assert(visibleOnRow,
                $"runoff vanished behind the tile interior at y={y}");
        }
    }

    private static void WallBloodSurvivesChurnAndDrips()
    {
        var grid = new DestructibleGrid(80, 30, 16);
        for (var y = 0; y < grid.Rows; y++)
        {
            grid.Set(0, y, CellMaterial.Steel);
            grid.Set(grid.Columns - 1, y, CellMaterial.Steel);
        }
        for (var y = 10; y < grid.Rows; y += 2)
        for (var x = 1; x < grid.Columns - 1; x++)
            grid.Set(x, y, CellMaterial.Concrete);

        var deposits = 0;
        for (var y = 10; y < grid.Rows && deposits < 700; y += 2)
        for (var x = 1; x < grid.Columns - 1 && deposits < 700; x++)
        {
            grid.DepositBlood(
                x,
                y,
                new Vector2(x * grid.CellSize + 8f, y * grid.CellSize),
                -Vector2.UnitY,
                0.06f);
            deposits++;
        }

        for (var deposit = 0; deposit < 18; deposit++)
        {
            var positionY = 48f + (deposit % 6) * 12f;
            var cellY = (int)(positionY / grid.CellSize);
            grid.DepositBlood(
                0,
                cellY,
                new Vector2(grid.CellSize, positionY),
                Vector2.UnitX,
                0.065f);
            grid.DepositBlood(
                grid.Columns - 1,
                cellY,
                new Vector2((grid.Columns - 1) * grid.CellSize, positionY),
                -Vector2.UnitX,
                0.065f);
        }

        for (var step = 0; step < 520; step++)
        {
            grid.BeginStep(Dt);
            var platformY = 10 + (step / 78 % 10) * 2;
            var platformX = 1 + step % 78;
            var localX = 2f + (step % 3) * 5f;
            grid.DepositBlood(
                platformX,
                platformY,
                new Vector2(platformX * grid.CellSize + localX, platformY * grid.CellSize),
                -Vector2.UnitY,
                0.06f);
        }

        var leftWall = grid.BloodStains
            .Where(mark => mark.SurfaceNormal.X > 0.55f)
            .ToArray();
        var rightWall = grid.BloodStains
            .Where(mark => mark.SurfaceNormal.X < -0.55f)
            .ToArray();
        Assert(leftWall.Any(mark => !mark.IsDrip) && rightWall.Any(mark => !mark.IsDrip),
            "wall stains were evicted while newer floor blood churned the capped layer");
        Assert(leftWall.Any(mark => mark.IsDrip) && rightWall.Any(mark => mark.IsDrip),
            "dense protected wall blood never matured into runoff on both wall orientations");
        grid.DepositBlood(
            0,
            5,
            new Vector2(grid.CellSize, 5f * grid.CellSize + 0.2f),
            Vector2.Normalize(new Vector2(0.52f, -0.48f)),
            0.08f);
        grid.DepositBlood(
            grid.Columns - 1,
            5,
            new Vector2((grid.Columns - 1) * grid.CellSize, 5f * grid.CellSize + 0.2f),
            Vector2.Normalize(new Vector2(-0.52f, -0.48f)),
            0.08f);
        Assert(grid.BloodStains.Any(mark => mark.SurfaceNormal == Vector2.UnitX && mark.CellY == 5) &&
               grid.BloodStains.Any(mark => mark.SurfaceNormal == -Vector2.UnitX && mark.CellY == 5),
            "wall-seam impacts retained diagonal normals instead of becoming persistent wall stains");
        Assert(grid.StainedCellCount <= DestructibleGrid.PersistentStainSoftLimit + grid.Columns * 3,
            "persistent wall runoff failed to coalesce after the soft performance threshold");
    }

    private static void ActiveBloodTrailsSurviveStainChurn()
    {
        var grid = new DestructibleGrid(80, 20, 16);
        for (var y = 1; y < grid.Rows; y += 2)
        for (var x = 0; x < grid.Columns; x++)
            grid.Set(x, y, CellMaterial.Concrete);

        var trailOrigin = new Vector2(10 * grid.CellSize + 8f, 3 * grid.CellSize + 1f);
        for (var deposit = 0; deposit < 12; deposit++)
            grid.DepositBlood(10, 3, trailOrigin, -Vector2.UnitY, 0.22f);
        for (var step = 0; step < 20; step++) grid.BeginStep(Dt);
        var originalTrail = grid.BloodStains.FirstOrDefault(mark => mark.IsDrip);
        Assert(originalTrail.IsDrip, "test pooling did not establish an active 2.5D trail");

        var deposits = 0;
        for (var y = 1; y < grid.Rows && deposits < 700; y += 2)
        for (var x = 0; x < grid.Columns && deposits < 700; x++)
        {
            if (x == 10 && y == 3) continue;
            var position = new Vector2(x * grid.CellSize + 8f, y * grid.CellSize + 1f);
            grid.DepositBlood(x, y, position, -Vector2.UnitY, 0.06f);
            deposits++;
        }

        Assert(grid.StainedCellCount <= DestructibleGrid.PersistentStainSoftLimit + grid.Columns * 3,
            "persistent stain churn failed to coalesce after the soft performance threshold");
        Assert(grid.BloodStains.Any(mark => mark.IsDrip &&
                   Vector2.DistanceSquared(mark.Position, originalTrail.Position) < 4f),
            "active 2.5D trail was evicted by newer flat splatter");
    }

    private static void SaturatedBloodZoneRenewsRunoff()
    {
        var grid = new DestructibleGrid(80, 20, 16);
        for (var y = 1; y < grid.Rows; y += 2)
        for (var x = 0; x < grid.Columns; x++)
            grid.Set(x, y, CellMaterial.Concrete);

        var targetX = 10;
        var targetY = 3;
        var target = new Vector2(targetX * grid.CellSize + 8f, targetY * grid.CellSize);
        for (var deposit = 0; deposit < 12; deposit++)
            grid.DepositBlood(targetX, targetY, target, -Vector2.UnitY, 0.18f);
        for (var step = 0; step < 900; step++) grid.BeginStep(Dt);

        var deposits = 0;
        for (var y = 1; y < grid.Rows && deposits < 720; y += 2)
        for (var x = 0; x < grid.Columns && deposits < 720; x++)
        {
            if (x == targetX && y == targetY) continue;
            var position = new Vector2(x * grid.CellSize + 2f + deposits % 3 * 5f, y * grid.CellSize);
            grid.DepositBlood(x, y, position, -Vector2.UnitY, 0.06f);
            deposits++;
        }

        for (var deposit = 0; deposit < 12; deposit++)
            grid.DepositBlood(
                targetX,
                targetY,
                target + new Vector2((deposit % 5 - 2) * 1.5f, 0f),
                -Vector2.UnitY,
                0.08f);
        for (var step = 0; step < 120; step++) grid.BeginStep(Dt);

        Assert(grid.BloodStains.Any(mark => !mark.IsDrip && mark.CellX == targetX && mark.CellY == targetY &&
                                           mark.Wetness > 0.45f),
            "repeat blood did not re-wet the old saturated surface zone");
        Assert(grid.BloodStains.Any(mark => mark.IsDrip && MathF.Abs(mark.Position.X - target.X) < 24f &&
                                           mark.Wetness > 0.2f),
            "repeat blood did not restart 2.5D runoff in the old surface zone");
        Assert(grid.StainedCellCount <= DestructibleGrid.PersistentStainSoftLimit + grid.Columns * 3,
            "renewing an old blood zone failed to coalesce after the soft performance threshold");
    }

    private static void SettledBloodPoolKeepsStaining()
    {
        var grid = new DestructibleGrid(80, 20, 16);
        for (var y = 1; y < grid.Rows; y += 2)
        for (var x = 0; x < grid.Columns; x++)
            grid.Set(x, y, CellMaterial.Concrete);

        const int targetStartX = 10;
        const int targetEndX = 15;
        var deposits = 0;
        for (var y = 1; y < grid.Rows && deposits < 720; y += 2)
        for (var x = 0; x < grid.Columns && deposits < 720; x++)
        {
            if (y == grid.Rows - 1 && x >= targetStartX - 1 && x <= targetEndX + 1) continue;
            grid.DepositBlood(
                x,
                y,
                new Vector2(x * grid.CellSize + 2f + deposits % 3 * 5f, y * grid.CellSize),
                -Vector2.UnitY,
                0.06f);
            deposits++;
        }
        Assert(grid.StainedCellCount >= DestructibleGrid.PersistentStainSoftLimit,
            "test did not cross the stain coalescing threshold before the new physical pool arrived");

        var world = new BlobWorld(grid);
        var floorY = (grid.Rows - 1) * grid.CellSize;
        for (var i = 0; i < 42; i++)
        {
            var radius = 1.45f + i % 3 * 0.15f;
            var position = new Vector2(
                targetStartX * grid.CellSize + 3f + i % 18 * 4.3f,
                floorY - radius);
            world.Granular.Particles.Add(new GranularParticle
            {
                Position = position,
                PreviousPosition = position,
                Radius = radius,
                Lifetime = 30f,
                RestFrames = 32,
                Kind = GranularKind.Blood
            });
        }
        for (var step = 0; step < 360; step++) world.Step(Dt);

        var targetMarks = grid.BloodStains.Where(mark =>
            mark.CellY == grid.Rows - 1 &&
            mark.CellX >= targetStartX - 1 && mark.CellX <= targetEndX + 1).ToArray();
        Assert(targetMarks.Any(mark => !mark.IsDrip && mark.Wetness > 0.4f),
            "settled physical blood did not keep staining the previously clean floor");
        Assert(targetMarks.Any(mark => mark.IsDrip && mark.VisibleTrailLength > 12f),
            "settled physical blood did not create visible 2.5D runoff on the saturated layer");
        Assert(grid.StainedCellCount <= DestructibleGrid.PersistentStainSoftLimit + grid.Columns * 3,
            "settled-pool seepage failed to coalesce after the soft performance threshold");
    }

    private static void DenseGranularPilesBecomeForegroundSpills()
    {
        var sparseChance =
            GranularMaterialSystem.ForegroundTransitionChanceForDensity(1);
        var moderateChance =
            GranularMaterialSystem.ForegroundTransitionChanceForDensity(10);
        var piledChance =
            GranularMaterialSystem.ForegroundTransitionChanceForDensity(40);
        Assert(sparseChance > 0f &&
               moderateChance > sparseChance &&
               piledChance > moderateChance * 8f,
            "foreground selection was not occasional at low density and strongly pile-weighted");

        var grid = new DestructibleGrid(40, 22, 32);
        var world = new BlobWorld(grid) { Gravity = Vector2.Zero };
        const int physicalMatterCount = 72;
        for (var i = 0; i < physicalMatterCount; i++)
        {
            var position = new Vector2(
                385f + i % 6 * 2.2f,
                180f + i / 6 % 4 * 2.1f);
            world.Granular.Particles.Add(new GranularParticle
            {
                Position = position,
                PreviousPosition = position,
                Radius = 1.8f + i % 3 * 0.2f,
                Lifetime = 30f,
                RestFrames = 40,
                ForegroundSupportFrames = 18,
                Kind = (i & 1) == 0 ? GranularKind.Blood : GranularKind.Tissue,
                Appearance = (GranularAppearance)(i % 3)
            });
        }
        const int acidCount = 28;
        for (var i = 0; i < acidCount; i++)
        {
            var position = new Vector2(710f + i % 5 * 2f, 180f + i / 5 * 2f);
            world.Granular.Particles.Add(new GranularParticle
            {
                Position = position,
                PreviousPosition = position,
                Radius = 2f,
                Lifetime = 30f,
                RestFrames = 40,
                Kind = GranularKind.Acid
            });
        }

        for (var step = 0; step < 72; step++) world.Step(Dt);

        var spills = world.Granular.ForegroundSpills;
        var remainingPhysicalMatter = world.Granular.Particles.Count(particle =>
            particle.Kind is GranularKind.Blood or GranularKind.Tissue);
        Assert(spills.Count > 0,
            "dense physical matter never transitioned into a foreground fall");
        Assert(spills.Any(spill => spill.Kind == GranularKind.Blood) &&
               spills.Any(spill => spill.Kind == GranularKind.Tissue),
            "foreground overflow did not preserve both blood and tissue material");
        Assert(spills.All(spill => MathF.Abs(spill.Position.X - 392f) < 30f),
            "foreground overflow drifted away from the local pile X position");
        Assert(world.Granular.Particles.Count(particle => particle.Kind == GranularKind.Acid) == acidCount,
            "acid was incorrectly converted by the blood/tissue overflow tier");
        Assert(world.Granular.ForegroundSpillConvertedTotal ==
               physicalMatterCount - remainingPhysicalMatter,
            "foreground transition silently lost or manufactured physical pixels");
        Assert(spills.Count <= GranularMaterialSystem.ForegroundSpillCapacity,
            "foreground spill representation exceeded its hard performance cap");

        var leadingY = spills.Max(spill => spill.Position.Y);
        for (var step = 0; step < 18; step++) world.Step(Dt);
        Assert(world.Granular.ForegroundSpills.Max(spill => spill.Position.Y) > leadingY + 25f,
            "foreground overflow did not visibly fall down the screen");

        using var baseline = new Bitmap(1280, 720);
        using var baselineGraphics = Graphics.FromImage(baseline);
        var renderWorld = new BlobWorld(new DestructibleGrid(40, 22, 32));
        var renderer = new GameRenderer();
        renderer.Draw(baselineGraphics, baseline.Size, renderWorld, null);
        renderWorld.Granular.ForegroundSpills.Add(new ForegroundGranularSpill
        {
            Position = new Vector2(500f, 400f),
            Velocity = new Vector2(0f, 320f),
            Radius = 2.8f,
            Lifetime = 2f,
            Kind = GranularKind.Tissue,
            Appearance = GranularAppearance.BlobMint,
            Variation = 31
        });
        using var rendered = new Bitmap(1280, 720);
        using var renderedGraphics = Graphics.FromImage(rendered);
        renderer.Draw(renderedGraphics, rendered.Size, renderWorld, null);
        var changedPixels = 0;
        for (var y = 360; y <= 410; y++)
        for (var x = 485; x <= 515; x++)
            if (rendered.GetPixel(x, y).ToArgb() != baseline.GetPixel(x, y).ToArgb())
                changedPixels++;
        Assert(changedPixels is > 0 and <= 36,
            $"one foreground source rendered as {changedPixels} pixels instead of one particle-sized square");

        var basinGrid = new DestructibleGrid(40, 22, 32);
        basinGrid.BuildProcessingStation();
        basinGrid.OpenContinuousConveyorPortals();
        var line = new ProcessingLine(
            DestructibleGrid.ProcessingDeckRow * basinGrid.CellSize,
            continuousFlow: true);
        var basinWorld = new BlobWorld(basinGrid)
        {
            ProcessingLine = line,
            Gravity = Vector2.Zero
        };
        var basinX = line.Basin.Left + line.Basin.Width * 0.45f;
        var surfaceY = line.Basin.SurfaceYAt(basinX);
        basinWorld.Granular.ForegroundSpills.Add(new ForegroundGranularSpill
        {
            Position = new Vector2(basinX, surfaceY - 9f),
            Velocity = new Vector2(0f, 330f),
            Radius = 2.2f,
            Lifetime = 2f,
            Kind = GranularKind.Blood,
            Variation = 17
        });
        for (var step = 0; step < 24; step++) basinWorld.Step(Dt);
        Assert(basinWorld.Granular.ForegroundSpills.Count == 0 &&
               basinWorld.Granular.ForegroundSpillCollectedTotal == 1,
            "a foreground blood pixel above the basin did not enter the basin");
        Assert(line.Basin.StoredVolume > 0f && line.Basin.TotalDeposited > 0f,
            "foreground blood entered the basin visually without increasing authoritative blood");
        Assert(basinWorld.Granular.Particles.Count == 0,
            "basin collection manufactured a replacement granular pixel");

        line.Basin.AddMaterial(
            basinX,
            line.Basin.RemainingCapacity,
            0f,
            0f);
        var fullStoredVolume = line.Basin.StoredVolume;
        var overflowBefore = line.Basin.TotalOverflowed;
        basinWorld.Granular.ForegroundSpills.Add(new ForegroundGranularSpill
        {
            Position = new Vector2(
                basinX,
                line.Basin.SurfaceYAt(basinX) - 4f),
            Velocity = new Vector2(0f, 330f),
            Radius = 2.2f,
            Lifetime = 2f,
            Kind = GranularKind.Blood,
            Variation = 29
        });
        for (var step = 0; step < 8; step++) basinWorld.Step(Dt);
        Assert(basinWorld.Granular.ForegroundSpills.Count == 0 &&
               basinWorld.Granular.ForegroundSpillReemittedTotal == 1 &&
               basinWorld.Granular.Particles.Count == 1 &&
               MathF.Abs(line.Basin.StoredVolume - fullStoredVolume) < 0.001f &&
               line.Basin.TotalOverflowed > overflowBefore,
            "a full basin did not return the same foreground pixel to physical overflow");
        var reemitted = basinWorld.Granular.Particles[0];
        var reemittedVelocity =
            (reemitted.Position - reemitted.PreviousPosition) / Dt;
        Assert((reemitted.Position.X < line.Basin.Left ||
                reemitted.Position.X > line.Basin.Right) &&
               MathF.Abs(reemittedVelocity.X) > 20f,
            "full-basin foreground blood remained a steady 2.5D diagonal instead of ejecting physically");
        Assert(line.Basin.FrontOverflowStains.Count == 0,
            "foreground full-basin impact recreated a front-glass trail");
    }

    private static void ConveyorCarriesBlob()
    {
        var grid = new DestructibleGrid(30, 18, 32);
        var world = new BlobWorld(grid);
        var conveyor = new ConveyorBelt(new Vector2(130f, 330f), 420f, 38f, 130f);
        world.Conveyors.Add(conveyor);
        var blob = BlobArchetype.Standard.Create(new Vector2(245f, 220f));
        world.Bodies.Add(blob);
        var initialX = blob.Center.X;
        for (var step = 0; step < 260; step++) world.Step(Dt);
        Assert(blob.Center.X > initialX + 55f,
            $"positive conveyor moved blob only {blob.Center.X - initialX:0.0}px");
        Assert(blob.Particles.Max(particle => particle.Position.Y + particle.Radius) <= conveyor.Position.Y + 2.5f,
            "blob penetrated through the conveyor top");
        Assert(blob.AverageVelocity(Dt).Y < 80f, "conveyor launched blob vertically");

        var beforeReverse = blob.Center.X;
        conveyor.Reverse();
        for (var step = 0; step < 220; step++) world.Step(Dt);
        Assert(blob.Center.X < beforeReverse - 25f, "reversed conveyor did not reverse blob transport");

        var oldPosition = conveyor.Position;
        conveyor.DepositBlood(oldPosition + new Vector2(80f, 1f), -Vector2.UnitY, 0.12f);
        Assert(conveyor.BloodStains.Count == 1, "conveyor rejected localized blood paint");
        var localMark = conveyor.BloodStains[0].Position;
        conveyor.Move(new Vector2(24f, -12f), 960f, 576f);
        Assert(Vector2.Distance(conveyor.BloodStains[0].Position, localMark) < 0.001f,
            "moving conveyor detached its stain from conveyor-local space");
        var oldWidth = conveyor.Width;
        conveyor.Resize(40f, 8f, 960f, 576f);
        conveyor.ChangeSpeed(30f);
        Assert(conveyor.Width > oldWidth && conveyor.Height > 38f,
            "runtime conveyor resize controls did not change its dimensions");
        Assert(MathF.Abs(conveyor.Speed + 100f) < 0.01f,
            "runtime conveyor speed control produced the wrong speed");
        Assert(conveyor.HitEditHandle(conveyor.Position + new Vector2(conveyor.Width * 0.5f, conveyor.Height * 0.5f)) ==
               ConveyorEditHandle.Move, "conveyor body is not mouse-draggable");
        Assert(conveyor.HitEditHandle(conveyor.Position + new Vector2(conveyor.Width, conveyor.Height * 0.5f)) ==
               ConveyorEditHandle.Length, "conveyor length handle is not mouse-draggable");
        Assert(conveyor.HitEditHandle(conveyor.Position + new Vector2(conveyor.Width * 0.5f, conveyor.Height)) ==
               ConveyorEditHandle.Height, "conveyor height handle is not mouse-draggable");

        var loopBelt = new ConveyorBelt(new Vector2(100f, 100f), 220f, 40f, 120f);
        loopBelt.DepositBlood(loopBelt.Position + new Vector2(45f, 0f), -Vector2.UnitY, 0.10f);
        var startingLoopCoordinate = loopBelt.BloodStains[0].LoopCoordinate;
        for (var step = 0; step < 260; step++) loopBelt.Step(Dt);
        var circulated = loopBelt.BloodStains[0];
        Assert(MathF.Abs(circulated.LoopCoordinate - startingLoopCoordinate) > 80f,
            "conveyor-local blood did not advance with the moving belt material");
        Assert(circulated.SurfaceNormal.Y > 0.45f || MathF.Abs(circulated.SurfaceNormal.X) > 0.45f,
            "conveyor stain remained on the top instead of circulating around an end/return run");
        var dripBelt = new ConveyorBelt(new Vector2(100f, 100f), 220f, 40f, 120f);
        for (var deposit = 0; deposit < 14; deposit++)
            dripBelt.DepositBlood(dripBelt.Position + new Vector2(70f, 0f), -Vector2.UnitY, 0.20f);
        for (var step = 0; step < 90 && dripBelt.DripEmitters.Count == 0; step++)
            dripBelt.Step(Dt);
        Assert(dripBelt.DripEmitters.Count > 0,
            "dense wet blood on the conveyor never established a falling drip point");
        for (var step = 0; step < 30 && dripBelt.TransientDrops.Count == 0; step++)
            dripBelt.Step(Dt);
        Assert(dripBelt.TransientDrops.Count > 0,
            "conveyor drip point never released a falling blood pixel");
        Assert(dripBelt.BloodStains.All(mark => !mark.IsDrip),
            "conveyor created a persistent paint trail when only falling droplets were requested");
        var stationaryDripX = dripBelt.DripEmitters[0].LocalX;
        var stationaryDripVariation = dripBelt.DripEmitters[0].Variation;
        var stationaryDripCount = dripBelt.DripEmitters.Count;
        var movingFlatCoordinate = dripBelt.BloodStains.First(mark => !mark.IsDrip).LoopCoordinate;
        for (var step = 0; step < 120; step++) dripBelt.Step(Dt);
        var sameDripPoint = dripBelt.DripEmitters.First(emitter =>
            emitter.Variation == stationaryDripVariation);
        Assert(MathF.Abs(sameDripPoint.LocalX - stationaryDripX) < 0.01f,
            $"conveyor drip point followed the moving tread instead of staying at its origin " +
            $"({stationaryDripX:0.0} -> {sameDripPoint.LocalX:0.0})");
        Assert(dripBelt.DripEmitters.Count == stationaryDripCount,
            "one moving belt stain repeatedly created drip points along the conveyor length");
        Assert(dripBelt.BloodStains.Any(mark => !mark.IsDrip &&
            MathF.Abs(mark.LoopCoordinate - movingFlatCoordinate) > 20f),
            "stationary runoff fix accidentally stopped ordinary belt pigment from circulating");
        for (var step = 0; step < 960 && dripBelt.TransientDrops.Count == 0; step++) dripBelt.Step(Dt);
        Assert(dripBelt.TransientDrops.Count > 0, "dense conveyor pool never shed an occasional underside pixel");
        var transientPosition = dripBelt.TransientDrops[0].Position;
        var transientLifetime = dripBelt.TransientDrops[0].Lifetime;
        for (var step = 0; step < 8; step++) dripBelt.Step(Dt);
        Assert(dripBelt.TransientDrops.Count <= 6, "conveyor exceeded its transient blood-pixel cap");
        Assert(dripBelt.TransientDrops.Count == 0 ||
               (dripBelt.TransientDrops[0].Position.Y > transientPosition.Y &&
                dripBelt.TransientDrops[0].Lifetime < transientLifetime),
            "conveyor underside pixel became a persistent staining trail");
    }

    private static void MassDamageRemainsBudgeted()
    {
        var world = new BlobWorld(FlatGrid()) { Gravity = Vector2.Zero };
        for (var i = 0; i < 10; i++)
        {
            var body = BlobArchetype.Standard.Create(new Vector2(115 + (i % 5) * 100, 100 + (i / 5) * 170));
            world.Bodies.Add(body);
            var center = body.Center;
            body.DamageLine(center - Vector2.UnitY * body.Radius, center + Vector2.UnitY * body.Radius, 4f, 2f);
        }

        world.Step(Dt);
        Assert(world.TopologySplitsThisStep <= 4, "topology work exceeded the per-step body budget");
        Assert(world.Granular.BloodSpawnedThisStep <= GranularMaterialSystem.BloodSpawnBudgetPerStep,
            "mass wounds exceeded the blood spawn budget");
        Assert(world.ActiveWoundCount <= 128, "mass wounds exceeded the active-wound cap");

        var maximumSpawned = world.Granular.SpawnedThisStep;
        for (var i = 0; i < 240; i++)
        {
            world.Step(Dt);
            maximumSpawned = Math.Max(maximumSpawned, world.Granular.SpawnedThisStep);
        }
        Assert(maximumSpawned <= GranularMaterialSystem.BloodSpawnBudgetPerStep + GranularMaterialSystem.TissueSpawnBudgetPerStep,
            $"one step spawned {maximumSpawned} material pixels");
        Assert(world.Granular.Particles.Count <= GranularMaterialSystem.ParticleCapacity, "granular system exceeded its global cap");
    }

    private static void ContinuousFlowUsesSingleAutomaticLine()
    {
        var line = new ProcessingLine(480f, powered: true, continuousFlow: true);
        Assert(line.ContinuousFlowMode, "continuous-flow mode was not retained");
        Assert(line.Belts.Count == 1, "continuous flow did not replace segmented transfers with one belt");
        Assert(line.Belts[0].Position.X < 0f && line.Belts[0].Position.X + line.Belts[0].Width > 1280f,
            "continuous belt does not extend beyond both screen edges");
        Assert(!line.HitCrusherButton(line.CrusherButtonCenter),
            "continuous-flow machinery still accepted manual button input");

        var body = BlobArchetype.ProcessingUnit.Create(new Vector2(line.Bays[0].CenterX, line.DeckY - 30f));
        var bodies = new List<SoftBody> { body };
        var granular = new List<GranularParticle>();
        for (var i = 0; i < 480; i++)
            line.PreStep(bodies, granular, Dt);
        Assert(line.LockedBody is null && line.CrusherTravel < 0.001f,
            "removed machinery still captured or processed a passing blob");

        line.RegisterDoorwayBlood(new Vector2(line.DoorwayBounds.Left, line.DoorwayBounds.Top + 20f),
            new Vector2(line.DoorwayBounds.Left - 2f, line.DoorwayBounds.Top + 20f), 2f, 140f);
        Assert(line.DoorwayStains.Count == 0,
            "continuous portal recorded a legacy stain in unsupported doorway air");
        Assert(line.ProcessedCount == 0,
            "a blob created inside the factory was counted without entering through the left portal");

        var entering = BlobArchetype.ProcessingUnit.Create(Vector2.Zero);
        var entryY = line.DeckY - entering.Radius - 4f;
        entering.ApplyTranslation(new Vector2(-entering.Radius - 8f, entryY) - entering.Center,
            preserveVelocity: false);
        bodies.Add(entering);
        line.PreStep(bodies, granular, Dt);
        Assert(line.ProcessedCount == 0, "processed counter advanced before the blob entered the factory");
        entering.ApplyTranslation(new Vector2(32f + entering.Radius, entryY) - entering.Center,
            preserveVelocity: false);
        line.PreStep(bodies, granular, Dt);
        Assert(line.ProcessedCount == 0,
            "untouched left-portal entry incorrectly earned processed credit");
        DamageGestureProfile.Bite(entering, entering.Center);
        line.PreStep(bodies, granular, Dt);
        Assert(line.ProcessedCount == 1,
            "an entered blob's first real damage did not increment the processed counter");
        DamageGestureProfile.Bite(entering, entering.Center + Vector2.UnitX * 2f);
        line.PreStep(bodies, granular, Dt);
        Assert(line.ProcessedCount == 1, "one entering lineage incremented the processed counter twice");

        var oneHit = BlobArchetype.ProcessingUnit.Create(Vector2.Zero);
        oneHit.ApplyTranslation(new Vector2(-oneHit.Radius - 8f, entryY) - oneHit.Center,
            preserveVelocity: false);
        bodies.Add(oneHit);
        line.PreStep(bodies, granular, Dt);
        oneHit.ApplyTranslation(new Vector2(32f + oneHit.Radius, entryY) - oneHit.Center,
            preserveVelocity: false);
        line.PreStep(bodies, granular, Dt);
        var oneHitCenter = oneHit.Center;
        oneHit.DamageLine(
            oneHitCenter - Vector2.UnitX * oneHit.Radius,
            oneHitCenter + Vector2.UnitX * oneHit.Radius,
            oneHit.Radius * 2f,
            100f,
            maximumBreaks: int.MaxValue);
        line.ObserveProcessedDamage(bodies);
        bodies.Remove(oneHit); // Emulate lethal topology cleanup in the same fixed tick.
        Assert(line.ProcessedCount == 2,
            "a lethal one-hit weapon did not credit its entered lineage before body removal");

        var exiting = BlobArchetype.ProcessingUnit.Create(new Vector2(1340f, line.DeckY - 28f));
        bodies.Add(exiting);
        line.PreStep(bodies, granular, Dt);
        Assert(line.ProcessedCount == 2,
            "a blob leaving through the right portal incorrectly incremented the processed counter");
    }

    private static void OverheadTubeStagesAndReleasesBodies()
    {
        var feed = new OverheadTubeFeed { MaximumBodiesInFactory = 1 };
        var world = new BlobWorld(FlatGrid()) { TubeFeed = feed, Gravity = Vector2.Zero };
        var sawHiddenReturn = false;
        var sawConveyorEntry = false;
        var sawVisibleVerticalTransfer = false;
        for (var i = 0; i < 1800; i++)
        {
            feed.Update(world.Bodies, Dt, BlobArchetype.ProcessingUnit.Create);
            if (world.Bodies.Count > 0)
            {
                var body = world.Bodies[0];
                sawHiddenReturn |= feed.IsInHiddenReturn(body);
                sawConveyorEntry |= feed.IsEnteringConveyor(body);
                var conveyorHeight = feed.DeckY - body.Radius - 4f;
                if (body.Center.X + body.Radius > 0f &&
                    body.Center.Y > OverheadTubeFeed.GlassBottom + 8f &&
                    body.Center.Y < conveyorHeight - 8f)
                    sawVisibleVerticalTransfer = true;
            }
            world.Step(Dt);
            if (world.Bodies.Count > 0 && sawConveyorEntry && feed.BodiesInTube == 0) break;
        }
        Assert(world.Bodies.Count == 1, "ceiling tube did not create exactly one bounded factory body");
        Assert(feed.BodiesInTube == 0, "ceiling tube never released its staged body");
        Assert(sawHiddenReturn && sawConveyorEntry,
            "ceiling tube skipped the hidden return plumbing before conveyor entry");
        Assert(!sawVisibleVerticalTransfer,
            "tube body visibly descended from the ceiling instead of transferring fully offscreen");
        Assert(world.Bodies[0].Center.X <= 100f && world.Bodies[0].Center.Y >= 420f,
            "ceiling tube did not exit offscreen left and reintroduce the body behind the belt wall");
    }

    private static void ContinuousConveyorContainsCommittedTissue()
    {
        var belt = new ConveyorBelt(new Vector2(-64f, 480f), 1408f, 26f, 120f,
            minimumWidth: 96f, systemControlled: true);
        var particle = new Particle
        {
            Position = new Vector2(640f, 548f),
            PreviousPosition = new Vector2(640f, 472f),
            Radius = 5f
        };
        var contact = belt.ResolveParticle(ref particle, Dt, applyBeltVelocity: true,
            forceTopContainment: true);
        Assert(contact.Hit && contact.IsTop && particle.Supported,
            "committed conveyor particle was not restored as top-supported contact");
        Assert(MathF.Abs(particle.Position.Y - 475f) < 0.01f,
            $"committed tissue remained beneath the belt at y={particle.Position.Y:0.0}");
        Assert(particle.PreviousPosition.Y <= particle.Position.Y + 0.01f,
            "containment preserved downward velocity that could immediately tunnel again");
    }

    private static void SpawnedGoreCanBypassConveyors()
    {
        var granular = new GranularMaterialSystem();
        granular.BeginStep();
        var emitted = granular.EmitBlood(
            new WoundEvent(new Vector2(320f, 440f), Vector2.UnitY, 1f),
            Dt,
            GranularMaterialSystem.BloodSpawnBudgetPerStep,
            1f);
        var bypassBlood = granular.Particles.Count(particle =>
            particle.Kind == GranularKind.Blood && particle.BypassConveyors);
        Assert(emitted == GranularMaterialSystem.BloodSpawnBudgetPerStep &&
               bypassBlood > 0 && bypassBlood < emitted / 2,
            $"spawned blood did not retain a small deterministic conveyor-bypass fraction ({bypassBlood}/{emitted})");

        var tissueSystem = new GranularMaterialSystem();
        tissueSystem.BeginStep();
        var detached = BlobArchetype.ProcessingUnit.Create(new Vector2(380f, 420f));
        Assert(tissueSystem.TryEmitDetached(detached, Dt),
            "detached tissue could not be converted for conveyor-bypass verification");
        var tissueCount = tissueSystem.Particles.Count;
        var bypassTissue = tissueSystem.Particles.Count(particle => particle.BypassConveyors);
        Assert(tissueCount > 0 && bypassTissue > 0 && bypassTissue < tissueCount / 2,
            $"spawned tissue did not retain a small deterministic conveyor-bypass fraction ({bypassTissue}/{tissueCount})");
    }

    private static void OverheadTubeBlobsTumbleWithoutInteraction()
    {
        var feed = new OverheadTubeFeed { MaximumBodiesInFactory = 1 };
        var world = new BlobWorld(FlatGrid()) { TubeFeed = feed, Gravity = Vector2.Zero };
        while (world.Bodies.Count == 0)
            feed.Update(world.Bodies, Dt, BlobArchetype.ProcessingUnit.Create);
        var body = world.Bodies[0];
        var particleIndex = -1;
        for (var i = 0; i < body.Particles.Length; i++)
            if (body.IsPhysicalParticle(i))
            {
                particleIndex = i;
                break;
            }
        Assert(particleIndex >= 0, "tube body had no physical particle for rotation verification");
        var before = body.Particles[particleIndex].Position - body.Center;
        var minimumCenterY = body.Center.Y;
        var maximumCenterY = body.Center.Y;
        for (var step = 0; step < 48; step++)
        {
            feed.Update(world.Bodies, Dt, BlobArchetype.ProcessingUnit.Create);
            world.Step(Dt);
            minimumCenterY = MathF.Min(minimumCenterY, body.Center.Y);
            maximumCenterY = MathF.Max(maximumCenterY, body.Center.Y);
        }
        var after = body.Particles[particleIndex].Position - body.Center;
        var cross = before.X * after.Y - before.Y * after.X;
        Assert(MathF.Abs(cross) > 0.4f && maximumCenterY - minimumCenterY > 0.25f,
            "distributed airflow did not visibly tumble and buffet the staged tube body");

        Assert(world.PickBody(body.Center) is null, "tube body remained mouse-pickable");

        var tool = new PhysicalKnife(body.Center - new Vector2(40f, 0f));
        Assert(tool.BeginGrab(tool.Position), "tube interaction regression could not take the cleaver");
        var linksBefore = body.BrokenLinkCount;
        for (var step = 0; step < 20; step++)
        {
            tool.SetGrabTarget(body.Center - new Vector2(35f - step * 4f, 0f));
            tool.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(), world.Bodies, 1280f, 720f, feed);
        }
        Assert(tool.BlobContactsThisStep == 0 && body.BrokenLinkCount == linksBefore,
            "cleaver physically contacted or damaged a blob still inside the air tube");
        var toolUppermost = MathF.Min(
            MathF.Min(tool.HandleStart.Y - 7f, tool.HandleEnd.Y - 7f),
            MathF.Min(tool.BladeCoreStart.Y - 12f, tool.BladeCoreEnd.Y - 12f));
        Assert(toolUppermost >= OverheadTubeFeed.GlassBottom - 0.01f,
            "carried cleaver crossed through the tube's lower glass face");

        var external = BlobArchetype.ProcessingUnit.Create(new Vector2(420f, 98f));
        world.Bodies.Add(external);
        world.Step(Dt);
        Assert(external.Particles.Where((_, index) => external.IsPhysicalParticle(index))
                .All(particle => particle.Position.Y - particle.Radius >= OverheadTubeFeed.GlassBottom - 0.01f),
            "ordinary blob matter entered the sealed tube interior");
    }

    private static void OverheadTubeGlassPreservesHardImpactDamage()
    {
        var slingFeed = new OverheadTubeFeed();
        var sling = new PhysicalKnife(new Vector2(320f, 300f));
        sling.SelectArsenalVisual(8);
        Assert(sling.Equip(sling.Position, sling.Position),
            "tube-impact fixture could not equip its slingshot");
        var launched = BlobArchetype.ProcessingUnit.Create(sling.Position);
        var slingWorld = new BlobWorld(FlatGrid())
        {
            Gravity = Vector2.Zero,
            TubeFeed = slingFeed,
            Knife = sling
        };
        slingWorld.Bodies.Add(launched);
        Assert(sling.BeginPrimaryAction(),
            "tube-impact fixture could not charge its slingshot");
        for (var step = 0; step < 135; step++) slingWorld.Step(Dt);
        sling.EndPrimaryAction();
        var slingDamageBefore = launched.BrokenLinkCount;
        var maximumGlassImpact = 0f;
        for (var step = 0; step < 120 &&
             launched.BrokenLinkCount == slingDamageBefore; step++)
        {
            slingWorld.Step(Dt);
            maximumGlassImpact = MathF.Max(
                maximumGlassImpact, launched.LastTerrainImpact);
        }
        Assert(maximumGlassImpact > 430f,
            $"tube glass did not report the slingshot's hard impact ({maximumGlassImpact:0.0})");
        Assert(launched.BrokenLinkCount > slingDamageBefore,
            "slingshot blob bounced from tube glass without its normal impact splatter");

        var freeze = new PhysicalKnife(new Vector2(420f, 300f));
        freeze.SelectArsenalVisual(17);
        Assert(freeze.Equip(freeze.Position, freeze.Position),
            "tube-impact fixture could not equip its freeze ray");
        var frozen = BlobArchetype.ProcessingUnit.Create(new Vector2(280f, 300f));
        Assert(freeze.BeginPrimaryAction(),
            "tube-impact fixture could not fire its freeze ray");
        freeze.EndPrimaryAction();
        for (var step = 0; step < 80; step++)
            freeze.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
                new[] { frozen }, 640f, 480f);
        Assert(freeze.FrozenBlobs.Count == 1,
            "freeze ray did not establish the ice state before the tube impact");

        var freezeFeed = new OverheadTubeFeed();
        var freezeWorld = new BlobWorld(FlatGrid())
        {
            Gravity = Vector2.Zero,
            TubeFeed = freezeFeed,
            Knife = freeze
        };
        freezeWorld.Bodies.Add(frozen);
        frozen.ApplyTranslation(
            new Vector2(320f, 230f) - frozen.Center,
            preserveVelocity: false);
        frozen.AddImpulse(new Vector2(0f, -820f), Dt);
        var frozenDamageBefore = frozen.BrokenLinkCount;
        for (var step = 0; step < 90 &&
             frozen.BrokenLinkCount == frozenDamageBefore; step++)
            freezeWorld.Step(Dt);
        Assert(frozen.BrokenLinkCount > frozenDamageBefore &&
               freeze.FrozenBlobs.Any(state => state.PendingSplitPropagation),
            "frozen blob bounced from tube glass without shattering like a wall impact");
    }

    private static void ContinuousBeltUsesWallPortals()
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        grid.OpenContinuousConveyorPortals();
        for (var y = 12; y <= 16; y++)
            Assert(!grid.Cell(0, y).IsSolid && !grid.Cell(grid.Columns - 1, y).IsSolid,
                $"continuous conveyor portal row {y} remained physically blocked");
        Assert(grid.Cell(0, 11).IsSolid && grid.Cell(0, 17).IsSolid &&
               grid.Cell(grid.Columns - 1, 11).IsSolid && grid.Cell(grid.Columns - 1, 17).IsSolid,
            "continuous portal opening leaked beyond its authored wall rim");
    }

    private static void KnifePokesPhysicalTissue()
    {
        var knife = new PhysicalKnife(new Vector2(320f, 300f));
        var body = BlobArchetype.ProcessingUnit.Create(new Vector2(286f, 350f));
        var originalCenter = body.Center;
        Assert(knife.Equip(knife.Position, knife.Position), "cleaver test could not equip the tool from its rack");
        var sawContact = false;
        var sawCut = false;
        var sawHeavyImpact = false;
        var cursor = knife.Position;
        Assert(knife.BeginPrimaryAction(), "equipped cleaver rejected its primary action");
        for (var step = 0; step < 250; step++)
        {
            knife.SetGrabTarget(cursor);
            knife.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(), new[] { body }, 1280f, 720f);
        }
        Assert(knife.IsCharging && knife.WindupStrength >= 0.80f,
            "held primary action did not charge a strong cleaver swing");
        Assert(knife.EndPrimaryAction(), "releasing primary action did not commit the charged swing");
        for (var step = 0; step < 58; step++)
        {
            knife.SetGrabTarget(cursor);
            knife.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(), new[] { body }, 1280f, 720f);
            sawContact |= knife.BlobContactsThisStep > 0;
            sawCut |= knife.PuncturedThisStep;
            sawHeavyImpact |= knife.HeavyImpactActive;
        }
        Assert(sawContact, "cleaver passed through tissue without physical blade contact");
        Assert(sawCut, "visible cutting edge never produced a localized cleaver strike");
        Assert(sawHeavyImpact,
            "fully charged cutting-edge strike produced no heavy-impact effect state");
        Assert(body.BrokenLinkCount >= 2,
            $"cleaver edge did not produce meaningfully strong local damage ({body.BrokenLinkCount} links)");
        Assert(knife.BloodStains.Count > 0, "successful cleaver cut left its cutting edge perfectly clean");
        Assert(body.Center.Y > originalCenter.Y + 0.5f,
            "cleaver contact did not transfer readable physical displacement into the blob");
    }

    private static void CleaverFacesMovementAndOnlyEdgeDamages()
    {
        var knife = new PhysicalKnife(new Vector2(100f, 360f));
        Assert(knife.HandleStart.X > knife.Position.X && knife.BladeEdgeEnd.X < knife.Position.X,
            "cleaver was not constructed blade-left and handle-right around its grip pivot");
        Assert(knife.BladeEdgeStart.Y > knife.BladeCoreStart.Y,
            "cleaver cutting edge was not on the long side opposite its spine");
        Assert(knife.Equip(knife.Position, knife.Position), "cleaver direction test could not equip the tool");
        Assert(knife.BladeEdgeEnd.Y < knife.HandleEnd.Y - 20f,
            "equipped cleaver did not begin in its blade-up ready pose");

        var cursor = knife.Position;
        var previousAngle = knife.Angle;
        Assert(knife.BeginPrimaryAction(), "cleaver direction test could not begin charging");
        for (var step = 0; step < 20; step++)
        {
            knife.SetGrabTarget(cursor);
            knife.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(), Array.Empty<SoftBody>(), 1280f, 720f);
            Assert(MathF.Abs(knife.Angle - previousAngle) <= 0.12f,
                "cleaver exceeded its capped physical angular speed");
            previousAngle = knife.Angle;
        }
        Assert(knife.IsCharging && knife.WindupStrength > 0.20f && !knife.PrimaryChargeVisible,
            "quick click threshold did not build a hidden initial cleaver charge");

        var minimumChargedAngle = float.MaxValue;
        var maximumChargedAngle = float.MinValue;
        var minimumChargedX = float.MaxValue;
        var maximumChargedX = float.MinValue;
        for (var step = 0; step < 2; step++)
        {
            knife.SetGrabTarget(cursor);
            knife.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(), Array.Empty<SoftBody>(), 1280f, 720f);
        }
        Assert(knife.PrimaryChargeVisible && knife.PrimaryCharge >= 0.25f && knife.PrimaryCharge <= 0.32f,
            "fast charge bar did not reveal near one-quarter fill");
        for (var step = 0; step < 82; step++)
        {
            knife.SetGrabTarget(cursor);
            knife.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(), Array.Empty<SoftBody>(), 1280f, 720f);
            if (knife.WindupStrength >= 0.999f)
            {
                minimumChargedAngle = MathF.Min(minimumChargedAngle, knife.Angle);
                maximumChargedAngle = MathF.Max(maximumChargedAngle, knife.Angle);
                minimumChargedX = MathF.Min(minimumChargedX, knife.Position.X);
                maximumChargedX = MathF.Max(maximumChargedX, knife.Position.X);
            }
        }
        Assert(knife.IsCharging && knife.WindupStrength >= 0.999f &&
               (maximumChargedAngle - minimumChargedAngle > 0.003f ||
                maximumChargedX - minimumChargedX > 0.20f),
            "fully charged cleaver had no visible physical shake cue");

        Assert(knife.EndPrimaryAction(), "charged cleaver did not release into a swing");
        var clickReleaseAngle = knife.Angle;
        for (var step = 0; step < 6; step++)
        {
            knife.SetGrabTarget(cursor);
            knife.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(), Array.Empty<SoftBody>(), 1280f, 720f);
        }
        Assert(knife.ControlState == CleaverControlState.Swing && knife.ChopDirection.Y > 0.85f,
            "primary-action release did not commit a downward assisted swing");
        Assert(MathF.Abs(knife.Angle - clickReleaseAngle) > 1.0f,
            "left-click swing remained visually sluggish after release");

        var recoveryKnife = new PhysicalKnife(new Vector2(620f, 280f));
        Assert(recoveryKnife.Equip(recoveryKnife.Position, recoveryKnife.Position) &&
               recoveryKnife.BeginPrimaryAction() && recoveryKnife.EndPrimaryAction(),
            "recovery timing test could not start a click swing");
        for (var step = 0; step < 40 && recoveryKnife.ControlState != CleaverControlState.Recovery; step++)
        {
            recoveryKnife.SetGrabTarget(recoveryKnife.Position);
            recoveryKnife.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
                Array.Empty<SoftBody>(), 1280f, 720f);
        }
        Assert(recoveryKnife.ControlState == CleaverControlState.Recovery,
            "click swing never entered its recovery phase");
        Assert(recoveryKnife.BeginRotationAdjust(recoveryKnife.Position),
            "right-drag rotation was rejected during melee recovery");
        Assert(recoveryKnife.ControlState == CleaverControlState.Carry,
            "rotation override did not cancel recovery into an editable carry state");
        recoveryKnife.UpdateRotationAdjust(recoveryKnife.Position + new Vector2(60f, 0f));
        recoveryKnife.EndRotationAdjust();
        var recoverySteps = 0;
        while (recoveryKnife.ControlState == CleaverControlState.Recovery && recoverySteps < 12)
        {
            recoveryKnife.SetGrabTarget(recoveryKnife.Position);
            recoveryKnife.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
                Array.Empty<SoftBody>(), 1280f, 720f);
            recoverySteps++;
        }
        Assert(recoveryKnife.ControlState == CleaverControlState.Carry && recoverySteps == 0,
            $"click swing recovery was not nearly instant ({recoverySteps} fixed steps)");

        var bufferedKnife = new PhysicalKnife(new Vector2(620f, 280f));
        Assert(bufferedKnife.Equip(bufferedKnife.Position, bufferedKnife.Position) &&
               bufferedKnife.BeginPrimaryAction() &&
               bufferedKnife.EndPrimaryAction(),
            "buffered-charge fixture could not start its first swing");
        for (var step = 0;
             step < 40 &&
             bufferedKnife.ControlState != CleaverControlState.Recovery;
             step++)
            bufferedKnife.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
                Array.Empty<SoftBody>(), 1280f, 720f);
        Assert(bufferedKnife.ControlState == CleaverControlState.Recovery &&
               bufferedKnife.BeginPrimaryAction(),
            "held LMB during recovery was not accepted as a buffered charge");
        for (var step = 0;
             step < 20 && bufferedKnife.ControlState != CleaverControlState.Windup;
             step++)
            bufferedKnife.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
                Array.Empty<SoftBody>(), 1280f, 720f);
        Assert(bufferedKnife.IsCharging,
            "buffered LMB did not begin charging on the first post-recovery step");
        bufferedKnife.EndPrimaryAction();

        var orientedKnife = new PhysicalKnife(new Vector2(620f, 280f));
        Assert(orientedKnife.Equip(orientedKnife.Position, orientedKnife.Position),
            "relative-arc cleaver test could not equip the tool");
        Assert(orientedKnife.BeginRotationAdjust(orientedKnife.Position),
            "relative-arc cleaver test could not begin rotation");
        orientedKnife.UpdateRotationAdjust(orientedKnife.Position + new Vector2(60f, 0f));
        orientedKnife.EndRotationAdjust();
        Assert(orientedKnife.BeginPrimaryAction() && orientedKnife.EndPrimaryAction(),
            "relative-arc cleaver test could not release a swing");
        Assert(Vector2.Dot(orientedKnife.ChopDirection, -Vector2.UnitX) > 0.98f,
            "cleaver swing stayed screen-down instead of rotating its original arc with the selected base");

        var gestureKnife = new PhysicalKnife(new Vector2(300f, 300f));
        Assert(gestureKnife.Equip(gestureKnife.Position, gestureKnife.Position),
            "movement-only swing regression could not equip the cleaver");
        var gestureCursor = gestureKnife.Position;
        for (var step = 0; step < 32; step++)
        {
            gestureCursor.Y -= 3.2f;
            gestureKnife.SetGrabTarget(gestureCursor);
            gestureKnife.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
                Array.Empty<SoftBody>(), 1280f, 720f);
        }
        for (var step = 0; step < 14; step++)
        {
            gestureCursor.Y += 8f;
            gestureKnife.SetGrabTarget(gestureCursor);
            gestureKnife.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
                Array.Empty<SoftBody>(), 1280f, 720f);
        }
        Assert(gestureKnife.ControlState == CleaverControlState.Carry,
            "vertical mouse movement still triggered the legacy cleaver swing without LMB");

        var diagonalKnife = new PhysicalKnife(new Vector2(500f, 300f));
        Assert(diagonalKnife.Equip(diagonalKnife.Position, diagonalKnife.Position), "fixed-arc swing test could not equip the cleaver");
        var diagonalCursor = diagonalKnife.Position;
        Assert(diagonalKnife.BeginPrimaryAction(), "fixed-arc swing test could not begin charging");
        for (var step = 0; step < 30; step++)
        {
            diagonalCursor.X += 2.4f;
            diagonalKnife.SetGrabTarget(diagonalCursor);
            diagonalKnife.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(), Array.Empty<SoftBody>(), 1280f, 720f);
        }
        Assert(diagonalKnife.EndPrimaryAction(), "fixed-arc charge did not release");
        for (var step = 0; step < 6; step++)
        {
            diagonalKnife.SetGrabTarget(diagonalCursor);
            diagonalKnife.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(), Array.Empty<SoftBody>(), 1280f, 720f);
        }
        Assert(diagonalKnife.ControlState == CleaverControlState.Swing &&
               Vector2.Dot(diagonalKnife.ChopDirection, Vector2.UnitY) > 0.98f,
            "cursor travel distorted the melee arc away from its selected base orientation");

        var responsiveKnife = new PhysicalKnife(new Vector2(760f, 240f));
        Assert(responsiveKnife.BeginGrab(responsiveKnife.Position),
            "responsive-carry test could not take the cleaver");
        var carryStart = responsiveKnife.Position;
        for (var step = 0; step < 12; step++)
        {
            responsiveKnife.SetGrabTarget(carryStart + new Vector2(220f, 0f));
            responsiveKnife.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
                Array.Empty<SoftBody>(), 1280f, 720f);
        }
        Assert(responsiveKnife.Position.X > carryStart.X + 40f,
            $"assisted cleaver still lagged excessively behind a deliberate hand move ({responsiveKnife.Position.X - carryStart.X:0.0}px)");
        Assert(responsiveKnife.ControlState == CleaverControlState.Carry,
            "direct click-drag movement accidentally triggered the equipped-only swing action");

        var bluntKnife = new PhysicalKnife(new Vector2(100f, 200f));
        var smallBody = new SoftBody(new Vector2(108f, 200f), 12f, 24);
        Assert(bluntKnife.BeginGrab(bluntKnife.Position), "blunt-contact test could not take the cleaver");
        var sawPhysicalContact = false;
        for (var step = 0; step < 12; step++)
        {
            bluntKnife.SetGrabTarget(new Vector2(100f + step, 200f));
            bluntKnife.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(), new[] { smallBody }, 1280f, 720f);
            sawPhysicalContact |= bluntKnife.BlobContactsThisStep > 0;
            Assert(!bluntKnife.PuncturedThisStep,
                "a non-edge cleaver contact incorrectly registered as a cutting strike");
        }

        Assert(sawPhysicalContact, "cleaver handle/spine test never made physical contact");
        Assert(smallBody.BrokenLinkCount == 0,
            "the cleaver handle, spine, or blade body damaged tissue without edge contact");
    }

    private static void CleaverCarriesBloodStains()
    {
        var knife = new PhysicalKnife(new Vector2(180f, 180f));
        var edgeMidpoint = (knife.BladeEdgeStart + knife.BladeEdgeEnd) * 0.5f;
        var blood = new GranularParticle
        {
            Position = edgeMidpoint,
            PreviousPosition = edgeMidpoint - new Vector2(0f, 20f),
            Radius = 2.2f,
            Lifetime = 10f,
            Kind = GranularKind.Blood
        };
        Assert(knife.ResolveBloodContact(ref blood, Dt),
            "physical blood pixel passed through the cleaver without coating it");
        Assert(knife.BloodStains.Count == 1 && knife.BloodStains[0].Wetness > 0.9f,
            "cleaver blood contact did not create a fresh local stain");
        var localPosition = knife.BloodStains[0].LocalPosition;

        for (var i = 0; i < 2_400; i++)
            knife.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(), Array.Empty<SoftBody>(), 1280f, 720f);
        Assert(knife.BloodStains.Count == 1 && knife.BloodStains[0].Wetness <= 0.01f,
            "cleaver stain disappeared instead of drying persistently");
        Assert(Vector2.DistanceSquared(knife.BloodStains[0].LocalPosition, localPosition) < 0.001f,
            "cleaver stain drifted away from its rotating local surface");

        var world = new BlobWorld(FlatGrid()) { Gravity = Vector2.Zero, Knife = knife };
        using var bitmap = new Bitmap(640, 480);
        using var graphics = Graphics.FromImage(bitmap);
        new GameRenderer().Draw(graphics, bitmap.Size, world, null);
        var redPixels = 0;
        for (var y = 145; y <= 215; y++)
        for (var x = 145; x <= 245; x++)
        {
            var pixel = bitmap.GetPixel(x, y);
            if (pixel.R > 70 && pixel.R > pixel.G * 1.35f && pixel.R > pixel.B * 1.2f) redPixels++;
        }
        Assert(redPixels >= 3, "persistent cleaver blood stain was not visible in the actual renderer");
    }

    private static void BloodPixelsStainBlobTissue()
    {
        var body = BlobArchetype.ProcessingUnit.Create(new Vector2(320f, 260f));
        var surfaceIndex = Enumerable.Range(0, body.Particles.Length)
            .First(body.IsSurfaceParticle);
        var surface = body.Particles[surfaceIndex];
        var outward = Vector2.Normalize(surface.Position - body.Center);
        var position = surface.Position + outward * (surface.Radius + 1.5f);
        var granular = new GranularMaterialSystem();
        granular.Particles.Add(new GranularParticle
        {
            Position = position,
            PreviousPosition = position + outward * 2f,
            Radius = 2.2f,
            Lifetime = 12f,
            Kind = GranularKind.Blood
        });
        granular.BeginStep();
        granular.Step(Dt, Vector2.Zero, FlatGrid(), new[] { body });
        Assert(body.BloodStains.Count > 0,
            "a physical blood pixel struck visible blob tissue without coating it");
        var stain = body.BloodStains[0];
        var before = body.BloodStainWorldPosition(stain);
        body.AddImpulse(new Vector2(18f, 0f), Dt);
        body.Integrate(Dt, Vector2.Zero);
        var after = body.BloodStainWorldPosition(stain);
        Assert(after.X > before.X,
            "blob blood stain did not remain bound to the deforming tissue particle");
        for (var i = 0; i < body.Particles.Length; i++)
            body.Particles[i].PreviousPosition = body.Particles[i].Position;
        for (var i = 0; i < 3_200; i++) body.Integrate(Dt, Vector2.Zero);
        Assert(body.BloodStains.Count > 0 && body.BloodStains[0].Wetness <= 0.01f,
            "blob blood stain disappeared instead of drying persistently");

        var world = new BlobWorld(FlatGrid()) { Gravity = Vector2.Zero };
        world.Bodies.Add(body);
        using var bitmap = new Bitmap(640, 480);
        using var graphics = Graphics.FromImage(bitmap);
        new GameRenderer().Draw(graphics, bitmap.Size, world, null);
        var visiblePigment = 0;
        var renderedMark = body.BloodStainWorldPosition(body.BloodStains[0]);
        for (var y = Math.Max(0, (int)renderedMark.Y - 10); y <= Math.Min(bitmap.Height - 1, (int)renderedMark.Y + 10); y++)
        for (var x = Math.Max(0, (int)renderedMark.X - 10); x <= Math.Min(bitmap.Width - 1, (int)renderedMark.X + 10); x++)
        {
            var pixel = bitmap.GetPixel(x, y);
            if (pixel.R > 75 && pixel.G < 150 && pixel.B < 145) visiblePigment++;
        }
        Assert(visiblePigment >= 2,
            "persistent particle-bound blob stain was not visible in the actual renderer");
    }

    private static void DetachedTissueRetainsBlobColor()
    {
        var detached = new SoftBody(new Vector2(300f, 240f), 18f, 19);
        var granular = new GranularMaterialSystem();
        granular.BeginStep();
        Assert(granular.TryEmitDetached(detached, Dt),
            "detached tissue fixture could not emit its physical pixels");
        var colored = granular.Particles.Count(p =>
            p.Kind == GranularKind.Tissue && p.Appearance != GranularAppearance.Gore);
        var gore = granular.Particles.Count(p =>
            p.Kind == GranularKind.Tissue && p.Appearance == GranularAppearance.Gore);
        Assert(colored > 0 && gore > 0,
            $"detached pixels did not preserve a readable tissue/gore mix ({colored} colored, {gore} gore)");
    }

    private static void DroppedCleaverRidesWithoutRolling()
    {
        var belt = new ConveyorBelt(new Vector2(100f, 430f), 800f, 28f, 150f,
            minimumWidth: 96f, systemControlled: true);
        var knife = new PhysicalKnife(new Vector2(180f, 220f));
        Assert(knife.BeginGrab(knife.Position), "belt-settling test could not take the cleaver");
        for (var i = 0; i < 72; i++)
        {
            knife.SetGrabTarget(new Vector2(340f, 320f));
            knife.Step(Dt, Vector2.Zero, new[] { belt }, Array.Empty<SoftBody>(), 1280f, 720f);
        }
        knife.EndGrab(new Vector2(80f, 30f), Dt);
        var releaseX = knife.Position.X;
        for (var i = 0; i < 300; i++)
            knife.Step(Dt, new Vector2(0f, 900f), new[] { belt }, Array.Empty<SoftBody>(), 1280f, 720f);
        Assert(knife.Position.X > releaseX + 80f,
            $"settled cleaver did not ride in the conveyor's linear direction (x={knife.Position.X:0.0}, y={knife.Position.Y:0.0}, angle={knife.Angle:0.00})");
        Assert(MathF.Abs(MathF.Sin(knife.Angle)) < 0.24f,
            $"dropped cleaver remained suspended on a diagonal instead of settling broad-side-down ({knife.Angle:0.00} radians)");

        var heldKnife = new PhysicalKnife(new Vector2(640f, 300f));
        Assert(heldKnife.BeginGrab(heldKnife.Position),
            "held conveyor-containment test could not take the cleaver");
        for (var i = 0; i < 180; i++)
        {
            heldKnife.SetGrabTarget(new Vector2(640f, 610f));
            heldKnife.Step(Dt, Vector2.Zero, new[] { belt }, Array.Empty<SoftBody>(), 1280f, 720f);
        }
        var lowestToolPoint = MathF.Max(
            MathF.Max(heldKnife.HandleStart.Y + 7f, heldKnife.HandleEnd.Y + 7f),
            MathF.Max(heldKnife.BladeCoreStart.Y + 12f, heldKnife.BladeCoreEnd.Y + 12f));
        Assert(lowestToolPoint <= belt.Position.Y + 0.1f,
            $"grabbed cleaver crossed below the conveyor ({lowestToolPoint:0.0} > {belt.Position.Y:0.0})");

        var thrownKnife = new PhysicalKnife(new Vector2(940f, 180f));
        Assert(thrownKnife.BeginGrab(thrownKnife.Position),
            "throw-momentum test could not take the cleaver");
        for (var i = 0; i < 36; i++)
        {
            thrownKnife.SetGrabTarget(new Vector2(1160f, 210f));
            thrownKnife.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
                Array.Empty<SoftBody>(), 1280f, 720f);
        }
        var throwRelease = thrownKnife.Position;
        thrownKnife.EndGrab(Vector2.Zero, Dt);
        for (var i = 0; i < 12; i++)
            thrownKnife.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
                Array.Empty<SoftBody>(), 1280f, 720f);
        Assert(thrownKnife.Position.X > throwRelease.X + 35f,
            $"released cleaver discarded its simulated throw momentum ({thrownKnife.Position.X - throwRelease.X:0.0}px travel)");
    }

    private static void KnifeReturnsToHolster()
    {
        var holster = new Vector2(640f, 275f);
        var knife = new PhysicalKnife(holster);
        Assert(knife.BeginGrab(holster), "cleaver could not be taken from its centered rack");
        for (var i = 0; i < 10; i++)
        {
            knife.SetGrabTarget(holster + new Vector2(72f, 8f));
            knife.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(), Array.Empty<SoftBody>(), 1280f, 720f);
        }
        knife.EndGrab(Vector2.Zero, Dt);
        Assert(knife.IsReturningToHolster, "cleaver released near the rack did not begin its magnetic return");
        for (var i = 0; i < 120; i++)
            knife.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(), Array.Empty<SoftBody>(), 1280f, 720f);
        Assert(knife.IsHolstered && Vector2.DistanceSquared(knife.Position, holster) < 0.01f,
            "returning cleaver did not fly back and attach to its authored wall socket");
    }

    private static void ArsenalSelectionSwapsCenteredTool()
    {
        var holster = new Vector2(640f, 300f);
        var tool = new PhysicalKnife(holster);
        Assert(tool.Equip(holster, holster), "default cleaver could not be equipped before inventory swap");

        tool.SelectArsenalVisual(4);
        Assert(tool.ArsenalVisualVariant == 4, "inventory selection did not change the centered physical tool");
        Assert(tool.IsHolstered && !tool.IsGrabbed && tool.Position == holster,
            "inventory swap did not reset the selected tool into the existing centered rack");
        Assert(tool.HitTest(holster), "selected arsenal sprite was not pickable at the rack socket");
        Assert(tool.BeginGrab(holster), "selected arsenal tool could not be left-dragged from the rack");
        tool.SelectArsenalVisual(8);
        Assert(tool.IsHolstered && !tool.IsGrabbed,
            "swapping while a tool was held did not safely return the replacement to the rack");
        Assert(tool.Equip(holster, holster), "selected arsenal tool could not be equipped with E semantics");

        var mountBelt = new ConveyorBelt(new Vector2(120f, 460f), 1040f, 32f, 0f,
            systemControlled: true);
        tool.SetGrabTarget(new Vector2(640f, 326f));
        tool.Step(Dt, Vector2.Zero, new[] { mountBelt }, Array.Empty<SoftBody>(), 1280f, 720f);
        Assert(tool.PlacementPreviewValid && tool.SlingshotHeightIndex == 1 &&
               tool.PlaceAtPreview() && tool.IsDeployed,
            "slingshot LMB placement preview did not snap to the selected conveyor and height");
        var mountedSlingBody = BlobArchetype.ProcessingUnit.Create(tool.SlingshotCradlePosition);
        mountedSlingBody.BeginGrab(mountedSlingBody.Center);
        Assert(mountedSlingBody.IsGrabbed,
            "slingshot test could not physically carry its blob ammunition");
        tool.Step(Dt, Vector2.Zero, new[] { mountBelt }, new[] { mountedSlingBody }, 1280f, 720f);
        Assert(tool.SlingshotBody == mountedSlingBody && !mountedSlingBody.IsGrabbed,
            "bringing a grabbed blob to the rubber cradle did not auto-load it");
        Assert(tool.CanBeginSlingshotPull(mountedSlingBody.Center) && tool.BeginPrimaryAction(),
            "deployed slingshot would not let the player re-grab its loaded blob");
        tool.SetGrabTarget(tool.SlingshotCradlePosition - new Vector2(88f, 0f));
        for (var step = 0; step < 36; step++)
            tool.Step(Dt, Vector2.Zero, new[] { mountBelt }, new[] { mountedSlingBody }, 1280f, 720f);
        tool.EndPrimaryAction();
        tool.Step(Dt, Vector2.Zero, new[] { mountBelt }, new[] { mountedSlingBody }, 1280f, 720f);
        Assert(tool.ArsenalShotSerial > 0 && mountedSlingBody.AverageVelocity(Dt).X > 0f,
            "mounted slingshot did not launch opposite its physical pull direction");

        tool.SelectArsenalVisual(8);
        Assert(tool.Equip(holster, holster),
            "slingshot could not be re-equipped for side-wall placement");
        tool.SetGrabTarget(new Vector2(16f, 320f));
        tool.Step(Dt, Vector2.Zero, new[] { mountBelt },
            Array.Empty<SoftBody>(), 1280f, 720f);
        Assert(tool.PlacementPreviewValid &&
               MathF.Abs(tool.PlacementPreviewAngle - MathF.PI * 0.5f) < 0.01f &&
               tool.PlaceAtPreview() &&
               tool.SlingshotCradlePosition.X > tool.Position.X + 80f,
            "left-wall slingshot preview did not rotate its forks east into the room");

        tool.SelectArsenalVisual(9);
        Assert(tool.Equip(holster, holster), "pike could not be equipped for wall placement");
        var pikeBelt = new ConveyorBelt(new Vector2(300f, 460f), 700f, 34f, 0f,
            systemControlled: true);
        tool.SetGrabTarget(new Vector2(920f, 500f));
        tool.Step(Dt, Vector2.Zero, new[] { pikeBelt },
            Array.Empty<SoftBody>(), 1280f, 720f);
        Assert(!tool.PlacementPreviewVisible && !tool.PlacementPreviewValid,
            "wall pike still ghosted or placed below/too close to a conveyor");
        Assert(tool.BeginRotationAdjust(tool.Position),
            "pike could not orient its placement preview");
        tool.UpdateRotationAdjust(tool.Position - new Vector2(60f, 0f));
        tool.EndRotationAdjust();
        tool.SetGrabTarget(new Vector2(920f, 240f));
        tool.Step(Dt, Vector2.Zero, new[] { pikeBelt },
            Array.Empty<SoftBody>(), 1280f, 720f);
        Assert(tool.PlacementPreviewValid && tool.PlaceAtPreview() && tool.IsDeployed,
            "pike LMB placement preview did not accept a background wall tile");
        var mountedPikeBody = BlobArchetype.ProcessingUnit.Create(
            (tool.BladeEdgeStart + tool.BladeEdgeEnd) * 0.5f);
        mountedPikeBody.AddImpulse(new Vector2(280f, 0f), Dt);
        var mountedPikeDamage = mountedPikeBody.BrokenLinkCount;
        var fixedPikePosition = tool.Position;
        for (var step = 0; step < 4; step++)
            tool.Step(Dt, Vector2.Zero, new[] { pikeBelt },
                new[] { mountedPikeBody }, 1280f, 720f);
        Assert(mountedPikeBody.BrokenLinkCount > mountedPikeDamage,
            $"wall-mounted pike did not passively puncture incoming physical matter " +
            $"(edge={tool.BladeEdgeStart}->{tool.BladeEdgeEnd}, body={mountedPikeBody.Center}, " +
            $"velocity={mountedPikeBody.AverageVelocity(Dt)}, spacing={mountedPikeBody.ParticleSpacing:0.0})");
        Assert(tool.Position == fixedPikePosition && tool.PikePinCount == 1,
            "placed wall pike moved under blob force or failed to retain the impaled blob");
        var retainedPikeCenter = mountedPikeBody.Center;
        mountedPikeBody.AddImpulse(new Vector2(0f, 560f), Dt);
        for (var step = 0; step < 16; step++)
            tool.Step(Dt, new Vector2(0f, 980f), new[] { pikeBelt },
                new[] { mountedPikeBody }, 1280f, 720f);
        Assert(tool.PikePinCount == 1 &&
               Vector2.Distance(mountedPikeBody.Center, retainedPikeCenter) < 36f,
            "an impaled blob slid through the pike instead of remaining attached at the puncture");

        tool.SelectArsenalVisual(-1);
        Assert(tool.ArsenalVisualVariant == -1 && tool.IsHolstered && tool.HitTest(holster),
            "cleaver inventory entry did not restore the original centered tool");
    }

    private static void WeaponDumbwaiterRerollsOneWeapon()
    {
        var socket = new Vector2(640f, 300f);
        var tool = new PhysicalKnife(socket);
        var dumbwaiter = new WeaponDumbwaiter(socket);
        var grid = new DestructibleGrid(40, 22, 32);
        dumbwaiter.PrepareInitialDelivery(-1, tool);
        Assert(dumbwaiter.Phase == WeaponDumbwaiterPhase.Closed &&
               dumbwaiter.DoorFrame == 3 && !tool.Visible,
            "new day did not begin with the shutter closed over the hidden weapon");
        for (var step = 0; step < 90; step++)
            dumbwaiter.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
                grid, 1280f, 720f, tool, powered: false);
        Assert(dumbwaiter.Phase == WeaponDumbwaiterPhase.Closed && !tool.Visible,
            "unpowered dumbwaiter opened before the day began");
        Assert(dumbwaiter.BeginInitialOpening(),
            "breaker latch could not immediately start the initial dumbwaiter opening");
        Assert(dumbwaiter.Phase == WeaponDumbwaiterPhase.Opening,
            "powered dumbwaiter did not begin opening on the first simulation tick");
        var displayRateDoorPositions = new HashSet<int>();
        for (var displayFrame = 0; displayFrame < 30; displayFrame++)
        {
            // Two 120 Hz simulation steps represent one ~60 Hz paint interval.
            dumbwaiter.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
                grid, 1280f, 720f, tool, powered: true);
            dumbwaiter.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
                grid, 1280f, 720f, tool, powered: true);
            displayRateDoorPositions.Add((int)MathF.Round(dumbwaiter.DoorClosure * 1000f));
        }
        Assert(displayRateDoorPositions.Count >= 24,
            $"dumbwaiter opening exposed only {displayRateDoorPositions.Count} display-rate positions");
        Assert(dumbwaiter.Phase == WeaponDumbwaiterPhase.Open &&
               dumbwaiter.DoorFrame == 0 && tool.Visible && tool.IsHolstered,
            "powered day start did not animate open and present the first weapon");
        Assert(tool.BeginGrab(socket), "presented dumbwaiter weapon could not be taken");
        dumbwaiter.NotifyWeaponTaken();
        dumbwaiter.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
            grid, 1280f, 720f, tool, powered: true);
        Assert(dumbwaiter.Phase == WeaponDumbwaiterPhase.Closing,
            "taking the presented weapon did not start closing the shutter");
        for (var step = 0; step < 65; step++)
            dumbwaiter.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
                grid, 1280f, 720f, tool, powered: true);
        Assert(dumbwaiter.Phase == WeaponDumbwaiterPhase.Closed &&
               dumbwaiter.DoorFrame == 3 && tool.Visible,
            "shutter did not remain closed after the player took the weapon");

        dumbwaiter.SpawnToken(new Vector2(420f, 300f));
        var firstToken = dumbwaiter.Token;
        Assert(WeaponDumbwaiter.TokenRadius == 16f,
            "reroll coin physical body did not match its 32-pixel presentation");
        dumbwaiter.SpawnToken(new Vector2(460f, 300f));
        Assert(ReferenceEquals(firstToken, dumbwaiter.Token),
            "a second reroll token spawned while one was already in the system");
        Assert(dumbwaiter.BeginTokenGrab(firstToken!.Position),
            "physical reroll coin could not be picked up");
        dumbwaiter.SetTokenGrabTarget(dumbwaiter.CoinSlotCenter);
        dumbwaiter.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
            grid, 1280f, 720f, tool, powered: true);
        Assert(dumbwaiter.ReleaseToken(dumbwaiter.CoinSlotCenter, Vector2.Zero) &&
               dumbwaiter.TokenDeposited &&
               dumbwaiter.ButtonArmed,
            "releasing the physical coin over the authored slot did not arm the button");
        Assert(dumbwaiter.Activate(1, tool) && !tool.Visible,
            "armed dumbwaiter did not explode and hide the current weapon");
        for (var step = 0; step < 180; step++)
            dumbwaiter.Step(Dt, new Vector2(0f, 980f), Array.Empty<ConveyorBelt>(),
                grid, 1280f, 720f, tool, powered: true);
        Assert(dumbwaiter.Phase == WeaponDumbwaiterPhase.Open && tool.Visible &&
               tool.IsHolstered && tool.ArsenalVisualVariant == 1,
            "dumbwaiter shutter did not finish by delivering the selected replacement");

        var assetRoot = Path.Combine(AppContext.BaseDirectory, "Assets");
        using var housing = new Bitmap(Path.Combine(assetRoot, "WeaponDumbwaiter.png"));
        using var controls = new Bitmap(Path.Combine(assetRoot, "WeaponDumbwaiterControls.png"));
        using var coin = new Bitmap(Path.Combine(assetRoot, "WeaponRerollToken.png"));
        Assert(housing.Size == new Size(72 * 4, 96) &&
               controls.Size == new Size(32 * 3, 64) &&
               coin.Size == new Size(16 * 4, 16),
            "Pixel Forge dumbwaiter exports do not match the runtime frame contracts");
    }

    private static void SlingshotImpactsDamageBothBodies()
    {
        var sling = new PhysicalKnife(new Vector2(500f, 390f));
        sling.SelectArsenalVisual(8);
        Assert(sling.Equip(sling.Position, sling.Position) &&
               MathF.Abs(sling.Angle) < 0.001f,
            "held slingshot did not equip in its locked north-facing pose");
        var launched = BlobArchetype.ProcessingUnit.Create(new Vector2(500f, 390f));
        var struck = BlobArchetype.ProcessingUnit.Create(new Vector2(500f, 205f));
        var world = new BlobWorld(FlatGrid())
        {
            Gravity = Vector2.Zero,
            Knife = sling
        };
        world.Bodies.Add(launched);
        world.Bodies.Add(struck);
        Assert(sling.BeginPrimaryAction(),
            "slingshot bilateral-impact fixture could not begin charging");
        for (var step = 0; step < 96; step++) world.Step(Dt);
        Assert(sling.EndPrimaryAction(),
            "slingshot bilateral-impact fixture could not release");
        var launchedDamage = launched.BrokenLinkCount;
        var struckDamage = struck.BrokenLinkCount;
        for (var step = 0; step < 150 &&
             (launched.BrokenLinkCount == launchedDamage ||
              struck.BrokenLinkCount == struckDamage); step++)
            world.Step(Dt);
        Assert(launched.BrokenLinkCount > launchedDamage,
            "slingshot impact did not damage the fired blob");
        Assert(struck.BrokenLinkCount > struckDamage,
            "slingshot impact damaged only its ammunition and not the struck blob");
    }

    private static void ArsenalPrimaryActionsAreDistinct()
    {
        static (PhysicalKnife Tool, SoftBody Body) Setup(int variant, Vector2 bodyPosition)
        {
            var tool = new PhysicalKnife(new Vector2(640f, 300f));
            tool.SelectArsenalVisual(variant);
            Assert(tool.Equip(tool.Position, tool.Position), $"arsenal variant {variant} could not equip");
            var expectedHolsterDirection = variant is >= 1 and <= 6 or 8 or 10
                ? -Vector2.UnitX
                : -Vector2.UnitY;
            Assert(Vector2.Dot(tool.BaseAimDirection, expectedHolsterDirection) > 0.98f,
                $"arsenal variant {variant} did not inherit its authored holster orientation");
            var rotationOrigin = tool.Position;
            if (variant == 8)
            {
                Assert(!tool.BeginRotationAdjust(rotationOrigin) &&
                       !tool.RotateBaseBy(MathF.PI / 12f) &&
                       MathF.Abs(tool.Angle) < 0.001f,
                    "held slingshot accepted rotation instead of remaining north-facing");
            }
            else
            {
                Assert(tool.BeginRotationAdjust(rotationOrigin),
                    $"arsenal variant {variant} rejected base-rotation control");
                Assert(!tool.BeginPrimaryAction(),
                    $"arsenal variant {variant} activated while its rotation control was held");
                tool.UpdateRotationAdjust(rotationOrigin + new Vector2(60f, 0f));
                tool.EndRotationAdjust();
            }
            if (variant == 7)
                Assert(!tool.RotationAdjusting && tool.SledgeSwingRight &&
                       Vector2.Dot(tool.BaseAimDirection, -Vector2.UnitY) > 0.98f,
                    "sledge RMB did not toggle its swing side or incorrectly changed base rotation");
            else if (variant == 8)
                Assert(MathF.Abs(tool.Angle) < 0.001f,
                    "slingshot did not retain its authored north-facing pose");
            else
                Assert(!tool.RotationAdjusting &&
                       Vector2.Dot(tool.BaseAimDirection, Vector2.UnitX) > 0.98f,
                    $"arsenal variant {variant} did not retain its right-drag base rotation");
            tool.SetGrabTarget(new Vector2(700f, 300f));
            for (var step = 0; step < 60; step++)
                tool.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
                    Array.Empty<SoftBody>(), 1280f, 720f);
            var body = BlobArchetype.ProcessingUnit.Create(bodyPosition);
            return (tool, body);
        }

        var (saber, _) = Setup(0, new Vector2(710f, 270f));
        Assert(!saber.BeginPrimaryAction() && saber.SaberIgnited,
            "first lightsaber LMB did not exclusively ignite the blade");
        var saberBody = new SoftBody((saber.BladeEdgeStart + saber.BladeEdgeEnd) * 0.5f, 12f, 24);
        var passiveCenter = saberBody.Center;
        for (var step = 0; step < 8; step++)
            saber.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(), new[] { saberBody }, 1280f, 720f);
        Assert(saberBody.BrokenLinkCount > 0 && saber.SaberIgnited,
            "stationary ignited lightsaber did not cut tissue that moved into its physical blade");
        Assert(Vector2.Distance(passiveCenter, saberBody.Center) < 1f,
            "passive lightsaber cut depended on blunt collision force");
        Assert(saber.BeginPrimaryAction() && saber.EndPrimaryAction(),
            "subsequent lightsaber LMB did not use the shared cleaver swing arc");
        Assert(saber.ControlState == CleaverControlState.Swing,
            "lightsaber did not enter the same relative swing state as the cleaver");

        var hotBladeBlood = new GranularParticle
        {
            Position = (saber.BladeEdgeStart + saber.BladeEdgeEnd) * 0.5f,
            PreviousPosition = (saber.BladeEdgeStart + saber.BladeEdgeEnd) * 0.5f - new Vector2(0f, 3f),
            Radius = 1.8f,
            Lifetime = 10f,
            Kind = GranularKind.Blood
        };
        var sizzleBefore = saber.SaberSizzleSerial;
        Assert(saber.ResolveBloodContact(ref hotBladeBlood, Dt) &&
               hotBladeBlood.Lifetime <= 0f &&
               saber.SaberSizzleSerial == sizzleBefore + 1 &&
               saber.BloodStains.Count == 0,
            "individual blood pixel did not fizzle on the hot blade or incorrectly left a blade stain");
        var hiltBlood = new GranularParticle
        {
            Position = saber.Position,
            PreviousPosition = saber.Position - new Vector2(0f, 3f),
            Radius = 1.8f,
            Lifetime = 10f,
            Kind = GranularKind.Blood
        };
        Assert(saber.ResolveBloodContact(ref hiltBlood, Dt) &&
               saber.BloodStains.Count > 0 &&
               saber.BloodStains.All(stain => stain.LocalPosition.X >= -18f),
            "lightsaber hilt did not retain blood independently of its self-cleaning blade");
        Assert(saber.DeigniteSaber() && !saber.SaberIgnited,
            "dedicated Z de-ignition path did not extinguish the lightsaber");

        foreach (var variant in new[] { 1, 2, 4 })
        {
            var (gun, target) = Setup(variant, new Vector2(850f, 300f));
            var downstreamTarget = variant == 1
                ? BlobArchetype.ProcessingUnit.Create(new Vector2(940f, 300f))
                : null;
            var gunTargets = downstreamTarget is null
                ? new[] { target }
                : new[] { target, downstreamTarget };
            var liveShotLine = gun.LiveMuzzlePosition + gun.LiveBarrelDirection * 150f;
            target.ApplyTranslation(liveShotLine - target.Center, preserveVelocity: true);
            if (downstreamTarget is not null)
            {
                var downstreamLine = gun.LiveMuzzlePosition + gun.LiveBarrelDirection * 240f;
                downstreamTarget.ApplyTranslation(
                    downstreamLine - downstreamTarget.Center, preserveVelocity: true);
            }
            var positionBeforeRecoil = gun.Position;
            var angleBeforeRecoil = gun.Angle;
            var muzzleBeforeRecoil = gun.LiveMuzzlePosition;
            Assert(gun.BeginPrimaryAction(), $"gun variant {variant} rejected its trigger");
            gun.EndPrimaryAction();
            gun.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(), gunTargets, 1280f, 720f);
            Assert(Vector2.DistanceSquared(gun.LastArsenalActionPosition, muzzleBeforeRecoil) < 1f,
                $"gun variant {variant} projectile did not originate at its live authored muzzle");
            for (var step = 1; step < 40; step++)
                gun.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(), gunTargets, 1280f, 720f);
            Assert(gun.ArsenalShotSerial > 0,
                $"gun variant {variant} behaved like a cleaver instead of firing");
            Assert(gun.ArsenalActionEffects.All(effect => effect.Variant is not (1 or 2 or 3 or 4)),
                $"gun variant {variant} still rendered a hitscan damage line");
            Assert(target.BrokenLinkCount > 0,
                $"gun variant {variant} projectile crossed its target without applying damage " +
                $"(tool={gun.Position}, aim={gun.BaseAimDirection}, target={target.Center}, " +
                $"projectiles={string.Join(", ", gun.ArsenalProjectiles.Select(p =>
                    $"{p.Kind}@{p.Position} v={p.Velocity}"))})");
            Assert(Vector2.DistanceSquared(positionBeforeRecoil, gun.Position) > 1f ||
                   MathF.Abs(angleBeforeRecoil - gun.Angle) > 0.03f,
                $"gun variant {variant} fired without physical grip or angular recoil");
            if (downstreamTarget is not null)
                Assert(target.AverageVelocity(Dt).Length() > 20f,
                    "large nail projectile did not carry or knock back the first blob it struck");
        }

        var (pinner, pinnedBody) = Setup(1, new Vector2(1080f, 315f));
        var pinLine = pinner.LiveMuzzlePosition + pinner.LiveBarrelDirection * 335f;
        pinnedBody.ApplyTranslation(pinLine - pinnedBody.Center, preserveVelocity: true);
        Assert(pinner.BeginPrimaryAction(), "nail gun rejected the wall-pinning shot");
        pinner.EndPrimaryAction();
        for (var step = 0; step < 180; step++)
            pinner.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
                new[] { pinnedBody }, 1280f, 720f);
        Assert(pinner.NailPinCount > 0,
            "large nail carried a blob toward the wall but never established a physical pin");

        var (joiner, _) = Setup(1, new Vector2(840f, 300f));
        var joinedFirst = new SoftBody(
            joiner.LiveMuzzlePosition + joiner.LiveBarrelDirection * 90f, 25f, 28);
        var joinedSecond = new SoftBody(
            joiner.LiveMuzzlePosition + joiner.LiveBarrelDirection * 146f, 25f, 28);
        Assert(joiner.BeginPrimaryAction(), "nail gun rejected the blob-joining shot");
        joiner.EndPrimaryAction();
        for (var step = 0; step < 90; step++)
            joiner.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
                new[] { joinedFirst, joinedSecond }, 1280f, 720f);
        Assert(joiner.JoinedNailPinCount > 0,
            "one nail penetrated two blobs but did not leave a physical blob-to-blob pin");

        var (magnum, magnumTarget) = Setup(3, new Vector2(900f, 300f));
        var magnumLine = magnum.LiveMuzzlePosition + magnum.LiveBarrelDirection * 170f;
        magnumTarget.ApplyTranslation(magnumLine - magnumTarget.Center, preserveVelocity: true);
        Assert(magnum.BeginPrimaryAction(), "magnum rejected cock/steady hold");
        for (var step = 0; step < 60; step++)
            magnum.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(), new[] { magnumTarget }, 1280f, 720f);
        magnum.EndPrimaryAction();
        var secondMagnumTarget = BlobArchetype.ProcessingUnit.Create(
            magnum.LiveMuzzlePosition + magnum.LiveBarrelDirection * 260f);
        for (var step = 0; step < 90; step++)
            magnum.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
                new[] { magnumTarget, secondMagnumTarget }, 1280f, 720f);
        Assert(magnum.ArsenalShotSerial == 1, "magnum did not fire exactly once on trigger release");
        Assert(magnumTarget.BrokenLinkCount > 0 && secondMagnumTarget.BrokenLinkCount > 0,
            "magnum projectile did not preserve its deeper multi-body penetration");

        var (saw, sawTarget) = Setup(5, new Vector2(890f, 300f));
        var sawMatterBefore = sawTarget.PhysicalParticleCount;
        var sawLine = saw.LiveMuzzlePosition + saw.LiveBarrelDirection * 190f;
        sawTarget.ApplyTranslation(sawLine - sawTarget.Center, preserveVelocity: true);
        Assert(saw.BeginPrimaryAction(), "blade shooter rejected spin-up");
        for (var step = 0; step < 45; step++)
            saw.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(), new[] { sawTarget }, 1280f, 720f);
        saw.EndPrimaryAction();
        for (var step = 0; step < 180; step++)
            saw.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
                new[] { sawTarget }, 1280f, 720f);
        Assert(saw.ArsenalProjectiles.Any(projectile =>
                projectile.Kind == ArsenalProjectileKind.SawBlade && projectile.Stuck),
            "blade shooter projectile did not preserve penetration and then stick into a surface");
        Assert(sawTarget.PhysicalParticleCount < sawMatterBefore,
            "saw blade broke bonds but did not remove the material band along its visible path");

        var (vacuum, vacuumTarget) = Setup(6, new Vector2(840f, 300f));
        Assert(vacuum.BeginPrimaryAction(), "chipper vacuum rejected suction hold");
        for (var step = 0; step < 24; step++)
            vacuum.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(), new[] { vacuumTarget }, 1280f, 720f);
        Assert(vacuumTarget.AverageVelocity(Dt).X < -0.1f,
            "chipper vacuum did not pull nearby blob matter toward its intake");

        var (hammer, hammerTarget) = Setup(7, new Vector2(760f, 355f));
        var hammerBelt = new ConveyorBelt(
            new Vector2(300f, 455f), 700f, 34f, 0f, systemControlled: true);
        hammerTarget.ApplyTranslation(
            new Vector2(hammer.Position.X, hammerBelt.Position.Y - 26f) - hammerTarget.Center,
            preserveVelocity: true);
        var nearbyHammerBody = BlobArchetype.ProcessingUnit.Create(
            new Vector2(hammer.Position.X + 145f, hammerBelt.Position.Y - 26f));
        var hammerCenterBefore = hammerTarget.Center;
        var hammerDamageBefore = hammerTarget.BrokenLinkCount;
        var hammerMatterBefore = hammerTarget.PhysicalParticleCount;
        var hammerGranular = new GranularMaterialSystem();
        for (var particle = 0; particle < 9; particle++)
        {
            var position = new Vector2(
                hammerBelt.Position.X + 45f + particle * 70f,
                hammerBelt.Position.Y - 3f);
            hammerGranular.Particles.Add(new GranularParticle
            {
                Position = position,
                PreviousPosition = position,
                Radius = 2.2f,
                Lifetime = 10f,
                Kind = particle % 3 == 0
                    ? GranularKind.Tissue
                    : GranularKind.Blood
            });
        }
        Assert(hammer.BeginPrimaryAction(), "sledgehammer rejected charge");
        for (var step = 0; step < 90; step++)
            hammer.Step(Dt, Vector2.Zero, new[] { hammerBelt },
                new[] { hammerTarget, nearbyHammerBody }, 1280f, 720f,
                granular: hammerGranular);
        hammer.EndPrimaryAction();
        var observedFlatImpact = false;
        var observedShake = false;
        var impactFrames = 0;
        for (var step = 0; step < 40; step++)
        {
            hammer.Step(Dt, Vector2.Zero, new[] { hammerBelt },
                new[] { hammerTarget, nearbyHammerBody }, 1280f, 720f,
                granular: hammerGranular);
            if (hammer.ControlState != CleaverControlState.Impact) continue;
            impactFrames++;
            observedFlatImpact |=
                MathF.Abs(hammer.BladeCoreStart.Y - hammer.BladeCoreEnd.Y) < 0.2f &&
                MathF.Abs(MathF.Max(
                              hammer.BladeEdgeStart.Y,
                              hammer.BladeEdgeEnd.Y) -
                          hammerBelt.Position.Y) < 0.3f;
            observedShake |= hammer.ScreenShakeOffset.LengthSquared() > 0.01f;
        }
        Assert(observedFlatImpact,
            "sledge impact did not lock its complete striking face flat to the ground");
        Assert(impactFrames >= 6,
            $"sledge did not visibly hold its grounded impact pose ({impactFrames} frames)");
        Assert(observedShake,
            "sledge impact did not produce its bounded camera shake");
        Assert(nearbyHammerBody.AverageVelocity(Dt).Length() > 40f,
            "fully charged sledge impact did not knock back a nearby blob with its ground AOE");
        Assert(hammerTarget.AverageVelocity(Dt).Length() > 30f ||
               Vector2.Distance(hammerTarget.Center, hammerCenterBefore) > 4f,
            "sledgehammer smash did not deliver its expected high knockback and local compression");
        Assert(hammerTarget.BrokenLinkCount > hammerDamageBefore,
            "sledgehammer passed through its target without broad crushing damage");
        Assert(hammerTarget.PhysicalParticleCount > 0 &&
               hammerTarget.PhysicalParticleCount < hammerMatterBefore,
            $"heavy impact did not destroy only the ground-side material under its hammer face " +
            $"({hammerTarget.PhysicalParticleCount}/{hammerMatterBefore} particles remain)");
        var convertedByCrush = Enumerable.Range(0, hammerTarget.Particles.Length)
            .Where(hammerTarget.IsConvertedParticle)
            .ToArray();
        Assert(convertedByCrush.Length > 0 &&
               convertedByCrush.All(index =>
                   hammerTarget.Particles[index].Position.Y >= hammerBelt.Position.Y - 37f) &&
               Enumerable.Range(0, hammerTarget.Particles.Length).Any(index =>
                   hammerTarget.IsPhysicalParticle(index) &&
                   hammerTarget.Particles[index].Position.Y < hammerBelt.Position.Y - 37f),
            "heavy crush removed material outside its bounded ground-side impact band");
        Assert(hammer.HeavyBloodBridges.Count is >= 2 and <= 3,
            "sledge crush produced no temporary blood bridges between its face and the ground");
        var impactGripY = hammer.Position.Y;
        var sawBridgeStretchDuringLift = false;
        var sawBridgeSnapBeforeRecoveryFinished = false;
        for (var step = 0; step < 90; step++)
        {
            hammer.Step(Dt, Vector2.Zero, new[] { hammerBelt },
                new[] { hammerTarget }, 1280f, 720f,
                granular: hammerGranular);
            if (hammer.ControlState == CleaverControlState.Recovery &&
                hammer.Position.Y < impactGripY - 5f &&
                hammer.HeavyBloodBridges.Count > 0)
                sawBridgeStretchDuringLift = true;
            if (hammer.ControlState == CleaverControlState.Recovery &&
                hammer.HeavyBloodBridges.Count == 0)
                sawBridgeSnapBeforeRecoveryFinished = true;
        }
        Assert(sawBridgeStretchDuringLift && sawBridgeSnapBeforeRecoveryFinished,
            "heavy blood bridges did not stretch briefly during lift and snap before recovery");
        var granularUpwardSpeeds = hammerGranular.Particles.Select(particle =>
                -(particle.Position.Y - particle.PreviousPosition.Y) / Dt)
            .Where(speed => speed > 100f)
            .ToArray();
        Assert(granularUpwardSpeeds.Length >= 7 &&
               granularUpwardSpeeds.Max() - granularUpwardSpeeds.Min() > 70f,
            "full sledge charge did not bounce conveyor matter to varied bounded heights");

        var (slowHammer, _) = Setup(7, new Vector2(1000f, 220f));
        var elevatedSlamStart = slowHammer.Position;
        Assert(slowHammer.BeginPrimaryAction() && slowHammer.EndPrimaryAction(),
            "slow-sledge timing fixture could not release its swing");
        var overheadHead = (slowHammer.BladeCoreStart + slowHammer.BladeCoreEnd) * 0.5f;
        Assert(overheadHead.Y < slowHammer.Position.Y - 48f &&
               MathF.Abs(overheadHead.X - slowHammer.Position.X) <= 23f,
            "sledge release did not begin with its head directly north of the grip");
        for (var step = 0; step < 15; step++)
            slowHammer.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
                Array.Empty<SoftBody>(), 1280f, 720f);
        Assert(slowHammer.ControlState == CleaverControlState.Swing,
            "sledgehammer completed its heavy swing as quickly as the light melee weapons");
        for (var step = 15; step < 45 &&
             slowHammer.ControlState == CleaverControlState.Swing; step++)
            slowHammer.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
                Array.Empty<SoftBody>(), 1280f, 720f);
        var groundHead = (slowHammer.BladeCoreStart + slowHammer.BladeCoreEnd) * 0.5f;
        var expectedHorizontalDirection = slowHammer.SledgeSwingRight ? 1f : -1f;
        Assert(slowHammer.ControlState == CleaverControlState.Impact &&
               groundHead.Y > slowHammer.Position.Y + 10f &&
               (groundHead.X - slowHammer.Position.X) * expectedHorizontalDirection > 35f &&
               MathF.Abs(slowHammer.BladeCoreStart.Y -
                         slowHammer.BladeCoreEnd.Y) < 0.2f &&
               slowHammer.Position.Y > elevatedSlamStart.Y + 250f,
            $"sledge did not curve from north to a west-facing, ground-parallel impact " +
            $"(state={slowHammer.ControlState}, grip={slowHammer.Position}, " +
            $"head={groundHead}, edge={slowHammer.BladeCoreStart}->{slowHammer.BladeCoreEnd})");

        var (sling, slingBody) = Setup(8, new Vector2(750f, 300f));
        Assert(sling.BeginPrimaryAction(), "blob slingshot rejected loading hold");
        for (var step = 0; step < 80; step++)
            sling.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(), new[] { slingBody }, 1280f, 720f);
        sling.EndPrimaryAction();
        sling.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(), new[] { slingBody }, 1280f, 720f);
        Assert(sling.ArsenalShotSerial > 0 && slingBody.AverageVelocity(Dt).Y < -100f,
            "north-facing held slingshot did not release its blob upward with stored impulse");

        var pike = new PhysicalKnife(new Vector2(640f, 300f));
        pike.SelectArsenalVisual(9);
        Assert(pike.Equip(pike.Position, pike.Position), "wall pike could not be deployed from the rack");
        var pikeTip = (pike.BladeEdgeStart + pike.BladeEdgeEnd) * 0.5f;
        var impaled = BlobArchetype.ProcessingUnit.Create(pikeTip);
        var beforePikeDamage = impaled.BrokenLinkCount;
        impaled.AddImpulse(new Vector2(280f, 0f), Dt);
        for (var step = 0; step < 4; step++)
            pike.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(), new[] { impaled }, 1280f, 720f);
        Assert(impaled.BrokenLinkCount > beforePikeDamage,
            $"pike did not passively puncture a blob slammed onto its physical tip " +
            $"(edge={pike.BladeEdgeStart}->{pike.BladeEdgeEnd}, body={impaled.Center}, " +
            $"velocity={impaled.AverageVelocity(Dt)}, spacing={impaled.ParticleSpacing:0.0})");

        var (gloves, gloveTarget) = Setup(10, new Vector2(790f, 300f));
        var gloveDamageBefore = gloveTarget.BrokenLinkCount;
        gloves.BeginPrimaryAction();
        for (var step = 0; step < 90; step++)
            gloves.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(), new[] { gloveTarget }, 1280f, 720f);
        gloves.EndPrimaryAction();
        for (var step = 0; step < 32; step++)
            gloves.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
                new[] { gloveTarget }, 1280f, 720f);
        Assert(gloves.ArsenalShotSerial == 1 &&
               gloveTarget.AverageVelocity(Dt).Y < -80f &&
               gloveTarget.BrokenLinkCount > gloveDamageBefore,
            "fully charged boxing glove did not physically uppercut, launch, and break its target");

        var (grenade, grenadeTarget) = Setup(11, new Vector2(880f, 300f));
        var grenadeHeldPosition = grenade.Position;
        grenade.BeginPrimaryAction();
        grenade.SetGrabTarget(grenade.Position + new Vector2(150f, 75f));
        var grenadeBelt = new ConveyorBelt(new Vector2(300f, 455f), 700f, 34f, 0f,
            systemControlled: true);
        var grenadeGrid = new DestructibleGrid(40, 22, 32);
        grenadeGrid.BuildProcessingStation();
        grenadeGrid.OpenContinuousConveyorPortals();
        for (var step = 0; step < 30; step++)
            grenade.Step(Dt, new Vector2(0f, 980f), new[] { grenadeBelt },
                new[] { grenadeTarget }, 1280f, 720f, grid: grenadeGrid);
        Assert(Vector2.Distance(grenadeHeldPosition, grenade.Position) < 1f,
            "grenade kept following the cursor instead of freezing at the LMB aim anchor");
        Assert(grenade.GrenadeTrajectory.Count > 4 && grenade.GrenadeTrajectory[^1].Final,
            "held grenade did not expose its predicted throw and landing arc");
        Assert(grenade.GrenadeTrajectory.Any(point => point.Bounced),
            "grenade trajectory did not mark its predicted conveyor or wall bounce");
        var blobPreviewGrenade = new PhysicalKnife(new Vector2(400f, 300f));
        blobPreviewGrenade.SelectArsenalVisual(11);
        Assert(blobPreviewGrenade.Equip(
                blobPreviewGrenade.Position,
                blobPreviewGrenade.Position) &&
               blobPreviewGrenade.BeginPrimaryAction(),
            "blob-collision grenade preview fixture could not begin aiming");
        var previewBlocker = BlobArchetype.ProcessingUnit.Create(new Vector2(505f, 300f));
        blobPreviewGrenade.SetGrabTarget(blobPreviewGrenade.Position + Vector2.UnitX * 190f);
        for (var step = 0; step < 3; step++)
            blobPreviewGrenade.Step(
                Dt,
                Vector2.Zero,
                Array.Empty<ConveyorBelt>(),
                new[] { previewBlocker },
                1280f,
                720f);
        Assert(blobPreviewGrenade.GrenadeTrajectory.Any(point =>
                point.BodyContact &&
                Vector2.Distance(point.Position, previewBlocker.Center) <=
                previewBlocker.Radius + previewBlocker.ParticleSpacing * 2f),
            "grenade trajectory ignored the blob collider that the live throw would hit");
        Assert(blobPreviewGrenade.GrenadeTrajectory[^1] is
               { BodyContact: true, Final: true } &&
               blobPreviewGrenade.GrenadeTrajectory.Count(point => point.BodyContact) == 1,
            "grenade trajectory kept predicting an erratic path after its first blob contact");

        blobPreviewGrenade.EndPrimaryAction();
        blobPreviewGrenade.Step(
            Dt,
            Vector2.Zero,
            Array.Empty<ConveyorBelt>(),
            new[] { previewBlocker },
            1280f,
            720f);
        Vector2? previousGrenadePosition = null;
        var maximumStationarySteps = 0;
        var stationarySteps = 0;
        var grenadeOverlappedTissue = false;
        for (var step = 0; step < 100; step++)
        {
            blobPreviewGrenade.Step(
                Dt,
                Vector2.Zero,
                Array.Empty<ConveyorBelt>(),
                new[] { previewBlocker },
                1280f,
                720f);
            var liveGrenade = blobPreviewGrenade.ArsenalProjectiles.FirstOrDefault(projectile =>
                projectile.Kind == ArsenalProjectileKind.Grenade);
            if (liveGrenade.Kind != ArsenalProjectileKind.Grenade) continue;
            if (previousGrenadePosition is { } previousGrenade)
            {
                stationarySteps = Vector2.DistanceSquared(previousGrenade, liveGrenade.Position) < 0.16f
                    ? stationarySteps + 1
                    : 0;
                maximumStationarySteps = Math.Max(maximumStationarySteps, stationarySteps);
            }
            previousGrenadePosition = liveGrenade.Position;
            for (var particleIndex = 0;
                 particleIndex < previewBlocker.Particles.Length;
                 particleIndex++)
            {
                if (!previewBlocker.IsPhysicalParticle(particleIndex)) continue;
                var particle = previewBlocker.Particles[particleIndex];
                var minimumDistance =
                    PhysicalKnife.ProjectileRadius(ArsenalProjectileKind.Grenade) +
                    particle.Radius - 0.3f;
                grenadeOverlappedTissue |=
                    Vector2.DistanceSquared(liveGrenade.Position, particle.Position) <
                    minimumDistance * minimumDistance;
            }
        }
        Assert(!grenadeOverlappedTissue && maximumStationarySteps < 3,
            "live grenade embedded in or stopped moving against blob particles");
        grenade.EndPrimaryAction();
        grenade.Step(Dt, new Vector2(0f, 980f), new[] { grenadeBelt },
            new[] { grenadeTarget }, 1280f, 720f, grid: grenadeGrid);
        Assert(grenade.ArsenalProjectiles.Any(projectile =>
                projectile.Kind == ArsenalProjectileKind.Grenade &&
                projectile.RemainingSeconds > 1.7f),
            "holding the grenade still cooked away its post-throw fuse");
        var grenadeEnteredSolid = false;
        for (var step = 1; step < 220; step++)
        {
            grenade.Step(Dt, new Vector2(0f, 980f), new[] { grenadeBelt },
                new[] { grenadeTarget }, 1280f, 720f, grid: grenadeGrid);
            foreach (var projectile in grenade.ArsenalProjectiles)
            {
                if (projectile.Kind != ArsenalProjectileKind.Grenade) continue;
                if (grenadeBelt.ContainsPoint(projectile.Position)) grenadeEnteredSolid = true;
                var cellX = (int)MathF.Floor(projectile.Position.X / grenadeGrid.CellSize);
                var cellY = (int)MathF.Floor(projectile.Position.Y / grenadeGrid.CellSize);
                if (cellX >= 0 && cellY >= 0 && cellX < grenadeGrid.Columns && cellY < grenadeGrid.Rows &&
                    grenadeGrid.Cell(cellX, cellY).IsSolid)
                    grenadeEnteredSolid = true;
            }
        }
        Assert(!grenadeEnteredSolid,
            "physical grenade entered a wall or conveyor instead of colliding and bouncing");
        Assert(grenade.ArsenalExplosionSerial == 1,
            "grenade did not travel and produce its fixed post-throw timed explosion");

        var tubeGrenade = new PhysicalKnife(new Vector2(640f, 230f));
        tubeGrenade.SelectArsenalVisual(11);
        Assert(tubeGrenade.Equip(tubeGrenade.Position, tubeGrenade.Position),
            "tube grenade fixture could not equip");
        Assert(tubeGrenade.BeginPrimaryAction(),
            "tube grenade fixture could not begin aiming");
        tubeGrenade.SetGrabTarget(tubeGrenade.Position - Vector2.UnitY * 180f);
        var overheadTube = new OverheadTubeFeed();
        for (var step = 0; step < 3; step++)
            tubeGrenade.Step(
                Dt,
                new Vector2(0f, 980f),
                Array.Empty<ConveyorBelt>(),
                Array.Empty<SoftBody>(),
                1280f,
                720f,
                overheadTube);
        Assert(tubeGrenade.GrenadeTrajectory.Any(point =>
                point.Bounced &&
                point.Position.Y >= OverheadTubeFeed.GlassBottom + 3.5f &&
                point.Position.Y <= OverheadTubeFeed.GlassBottom + 5f),
            "grenade preview passed through the overhead tube glass");
        tubeGrenade.EndPrimaryAction();
        var grenadeCrossedTube = false;
        for (var step = 0; step < 80; step++)
        {
            tubeGrenade.Step(
                Dt,
                new Vector2(0f, 980f),
                Array.Empty<ConveyorBelt>(),
                Array.Empty<SoftBody>(),
                1280f,
                720f,
                overheadTube);
            grenadeCrossedTube |= tubeGrenade.ArsenalProjectiles.Any(projectile =>
                projectile.Kind == ArsenalProjectileKind.Grenade &&
                projectile.Position.Y <
                OverheadTubeFeed.GlassBottom +
                PhysicalKnife.ProjectileRadius(ArsenalProjectileKind.Grenade) - 0.1f);
        }
        Assert(!grenadeCrossedTube,
            "live grenade passed through the overhead tube instead of bouncing");

        var (cancelGrenade, _) = Setup(11, new Vector2(880f, 300f));
        Assert(cancelGrenade.BeginPrimaryAction(), "grenade cancel fixture could not begin aiming");
        cancelGrenade.SetGrabTarget(cancelGrenade.Position + new Vector2(120f, -40f));
        cancelGrenade.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
            Array.Empty<SoftBody>(), 1280f, 720f);
        Assert(cancelGrenade.BeginRotationAdjust(cancelGrenade.Position),
            "RMB could not cancel an active grenade throw");
        cancelGrenade.EndRotationAdjust();
        cancelGrenade.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
            Array.Empty<SoftBody>(), 1280f, 720f);
        Assert(!cancelGrenade.ArsenalPrimaryHeld && cancelGrenade.GrenadeTrajectory.Count == 0 &&
               cancelGrenade.ArsenalShotSerial == 0,
            "RMB grenade cancel still threw or left an active aiming arc");

        var (axe, axeTarget) = Setup(12, new Vector2(730f, 300f));
        var axeStartAngle = axe.Angle;
        var axeDamageBefore = axeTarget.BrokenLinkCount;
        Assert(axe.BeginPrimaryAction(), "battleaxe rejected its held whirlwind attack");
        for (var step = 0; step < 90; step++)
            axe.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(), new[] { axeTarget }, 1280f, 720f);
        Assert(axe.ArsenalPrimaryHeld &&
               MathF.Abs(axe.Angle - axeStartAngle) > 1f &&
               axeTarget.BrokenLinkCount > axeDamageBefore,
            "holding battleaxe LMB did not continuously spin and damage contacted tissue");
        Assert(axe.EndPrimaryAction(), "battleaxe whirlwind did not stop on release");
        for (var step = 0; step < 8; step++)
            axe.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(), new[] { axeTarget }, 1280f, 720f);
        Assert(!axe.ArsenalPrimaryHeld,
            "battleaxe continued its whirlwind after LMB release");
    }

    private static void ExpandedArsenalMechanicsAreDistinct()
    {
        static PhysicalKnife Equipped(int variant)
        {
            var tool = new PhysicalKnife(new Vector2(640f, 300f));
            tool.SelectArsenalVisual(variant);
            Assert(tool.Equip(tool.Position, tool.Position),
                $"expanded arsenal variant {variant} could not equip");
            return tool;
        }

        static void Click(
            PhysicalKnife tool,
            IReadOnlyList<SoftBody> bodies,
            int steps,
            Vector2 gravity,
            GranularMaterialSystem? granular = null)
        {
            Assert(tool.BeginPrimaryAction(),
                $"expanded arsenal variant {tool.ArsenalVisualVariant} rejected LMB");
            tool.EndPrimaryAction();
            for (var step = 0; step < steps; step++)
                tool.Step(Dt, gravity, Array.Empty<ConveyorBelt>(), bodies,
                    1280f, 720f, granular: granular);
        }

        var blackHole = Equipped(13);
        var blackHoleTarget = BlobArchetype.ProcessingUnit.Create(new Vector2(500f, 300f));
        var blackHoleMatterBefore = blackHoleTarget.PhysicalParticleCount;
        var blackHoleGore = new GranularMaterialSystem();
        Click(blackHole, new[] { blackHoleTarget }, 260, Vector2.Zero, blackHoleGore);
        Assert(blackHoleTarget.PhysicalParticleCount < blackHoleMatterBefore &&
               blackHoleGore.Particles.Count > 0,
            "mini black hole did not pull apart a nearby blob and spit physical gore");

        var ratGun = Equipped(14);
        var ratTarget = BlobArchetype.ProcessingUnit.Create(new Vector2(500f, 300f));
        var ratDamageBefore = ratTarget.BrokenLinkCount;
        Click(ratGun, new[] { ratTarget }, 220, Vector2.Zero);
        Assert(ratGun.Rats.Any(rat => rat.Attached) &&
               ratTarget.BrokenLinkCount > ratDamageBefore,
            "rat projectile did not become a persistent chewing agent on its target");
        var attachedRat = ratGun.Rats.First(rat => rat.Attached);
        var ratHostBefore = ratTarget.Particles[attachedRat.TargetParticleIndex].Position;
        ratTarget.ApplyTranslation(new Vector2(31f, -18f), preserveVelocity: true);
        ratGun.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
            new[] { ratTarget }, 1280f, 720f);
        var followedRat = ratGun.Rats.First(rat => rat.Attached);
        var ratHostAfter = ratTarget.Particles[followedRat.TargetParticleIndex].Position;
        Assert(Vector2.Distance(
                   followedRat.Position - attachedRat.Position,
                   ratHostAfter - ratHostBefore) < 1.5f,
            "attached rat stayed at its world landing point instead of following host deformation");

        for (var extraRat = 0; extraRat < 3; extraRat++)
            Click(ratGun, new[] { ratTarget }, 90, Vector2.Zero);
        Assert(ratGun.Rats.Count(rat => rat.Attached) >= 3,
            "multiple-rat topology fixture failed to attach its swarm before host breakup");

        DamageGestureProfile.Slice(
            ratTarget,
            ratTarget.Center - Vector2.UnitY * ratTarget.Radius * 1.25f,
            ratTarget.Center + Vector2.UnitY * ratTarget.Radius * 1.25f);
        var ratHostPieces = ratTarget.SplitDisconnectedComponents();
        Assert(ratHostPieces.Count >= 2,
            "rat topology-remap fixture did not split its attached host");
        var survivingRatHost = ratHostPieces.MaxBy(piece => piece.PhysicalParticleCount)!;
        foreach (var piece in ratHostPieces)
            if (!ReferenceEquals(piece, survivingRatHost))
                piece.MarkDetachedDebris(Dt);
        ratGun.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
            ratHostPieces, 1280f, 720f);
        var remappedRats = ratGun.Rats.Where(rat => rat.Attached).ToArray();
        Assert(remappedRats.Length >= 3 &&
               remappedRats.All(rat =>
                   rat.Target is not null &&
                   ReferenceEquals(rat.Target, survivingRatHost) &&
                   (uint)rat.TargetParticleIndex < (uint)rat.Target.Particles.Length &&
                   rat.Target.IsPhysicalParticle(rat.TargetParticleIndex)),
            "attached rat swarm did not safely remap host particles after the blob split");
        Assert(!survivingRatHost.IsPhysicalParticle(survivingRatHost.Particles.Length),
            "out-of-range physical-particle queries were not safely rejected");

        var ratBurst = new GranularMaterialSystem();
        ratBurst.BeginStep();
        ratGun.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
            Array.Empty<SoftBody>(), 1280f, 720f, granular: ratBurst);
        Assert(ratGun.Rats.Count == 0 && ratBurst.BloodCount > 0,
            "rat retargeted after its one host disappeared instead of bursting into blood");

        var enlarger = Equipped(15);
        var growthTarget = BlobArchetype.ProcessingUnit.Create(new Vector2(500f, 300f));
        var growthRadiusBefore = growthTarget.Radius;
        var growthMatterBefore = growthTarget.PhysicalParticleCount;
        var growthCenter = growthTarget.Center;
        var growthGore = new GranularMaterialSystem();
        Assert(enlarger.BeginPrimaryAction(), "enlarger rejected its auto-aim hold");
        for (var step = 0; step < 420; step++)
            enlarger.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
                new[] { growthTarget }, 1280f, 720f, granular: growthGore);
        enlarger.EndPrimaryAction();
        Assert(growthTarget.Radius > growthRadiusBefore * 2.4f &&
               growthTarget.PhysicalParticleCount < growthMatterBefore,
            "enlarger did not approach 3x scale and burst its target");
        Assert(growthGore.Particles.Count >= 16 &&
               growthGore.Particles.Count(particle =>
                   Vector2.Dot(particle.Position - growthCenter,
                       particle.Position - particle.PreviousPosition) > 0f) >= 12 &&
               growthGore.Particles.Any(particle => particle.Position.X < growthCenter.X) &&
               growthGore.Particles.Any(particle => particle.Position.X > growthCenter.X) &&
               growthGore.Particles.Any(particle => particle.Position.Y < growthCenter.Y) &&
               growthGore.Particles.Any(particle => particle.Position.Y > growthCenter.Y),
            "enlarger burst spawned inert falling gore instead of a radial explosion");

        var flame = Equipped(16);
        var flameTarget = BlobArchetype.ProcessingUnit.Create(new Vector2(500f, 300f));
        Assert(flame.BeginPrimaryAction(), "flamethrower rejected continuous fire");
        for (var step = 0; step < 160; step++)
            flame.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
                new[] { flameTarget }, 1280f, 720f);
        flame.EndPrimaryAction();
        Assert(flameTarget.BrokenLinkCount > 0 &&
               flame.BurningBlobs.Count > 0 &&
               flame.FlamePatches.Count > 0 &&
               flame.SmokeParticles.Count is > 0 and <= 128,
            "flamethrower did not leave bounded pixel fire, smoke, and a persistent burning blob");
        var travelingFlame = Equipped(16);
        Assert(travelingFlame.BeginPrimaryAction(),
            "flamethrower travel fixture rejected continuous fire");
        travelingFlame.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
            Array.Empty<SoftBody>(), 1280f, 720f);
        travelingFlame.EndPrimaryAction();
        for (var step = 0; step < 72; step++)
            travelingFlame.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
                Array.Empty<SoftBody>(), 1280f, 720f);
        Assert(travelingFlame.ArsenalProjectiles.Any(projectile =>
                projectile.Kind == ArsenalProjectileKind.Flame),
            "flamethrower projectile expired in open air before reaching a surface");

        var freeze = Equipped(17);
        var supportedFreeze = BlobArchetype.ProcessingUnit.Create(new Vector2(640f, 380f));
        var supportedCenterBefore = supportedFreeze.Center;
        for (var particleIndex = 0;
             particleIndex < supportedFreeze.Particles.Length;
             particleIndex++)
        {
            if (!supportedFreeze.IsPhysicalParticle(particleIndex)) continue;
            ref var particle = ref supportedFreeze.Particles[particleIndex];
            if (particle.Position.Y < supportedCenterBefore.Y +
                supportedFreeze.Radius * 0.30f)
                continue;
            particle.Contacting = true;
            particle.ContactMemory = 6;
            particle.PreviousPosition = particle.Position - new Vector2(0f, 4f);
        }
        supportedFreeze.SetFrozen(true, 8f);
        Assert(supportedFreeze.Center.Y <= supportedCenterBefore.Y - 7.5f &&
               supportedFreeze.AverageVelocity(Dt).Length() < 0.1f,
            "grounded freeze expansion bounced instead of quietly shifting above its new ice edge");

        var frozenTarget = BlobArchetype.ProcessingUnit.Create(new Vector2(500f, 300f));
        Click(freeze, new[] { frozenTarget }, 48, Vector2.Zero);
        Assert(freeze.FrozenBlobs.Count == 1,
            "freeze projectile did not put the hit blob into an ice block");
        var ordinaryParticleRadius = frozenTarget.ParticleSpacing * 0.58f;
        Assert(frozenTarget.IsFrozen &&
               frozenTarget.FrozenCollisionPadding >= 7.5f &&
               frozenTarget.Particles
                   .Where((_, index) => frozenTarget.IsPhysicalParticle(index))
                   .All(particle => particle.Radius > ordinaryParticleRadius + 7f),
            "frozen blob collider did not expand to the surrounding ice shell");
        frozenTarget.RegisterHitReaction(2f);
        for (var step = 0; step < 600; step++)
            frozenTarget.AdvanceFaceAnimation(Dt);
        Assert(frozenTarget.FaceExpression == BlobFaceExpression.Neutral,
            "frozen blob continued blinking or playing hurt-face motion");
        for (var step = 0; step < 70; step++)
            freeze.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
                new[] { frozenTarget }, 1280f, 720f);
        var frozenDamageBefore = frozenTarget.BrokenLinkCount;
        frozenTarget.LastTerrainImpact = 500f;
        freeze.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
            new[] { frozenTarget }, 1280f, 720f);
        Assert(freeze.FrozenBlobs.Count == 1 &&
               freeze.FrozenBlobs[0].PendingSplitPropagation &&
               frozenTarget.BrokenLinkCount > frozenDamageBefore,
            "thrown ice block did not shatter its blob on a hard impact");
        var splitCenter = frozenTarget.Center;
        frozenTarget.DamageLine(
            splitCenter - Vector2.UnitY * frozenTarget.Radius,
            splitCenter + Vector2.UnitY * frozenTarget.Radius,
            frozenTarget.ParticleSpacing * 1.1f, 24f, maximumBreaks: 256);
        var frozenChildren = frozenTarget.SplitDisconnectedComponents()
            .Where(child => child.PhysicalParticleCount > 3)
            .ToArray();
        Assert(frozenChildren.Length > 0,
            "frozen shatter did not produce any physical child chunks");
        freeze.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
            frozenChildren, 1280f, 720f);
        Assert(freeze.FrozenBlobs.Count == frozenChildren.Length &&
               frozenChildren.All(child => freeze.FrozenBlobs.Any(state =>
                   ReferenceEquals(state.Body, child) && state.Generation >= 1)),
            "ice state did not propagate to the chunks produced by a frozen shatter");
        foreach (var frozenChild in frozenChildren)
        {
            frozenChild.LastTerrainImpact = 0f;
            frozenChild.LastImpact = 0f;
        }
        for (var step = 0; step < 70; step++)
            freeze.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
                frozenChildren, 1280f, 720f);
        var terminalFrozen = frozenChildren[0];
        terminalFrozen.LastTerrainImpact = 500f;
        var frozenGore = new GranularMaterialSystem();
        freeze.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
            frozenChildren, 1280f, 720f, granular: frozenGore);
        Assert(freeze.FrozenBlobs.Count < frozenChildren.Length &&
               terminalFrozen.IsCrumbling &&
               !terminalFrozen.IsFrozen &&
               frozenGore.Particles.Count > 0,
            "second impact did not terminally shatter a frozen child into ordinary gore");

        var lightning = Equipped(18);
        var lightningFirst = BlobArchetype.ProcessingUnit.Create(new Vector2(500f, 300f));
        var lightningSecond = BlobArchetype.ProcessingUnit.Create(new Vector2(420f, 300f));
        Click(lightning, new[] { lightningFirst, lightningSecond }, 80, Vector2.Zero);
        Assert(lightningFirst.BrokenLinkCount > 0 && lightningSecond.BrokenLinkCount > 0,
            "lightning seed did not arc from the first struck blob to a nearby blob");

        var acid = Equipped(19);
        var acidTarget = BlobArchetype.ProcessingUnit.Create(new Vector2(505f, 265f));
        var acidDamageBefore = acidTarget.BrokenLinkCount;
        var acidGranular = new GranularMaterialSystem();
        var acidGrid = new DestructibleGrid(40, 22, 32);
        Assert(acid.BeginPrimaryAction(), "acid lobber rejected charge");
        for (var step = 0; step < 45; step++)
            acid.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
                new[] { acidTarget }, 1280f, 720f,
                granular: acidGranular);
        acid.EndPrimaryAction();
        for (var step = 0; step < 180; step++)
        {
            acid.Step(Dt, new Vector2(0f, 980f), Array.Empty<ConveyorBelt>(),
                new[] { acidTarget }, 1280f, 720f,
                granular: acidGranular);
            acidGranular.BeginStep();
            acidGranular.Step(
                Dt,
                new Vector2(0f, 980f),
                acidGrid,
                new[] { acidTarget });
        }
        Assert(acidGranular.AcidCount >= 12 &&
               acidTarget.BrokenLinkCount > acidDamageBefore,
            "acid ball did not burst into physical corrosive pixels that burn tissue");
        Assert(acidGranular.Particles.Where(particle =>
                    particle.Kind == GranularKind.Acid)
                .Select(particle => particle.Position.X)
                .Distinct()
                .Count() >= 5,
            "acid burst remained a single hovering pool instead of spreading physically");
        var acidLine = new ProcessingLine(
            DestructibleGrid.ProcessingDeckRow * 32f,
            continuousFlow: true);
        var acidDrainProbe = new GranularParticle
        {
            Position = new Vector2(
                acidLine.ContinuousDrainCollectorBounds.Left + 12f,
                acidLine.ContinuousDrainCollectorBounds.Top + 5f),
            PreviousPosition = new Vector2(
                acidLine.ContinuousDrainCollectorBounds.Left + 10f,
                acidLine.ContinuousDrainCollectorBounds.Top + 5f),
            Radius = 2f,
            Lifetime = 5f,
            Kind = GranularKind.Acid
        };
        Assert(!acidLine.RouteThroughContinuousEndDrain(ref acidDrainProbe, Dt) &&
               !acidLine.TryCollectBasinInflow(ref acidDrainProbe, Dt),
            "physical acid was incorrectly routed into the blood basin");
        var acidConveyor = new ConveyorBelt(
            new Vector2(320f, 390f),
            420f,
            28f,
            150f);
        var flowingAcid = new GranularMaterialSystem();
        flowingAcid.Particles.Add(new GranularParticle
        {
            Position = new Vector2(430f, acidConveyor.Position.Y - 5f),
            PreviousPosition = new Vector2(430f, acidConveyor.Position.Y - 5f),
            Radius = 2.4f,
            Lifetime = 8f,
            Kind = GranularKind.Acid
        });
        var acidStartX = flowingAcid.Particles[0].Position.X;
        for (var step = 0; step < 120; step++)
        {
            flowingAcid.BeginStep();
            flowingAcid.Step(
                Dt,
                new Vector2(0f, 980f),
                acidGrid,
                Array.Empty<SoftBody>(),
                new[] { acidConveyor });
        }
        Assert(flowingAcid.AcidCount == 1 &&
               flowingAcid.Particles[0].Position.X > acidStartX + 18f,
            "acid pixel floated in world space instead of riding the conveyor like blood");

        var water = Equipped(20);
        var waterTarget = BlobArchetype.ProcessingUnit.Create(new Vector2(500f, 300f));
        var tearExplosionBefore = water.ArsenalExplosionSerial;
        var tearGore = new GranularMaterialSystem();
        for (var tear = 0; tear < 5; tear++)
            Click(water, new[] { waterTarget }, 70, Vector2.Zero, tearGore);
        Assert(waterTarget.AverageVelocity(Dt).Length() > 20f &&
               waterTarget.HitFlash01 > 0f &&
               water.ArsenalExplosionSerial > tearExplosionBefore &&
               tearGore.Particles.Count > 0,
            "water doll tears lacked the red hit response or five-hit explosive payoff");

        var baseball = Equipped(21);
        var baseballTarget = BlobArchetype.ProcessingUnit.Create(new Vector2(430f, 300f));
        Assert(baseball.BeginPrimaryAction() && baseball.EndPrimaryAction(),
            "bat loadout did not lob its ball on the first LMB");
        for (var step = 0; step < 12; step++)
            baseball.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
                new[] { baseballTarget }, 1280f, 720f);
        Assert(baseball.BaseballInPlay,
            "baseball did not remain as a physical re-hittable projectile");
        Assert(baseball.BeginPrimaryAction() && baseball.EndPrimaryAction(),
            "bat did not activate after its ball was in play");
        for (var step = 0; step < 90; step++)
            baseball.Step(Dt, Vector2.Zero, Array.Empty<ConveyorBelt>(),
                new[] { baseballTarget }, 1280f, 720f);
        Assert(baseballTarget.BrokenLinkCount > 0,
            $"batted baseball did not become the promised high-speed damaging projectile " +
            $"(target={baseballTarget.Center}, shots={baseball.ArsenalShotSerial}, " +
            $"ball={string.Join(", ", baseball.ArsenalProjectiles.Select(projectile =>
                $"{projectile.Kind}@{projectile.Position} v={projectile.Velocity}"))})");
    }

    private static void ExplosiveFracturesHaveVariedSpin()
    {
        var center = new Vector2(500f, 300f);
        var body = BlobArchetype.ProcessingUnit.Create(center);
        body.AddRadialExplosion(center, 300f, 860f, Dt);
        var cutRadius = MathF.Max(3f, body.ParticleSpacing * 0.78f);
        body.DamageLine(
            center - Vector2.UnitX * body.Radius,
            center + Vector2.UnitX * body.Radius,
            cutRadius, 22f, maximumBreaks: 128);
        body.DamageLine(
            center - Vector2.UnitY * body.Radius,
            center + Vector2.UnitY * body.Radius,
            cutRadius, 22f, maximumBreaks: 128);
        body.DamageLine(
            center + new Vector2(-body.Radius, -body.Radius),
            center + new Vector2(body.Radius, body.Radius),
            cutRadius, 20f, maximumBreaks: 96);
        body.DamageLine(
            center + new Vector2(-body.Radius, body.Radius),
            center + new Vector2(body.Radius, -body.Radius),
            cutRadius, 20f, maximumBreaks: 96);

        var fragments = body.SplitDisconnectedComponents();
        Assert(fragments.Count >= 3,
            $"explosive fracture produced too few chunks for launch variation ({fragments.Count})");

        var outwardFragments = 0;
        var clockwiseFragments = 0;
        var counterClockwiseFragments = 0;
        var spinningFragments = 0;
        foreach (var fragment in fragments)
        {
            var radial = fragment.Center - center;
            if (radial.LengthSquared() > 0.001f &&
                Vector2.Dot(fragment.AverageVelocity(Dt), Vector2.Normalize(radial)) > 150f)
                outwardFragments++;

            if (fragment.PhysicalParticleCount < 2) continue;
            var averageVelocity = fragment.AverageVelocity(Dt);
            var angularNumerator = 0f;
            var angularDenominator = 0f;
            foreach (var particleIndex in Enumerable.Range(0, fragment.Particles.Length))
            {
                if (!fragment.IsPhysicalParticle(particleIndex)) continue;
                var offset = fragment.Particles[particleIndex].Position - fragment.Center;
                var relativeVelocity =
                    (fragment.Particles[particleIndex].Position -
                     fragment.Particles[particleIndex].PreviousPosition) / Dt -
                    averageVelocity;
                angularNumerator +=
                    offset.X * relativeVelocity.Y - offset.Y * relativeVelocity.X;
                angularDenominator += offset.LengthSquared();
            }
            if (angularDenominator <= 0.001f) continue;
            var angularVelocity = angularNumerator / angularDenominator;
            if (MathF.Abs(angularVelocity) < 0.45f) continue;
            spinningFragments++;
            if (angularVelocity < 0f) clockwiseFragments++;
            else counterClockwiseFragments++;
        }

        Assert(outwardFragments >= Math.Max(2, fragments.Count - 1),
            $"explosive chunks lost their outward launch ({outwardFragments}/{fragments.Count})");
        Assert(spinningFragments >= 2 &&
               clockwiseFragments > 0 &&
               counterClockwiseFragments > 0,
            $"explosive chunks did not receive varied two-way spin " +
            $"(spinning={spinningFragments}, cw={clockwiseFragments}, ccw={counterClockwiseFragments})");
    }

    private static void ContinuousEndDrainRoutesMatter()
    {
        var line = new ProcessingLine(480f, powered: true, continuousFlow: true);
        var midBelt = new GranularParticle
        {
            Position = new Vector2(620f, line.DeckY + 2f),
            PreviousPosition = new Vector2(618f, line.DeckY + 2f),
            Radius = 2f,
            Kind = GranularKind.Blood
        };
        Assert(!line.RouteThroughContinuousEndDrain(ref midBelt, Dt),
            "blood disappeared into a drain before traversing the full conveyor");

        // The retired per-bay drain routine used to attract material toward these
        // five centers even in continuous mode. Collision resolution must now leave
        // every old aperture position completely untouched.
        foreach (var bay in line.Bays)
        {
            var formerDrain = new Particle
            {
                Position = new Vector2(bay.CenterX + 18f, line.DeckY - 1f),
                PreviousPosition = new Vector2(bay.CenterX + 15f, line.DeckY - 1f),
                Radius = 2f,
                InverseMass = 1f
            };
            var originalPosition = formerDrain.Position;
            var originalPrevious = formerDrain.PreviousPosition;
            line.ResolveGranular(ref formerDrain, Dt, GranularKind.Blood);
            Assert(formerDrain.Position == originalPosition && formerDrain.PreviousPosition == originalPrevious,
                $"continuous-belt blood was still attracted toward retired drain at x={bay.CenterX:0}");
        }

        var blood = midBelt;
        blood.Position = new Vector2(line.ContinuousDrainCollectorBounds.Left +
                                     line.ContinuousDrainCollectorBounds.Width * 0.5f, line.DeckY + 1f);
        blood.PreviousPosition = blood.Position - new Vector2(2f, 0f);
        Assert(line.RouteThroughContinuousEndDrain(ref blood, Dt),
            "the single exit collector rejected blood at its opening");
        for (var step = 0; step < 180; step++)
            line.RouteThroughContinuousEndDrain(ref blood, Dt);
        Assert(blood.Position.X < line.Basin.Right && blood.Position.Y > line.Basin.Top,
            $"blood jammed before the basin pipe mouth ({blood.Position})");

        var tissue = midBelt;
        tissue.Kind = GranularKind.Tissue;
        tissue.Position = new Vector2(line.ContinuousDrainCollectorBounds.Left +
                                      line.ContinuousDrainCollectorBounds.Width * 0.5f + 8f, line.DeckY + 1f);
        tissue.PreviousPosition = tissue.Position;
        Assert(line.RouteThroughContinuousEndDrain(ref tissue, Dt),
            "granular tissue chunk was rejected by the single exit collector");

        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        grid.OpenContinuousConveyorPortals();
        var integratedLine = new ProcessingLine(480f, powered: true, continuousFlow: true);
        var granular = new GranularMaterialSystem();
        granular.Particles.Add(new GranularParticle
        {
            Position = new Vector2(integratedLine.ContinuousDrainPipeEntry.X - 8f, integratedLine.DeckY + 1f),
            PreviousPosition = new Vector2(integratedLine.ContinuousDrainPipeEntry.X - 10f, integratedLine.DeckY + 1f),
            Radius = 2.3f,
            Lifetime = 20f,
            Kind = GranularKind.Blood
        });
        granular.Particles.Add(new GranularParticle
        {
            Position = new Vector2(integratedLine.ContinuousDrainPipeEntry.X + 8f, integratedLine.DeckY + 1f),
            PreviousPosition = new Vector2(integratedLine.ContinuousDrainPipeEntry.X + 6f, integratedLine.DeckY + 1f),
            Radius = 2.8f,
            Lifetime = 20f,
            Kind = GranularKind.Tissue,
            Appearance = GranularAppearance.BlobMint
        });
        var noBodies = new List<SoftBody>();
        for (var step = 0; step < 600; step++)
        {
            integratedLine.PreStep(noBodies, granular.Particles, Dt);
            granular.BeginStep();
            granular.Step(Dt, new Vector2(0f, 980f), grid, noBodies,
                integratedLine.Belts, processingLine: integratedLine);
        }
        Assert(integratedLine.Basin.StoredVolume > 0f && granular.Particles.Count == 0,
            $"integrated end drain left matter jammed instead of feeding the basin " +
            $"({granular.Particles.Count} pixels at {string.Join(", ", granular.Particles.Select(p => p.Position))}, " +
            $"{integratedLine.Basin.StoredVolume:0.00} stored)");
    }

    private static void BlobPersonalitiesProduceVariedHops()
    {
        var population = new List<SoftBody>(48);
        for (var index = 0; index < 48; index++)
            population.Add(BlobArchetype.ProcessingUnit.Create(new Vector2(320f, 380f)));
        var playful = population.Where(body => body.PersonalityCanHop).ToArray();
        var quiet = population.Where(body => !body.PersonalityCanHop).ToArray();
        Assert(playful.Length >= 12 && quiet.Length >= 8,
            $"personality distribution did not retain both playful and quiet blobs " +
            $"({playful.Length} playful / {quiet.Length} quiet)");
        Assert(playful.Max(body => body.PersonalityHopSpeed) -
               playful.Min(body => body.PersonalityHopSpeed) > 55f &&
               playful.Max(body => body.PersonalityJumpiness) -
               playful.Min(body => body.PersonalityJumpiness) > 0.45f,
            "playful blobs did not receive meaningful height and cadence variance");

        var jumper = playful[0];
        var groundWorld = new BlobWorld(FlatGrid())
        {
            EnableBlobPersonalities = true
        };
        groundWorld.Bodies.Add(jumper);
        var launchY = float.NaN;
        var highestY = float.MaxValue;
        var stepsAfterLaunch = 0;
        for (var step = 0; step < 1800; step++)
        {
            var hopsBefore = jumper.PersonalityHopCount;
            groundWorld.Step(Dt);
            if (jumper.PersonalityHopCount > hopsBefore)
            {
                launchY = jumper.Center.Y;
                highestY = launchY;
                stepsAfterLaunch = 0;
            }
            if (float.IsNaN(launchY)) continue;
            highestY = MathF.Min(highestY, jumper.Center.Y);
            stepsAfterLaunch++;
            if (stepsAfterLaunch >= 120) break;
        }
        var rise = launchY - highestY;
        Assert(jumper.PersonalityHopCount > 0 &&
               jumper.LastPersonalityHopSpeed >= 145f &&
               rise >= 7f && rise <= 78f,
            $"supported personality hop was missing or physically unreasonable " +
            $"(speed {jumper.LastPersonalityHopSpeed:0.0}, rise {rise:0.0})");

        var quietBody = quiet[0];
        var quietCenter = quietBody.Center;
        for (var particleIndex = 0;
             particleIndex < quietBody.Particles.Length;
             particleIndex++)
        {
            ref var particle = ref quietBody.Particles[particleIndex];
            if (particle.Position.Y < quietCenter.Y) continue;
            particle.Supported = true;
            particle.SupportMemory = 10;
        }
        for (var step = 0; step < 1800; step++)
            Assert(!quietBody.TryApplyPersonalityHop(Dt, inTube: false),
                "quiet personality unexpectedly generated a hop");

        SoftBody? tubePersonality = null;
        var tubeFeed = new OverheadTubeFeed
        {
            SpawnInterval = 30f,
            MaximumBodiesInFactory = 1,
            EnableBlobPersonalities = true
        };
        var tubeWorld = new BlobWorld(FlatGrid())
        {
            Gravity = Vector2.Zero,
            TubeFeed = tubeFeed,
            EnableBlobPersonalities = true
        };
        SoftBody CreatePlayfulTubeBody(Vector2 position)
        {
            SoftBody candidate;
            do
            {
                candidate = BlobArchetype.ProcessingUnit.Create(position);
            } while (!candidate.PersonalityCanHop);
            tubePersonality = candidate;
            return candidate;
        }

        for (var step = 0;
             step < 1000 &&
             (tubePersonality is null || tubePersonality.PersonalityHopCount == 0);
             step++)
        {
            tubeFeed.Update(tubeWorld.Bodies, Dt, CreatePlayfulTubeBody);
            tubeWorld.Step(Dt);
        }
        Assert(tubePersonality is
               {
                   PersonalityHopCount: > 0,
                   LastPersonalityHopWasInTube: true
               } &&
               tubePersonality.LastPersonalityHopSpeed >= 60f &&
               tubePersonality.LastPersonalityHopSpeed <= 150f,
            "playful personality did not contribute a bounded air-assisted tube hop");
    }

    private static DestructibleGrid FlatGrid()
    {
        var grid = new DestructibleGrid(20, 15, 32);
        for (var x = 0; x < grid.Columns; x++) grid.Set(x, grid.Rows - 1, CellMaterial.Steel);
        for (var y = 0; y < grid.Rows; y++)
        {
            grid.Set(0, y, CellMaterial.Steel);
            grid.Set(grid.Columns - 1, y, CellMaterial.Steel);
        }
        return grid;
    }

    private static bool HasNoDanglingBridge(SoftBody body)
    {
        for (var excluded = 0; excluded < body.Constraints.Count; excluded++)
        {
            if (body.Constraints[excluded].Broken) continue;
            var edge = body.Constraints[excluded];
            var visited = new bool[body.Particles.Length];
            var queue = new Queue<int>();
            queue.Enqueue(edge.A);
            visited[edge.A] = true;
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                for (var i = 0; i < body.Constraints.Count; i++)
                {
                    if (i == excluded || body.Constraints[i].Broken) continue;
                    var candidate = body.Constraints[i];
                    var neighbor = candidate.A == current ? candidate.B : candidate.B == current ? candidate.A : -1;
                    if (neighbor < 0 || visited[neighbor]) continue;
                    visited[neighbor] = true;
                    queue.Enqueue(neighbor);
                }
            }
            if (!visited[edge.B]) return false;
        }
        return true;
    }

    private static float PolygonPerimeter(ReadOnlySpan<Vector2> points)
    {
        var perimeter = 0f;
        for (var i = 0; i < points.Length; i++)
            perimeter += Vector2.Distance(points[i], points[(i + 1) % points.Length]);
        return perimeter;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
