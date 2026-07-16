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
            ("factory tiles follow structural topology", FactoryTilesFollowStructure),
            ("holding chamber contains releases and feeds one at a time", HoldingChamberFeedsOneAtATime),
            ("lever holds the hatch and pixel lighting stays cached", LeverAndLightingAreDeterministic),
            ("main breaker requires a downward handle pull", BreakerRequiresDownwardHandlePull),
            ("holding chamber receives and releases a full soft body", HoldingChamberReceivesAndReleasesBody),
            ("powered receiving tub replaces the chamber support tower", ReceivingTubReplacesTerrainTower),
            ("released blobs cannot re-enter the holding chamber", ReleasedBlobCannotReenterChamber),
            ("blood treats the chamber tube as environment", BloodTreatsChamberTubeAsEnvironment),
            ("processing line has independent back-pressure segments", ProcessingLineBackPressureIsIndependent),
            ("spike crusher locks damages and releases one blob", SpikeCrusherCycleIsLocalized),
            ("bay two spike drill holds damages and releases one blob", SpikeDrillCycleRequiresHeldLever),
            ("bays three through five use distinct player-operated machinery", FinalMachineControlsAreDistinct),
            ("machine drains feed the conserved blood basin", MachineDrainsFeedBasin),
            ("basin inflow sloshes internally without staining the floor below", BasinInflowStaysContained),
            ("full basin locks safely and shop spending releases the line", FullBasinLocksAndShopSpendingUnlocks),
            ("basin blood uses conserved sleeping cellular fluid", BasinFluidIsCellularAndConserved),
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
            ("event damage destroys terrain cell", ImpactDamageDestroysCell),
            ("tissue bonds are damageable", TissueBondsBreak),
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
            ("blood paints terrain and dries persistently", BloodPaintsAndDriesOnTerrain),
            ("dried stains wait for explicit cleaning", DriedStainsWaitForCleaning),
            ("fresh blood diversifies dried runoff", FreshBloodDiversifiesDriedRunoff),
            ("terrain runoff stays in front of tile interiors", TerrainRunoffStaysInFrontOfTileInteriors),
            ("wall blood survives stain churn and forms runoff", WallBloodSurvivesChurnAndDrips),
            ("active blood trails survive stain-layer churn", ActiveBloodTrailsSurviveStainChurn),
            ("old saturated blood zones can renew runoff", SaturatedBloodZoneRenewsRunoff),
            ("settled blood pools keep staining a saturated floor", SettledBloodPoolKeepsStaining),
            ("mass damage remains event-budgeted", MassDamageRemainsBudgeted)
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
                $"viewport {viewport.Size} did not use the available fullscreen scale {expectedSize}");
        }
        Assert(ViewportLayout.Fit(new Size(1920, 1080), logical) == new Rectangle(0, 0, 1920, 1080),
            "16:9 fullscreen left black bars around the play area");
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
        for (var i = 0; i < 125; i++) world.Step(Dt);
        Assert(line.DrillTravel > 0.98f, "held drill lever did not lower the rotating bit");
        Assert(line.DrillBrokenLinks > 0,
            $"drill contact produced no localized structural wound (pulses {line.DrillDamagePulses}, " +
            $"center {line.DrillLockedBody?.Center}, tip {line.DrillTip})");
        Assert(line.DrillBrokenLinks < 55,
            $"single drill cycle caused unbounded repeated damage ({line.DrillBrokenLinks} links)");

        line.SetDrillLeverHeld(false);
        for (var i = 0; i < 90; i++) world.Step(Dt);
        Assert(line.DrillTravel < 0.01f && line.DrillLockedBody is null,
            "drill failed to retract and release after the lever was released");
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
                if (pixel.R > 180 && pixel.G < 75 && pixel.B < 75) visibleForegroundPixels++;
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
                var redBlobPixels = 0;
                var center = line.DrumCenter;
                for (var y = (int)(center.Y - 38f); y <= (int)(center.Y + 38f); y++)
                for (var x = (int)(center.X - 38f); x <= (int)(center.X + 38f); x++)
                {
                    var pixel = drumRender.GetPixel(x, y);
                    if (pixel.R > 180 && pixel.G < 70 && pixel.B < 70) redBlobPixels++;
                }
                Assert(redBlobPixels >= 30,
                    $"spinning blob was not visibly rendered inside the drum ({redBlobPixels} red pixels)");
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

    private static void FullBasinLocksAndShopSpendingUnlocks()
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
        Assert(line.MachineryLockedByStorage && line.Belts.All(belt => MathF.Abs(belt.Speed) < 0.001f),
            "100% storage did not stop every processing-line conveyor");
        Assert(Enumerable.Range(0, line.Bays.Count).All(line.IsBayInUse),
            "100% storage did not put every machine into its red locked state");

        var itemBounds = line.BloodShopItemBounds(0);
        var itemPoint = new Vector2(itemBounds.Left + itemBounds.Width * 0.5f,
            itemBounds.Top + itemBounds.Height * 0.5f);
        var beforePurchase = basin.SpendableBlood;
        Assert(line.TryActivateBloodShop(itemPoint) && line.BloodShopItems[0].Purchased,
            "blood exchange did not purchase an affordable upgrade socket");
        Assert(MathF.Abs(beforePurchase - basin.SpendableBlood - line.BloodShopItems[0].Cost) < 0.02f,
            "upgrade socket price was not deducted from the conserved basin value");
        Assert(!line.MachineryLockedByStorage, "spending below capacity did not release the storage interlock");

        world.Step(Dt);
        Assert(line.Belts.All(belt => MathF.Abs(belt.Speed - ProcessingLine.OperatingSpeed) < 0.001f),
            "processing conveyors did not restart after the basin dropped below 100%");
        var relief = line.BloodShopReliefBounds;
        var reliefPoint = new Vector2(relief.Left + relief.Width * 0.5f, relief.Top + relief.Height * 0.5f);
        var beforeRelief = basin.SpendableBlood;
        Assert(line.TryActivateBloodShop(reliefPoint) &&
               MathF.Abs(beforeRelief - basin.SpendableBlood - ProcessingLine.ReliefValveCost) < 0.02f,
            "repeatable purge control did not spend its displayed basin price");
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

        using var bitmap = new Bitmap(1280, 720);
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


    public static int WriteStationSnapshot(string outputPath)
    {
        var grid = new DestructibleGrid(40, 22, 32);
        grid.BuildProcessingStation();
        var chamber = HoldingChamber.CreateProcessingStation();
        var world = new BlobWorld(grid) { HoldingChamber = chamber };
        world.Lighting.ConfigureProcessingStation();
        var feed = new ChamberFeedController(chamber);
        world.ProcessingLine = new ProcessingLine(DestructibleGrid.ProcessingDeckRow * grid.CellSize);
        world.Conveyors.AddRange(world.ProcessingLine.Belts);
        for (var i = 0; i < 600; i++)
        {
            feed.Update(world.Bodies, Dt, BlobArchetype.ProcessingUnit.Create);
            world.Step(Dt);
        }
        var cart = world.ProcessingLine.CartDockBounds;
        world.Bodies.Add(BlobArchetype.ProcessingUnit.Create(
            new Vector2(cart.Left + cart.Width * 0.5f, cart.Top - 20f)));
        for (var i = 0; i < 90; i++) world.Step(Dt);
        // Seed the diagnostic render with a representative, non-gameplay basin load so the
        // translucent filtered surface and live volume gauge are visible while Diego is dormant.
        for (var i = 0; i < 220; i++)
        {
            world.ProcessingLine.Basin.AddMaterial(
                world.ProcessingLine.Basin.Left + 55f + i % 70 * 10.5f,
                fluidVolume: 46f,
                downwardSpeed: 28f + i % 9,
                nutrition: 0.045f);
        }
        for (var i = 0; i < 180; i++) world.Step(Dt);
        using var bitmap = new Bitmap(1280, 720);
        using var graphics = Graphics.FromImage(bitmap);
        new GameRenderer().Draw(graphics, bitmap.Size, world, null);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine($"Station snapshot: {Path.GetFullPath(outputPath)}");
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
        using var bitmap = new Bitmap(640, 480);
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
        for (var step = 0; step < 90; step++)
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
        Assert(conveyor.BloodStains.Count == 1 && conveyor.BloodStains[0].Wetness <= 0.001f &&
               conveyor.BloodStains[0].Amount >= 0.01f,
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
        for (var deposit = 0; deposit < 14; deposit++)
            loopBelt.DepositBlood(loopBelt.Position + new Vector2(70f, 0f), -Vector2.UnitY, 0.20f);
        for (var step = 0; step < 960 && loopBelt.TransientDrops.Count == 0; step++) loopBelt.Step(Dt);
        Assert(loopBelt.TransientDrops.Count > 0, "dense conveyor pool never shed an occasional underside pixel");
        var transientPosition = loopBelt.TransientDrops[0].Position;
        var transientLifetime = loopBelt.TransientDrops[0].Lifetime;
        for (var step = 0; step < 8; step++) loopBelt.Step(Dt);
        Assert(loopBelt.TransientDrops.Count <= 6, "conveyor exceeded its transient blood-pixel cap");
        Assert(loopBelt.TransientDrops.Count == 0 ||
               (loopBelt.TransientDrops[0].Position.Y > transientPosition.Y &&
                loopBelt.TransientDrops[0].Lifetime < transientLifetime),
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
