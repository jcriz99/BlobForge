using BlobForge.Diagnostics;

namespace BlobForge;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            return SelfTests.RunAll();
        }
        if (args.Contains("--contour-benchmark", StringComparer.OrdinalIgnoreCase))
        {
            return SelfTests.RunContourBenchmark();
        }
        if (args.Contains("--paint-benchmark", StringComparer.OrdinalIgnoreCase))
        {
            return SelfTests.RunPaintBenchmark();
        }
        if (args.Contains("--render-benchmark", StringComparer.OrdinalIgnoreCase))
        {
            return SelfTests.RunRenderBenchmark();
        }
        if (args.Contains("--station-render-benchmark", StringComparer.OrdinalIgnoreCase))
        {
            return SelfTests.RunStationRenderBenchmark();
        }
        if (args.Contains("--audio-loop-benchmark", StringComparer.OrdinalIgnoreCase))
        {
            return SelfTests.RunAudioLoopBenchmark();
        }
        if (args.Contains("--granular-render-benchmark", StringComparer.OrdinalIgnoreCase))
        {
            return SelfTests.RunGranularRenderBenchmark();
        }
        if (args.Contains("--granular-simulation-benchmark", StringComparer.OrdinalIgnoreCase))
        {
            return SelfTests.RunGranularSimulationBenchmark();
        }
        if (args.Contains("--factory-stress-benchmark", StringComparer.OrdinalIgnoreCase))
        {
            return SelfTests.RunFactoryStressBenchmark();
        }
        if (args.Contains("--spread-population-benchmark", StringComparer.OrdinalIgnoreCase))
        {
            return SelfTests.RunSpreadPopulationBenchmark();
        }
        var snapshotArgument = Array.FindIndex(args,
            argument => argument.Equals("--station-snapshot", StringComparison.OrdinalIgnoreCase));
        if (snapshotArgument >= 0)
        {
            var output = snapshotArgument + 1 < args.Length
                ? args[snapshotArgument + 1]
                : Path.Combine(AppContext.BaseDirectory, "station-preview.png");
            return SelfTests.WriteStationSnapshot(output);
        }
        var arsenalMenuSnapshotArgument = Array.FindIndex(args,
            argument => argument.Equals("--arsenal-menu-snapshot", StringComparison.OrdinalIgnoreCase));
        if (arsenalMenuSnapshotArgument >= 0)
        {
            var output = arsenalMenuSnapshotArgument + 1 < args.Length
                ? args[arsenalMenuSnapshotArgument + 1]
                : Path.Combine(AppContext.BaseDirectory, "arsenal-menu-preview.png");
            return SelfTests.WriteArsenalMenuSnapshot(output);
        }
        var basinOverflowSnapshotArgument = Array.FindIndex(args,
            argument => argument.Equals("--basin-overflow-snapshot", StringComparison.OrdinalIgnoreCase));
        if (basinOverflowSnapshotArgument >= 0)
        {
            var output = basinOverflowSnapshotArgument + 1 < args.Length
                ? args[basinOverflowSnapshotArgument + 1]
                : Path.Combine(AppContext.BaseDirectory, "basin-overflow-preview.png");
            return SelfTests.WriteBasinOverflowSnapshot(output);
        }
        var bloodShipmentSnapshotArgument = Array.FindIndex(args,
            argument => argument.Equals("--blood-shipment-snapshot", StringComparison.OrdinalIgnoreCase));
        if (bloodShipmentSnapshotArgument >= 0)
        {
            var output = bloodShipmentSnapshotArgument + 1 < args.Length
                ? args[bloodShipmentSnapshotArgument + 1]
                : Path.Combine(AppContext.BaseDirectory, "blood-shipment-preview.png");
            return SelfTests.WriteBloodShipmentSnapshot(output);
        }
        var payoutHandoffSnapshotArgument = Array.FindIndex(args,
            argument => argument.Equals("--payout-handoff-snapshot", StringComparison.OrdinalIgnoreCase));
        if (payoutHandoffSnapshotArgument >= 0)
        {
            var output = payoutHandoffSnapshotArgument + 1 < args.Length
                ? args[payoutHandoffSnapshotArgument + 1]
                : Path.Combine(AppContext.BaseDirectory, "payout-handoff-preview.png");
            ApplicationConfiguration.Initialize();
            using var window = new GameWindow();
            return window.WritePayoutHandoffSnapshot(output);
        }
        var granularOverflowSnapshotArgument = Array.FindIndex(args,
            argument => argument.Equals("--granular-overflow-snapshot", StringComparison.OrdinalIgnoreCase));
        if (granularOverflowSnapshotArgument >= 0)
        {
            var output = granularOverflowSnapshotArgument + 1 < args.Length
                ? args[granularOverflowSnapshotArgument + 1]
                : Path.Combine(AppContext.BaseDirectory, "granular-overflow-preview.png");
            return SelfTests.WriteGranularOverflowSnapshot(output);
        }
        var cleaverEffectsSnapshotArgument = Array.FindIndex(args,
            argument => argument.Equals("--cleaver-effects-snapshot", StringComparison.OrdinalIgnoreCase));
        if (cleaverEffectsSnapshotArgument >= 0)
        {
            var output = cleaverEffectsSnapshotArgument + 1 < args.Length
                ? args[cleaverEffectsSnapshotArgument + 1]
                : Path.Combine(AppContext.BaseDirectory, "cleaver-effects-preview.png");
            return SelfTests.WriteCleaverEffectsSnapshot(output);
        }
        var faceSnapshotArgument = Array.FindIndex(args,
            argument => argument.Equals("--face-snapshot", StringComparison.OrdinalIgnoreCase));
        if (faceSnapshotArgument >= 0)
        {
            var output = faceSnapshotArgument + 1 < args.Length
                ? args[faceSnapshotArgument + 1]
                : Path.Combine(AppContext.BaseDirectory, "face-preview.png");
            return SelfTests.WriteFaceSnapshot(output);
        }
        var drillSnapshotArgument = Array.FindIndex(args,
            argument => argument.Equals("--drill-snapshot", StringComparison.OrdinalIgnoreCase));
        if (drillSnapshotArgument >= 0)
        {
            var output = drillSnapshotArgument + 1 < args.Length
                ? args[drillSnapshotArgument + 1]
                : Path.Combine(AppContext.BaseDirectory, "drill-preview.png");
            return SelfTests.WriteDrillSnapshot(output);
        }
        var drumSnapshotArgument = Array.FindIndex(args,
            argument => argument.Equals("--drum-snapshot", StringComparison.OrdinalIgnoreCase));
        if (drumSnapshotArgument >= 0)
        {
            var output = drumSnapshotArgument + 1 < args.Length
                ? args[drumSnapshotArgument + 1]
                : Path.Combine(AppContext.BaseDirectory, "drum-preview.png");
            return SelfTests.WriteDrumSnapshot(output);
        }
        var workerSnapshotArgument = Array.FindIndex(args,
            argument => argument.Equals("--worker-snapshot", StringComparison.OrdinalIgnoreCase));
        if (workerSnapshotArgument >= 0)
        {
            var output = workerSnapshotArgument + 1 < args.Length
                ? args[workerSnapshotArgument + 1]
                : Path.Combine(AppContext.BaseDirectory, "worker-preview.png");
            return SelfTests.WriteWorkerSnapshot(output);
        }
        var drumLoadSnapshotArgument = Array.FindIndex(args,
            argument => argument.Equals("--drum-load-snapshot", StringComparison.OrdinalIgnoreCase));
        if (drumLoadSnapshotArgument >= 0)
        {
            var output = drumLoadSnapshotArgument + 1 < args.Length
                ? args[drumLoadSnapshotArgument + 1]
                : Path.Combine(AppContext.BaseDirectory, "drum-load-preview.png");
            return SelfTests.WriteDrumLoadingSnapshot(output);
        }
        var pipeSnapshotArgument = Array.FindIndex(args,
            argument => argument.Equals("--pipe-snapshot", StringComparison.OrdinalIgnoreCase));
        if (pipeSnapshotArgument >= 0)
        {
            var output = pipeSnapshotArgument + 1 < args.Length
                ? args[pipeSnapshotArgument + 1]
                : Path.Combine(AppContext.BaseDirectory, "pipe-preview.png");
            return SelfTests.WritePipeSnapshot(output);
        }
        var bloodSnapshotArgument = Array.FindIndex(args,
            argument => argument.Equals("--blood-snapshot", StringComparison.OrdinalIgnoreCase));
        if (bloodSnapshotArgument >= 0)
        {
            var output = bloodSnapshotArgument + 1 < args.Length
                ? args[bloodSnapshotArgument + 1]
                : Path.Combine(AppContext.BaseDirectory, "blood-preview.png");
            return SelfTests.WriteBloodSnapshot(output);
        }
        var rerouteSnapshotArgument = Array.FindIndex(args,
            argument => argument.Equals("--dynamic-runoff-snapshot", StringComparison.OrdinalIgnoreCase));
        if (rerouteSnapshotArgument >= 0)
        {
            var output = rerouteSnapshotArgument + 1 < args.Length
                ? args[rerouteSnapshotArgument + 1]
                : Path.Combine(AppContext.BaseDirectory, "dynamic-runoff-preview.png");
            return SelfTests.WriteDynamicRunoffSnapshot(output);
        }
        if (args.Contains("--window-smoke-test", StringComparer.OrdinalIgnoreCase))
        {
            ApplicationConfiguration.Initialize();
            using var smokeWindow = new GameWindow();
            _ = smokeWindow.Handle;
            return smokeWindow.IsHandleCreated ? 0 : 1;
        }

        ApplicationConfiguration.Initialize();
        var startGate = Path.Combine(AppContext.BaseDirectory, ".codex-test-start");
        var doneGate = Path.Combine(AppContext.BaseDirectory, ".codex-test-done");
        if (File.Exists(doneGate))
        {
            File.Delete(doneGate);
            TestGateForm.ShowDone();
            return 0;
        }
        if (File.Exists(startGate))
        {
            File.Delete(startGate);
            if (!TestGateForm.ConfirmStart()) return 0;
        }
        using var game = new GameWindow();
        Application.Run(game);
        return 0;
    }
}
