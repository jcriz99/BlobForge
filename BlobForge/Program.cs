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
        var snapshotArgument = Array.FindIndex(args,
            argument => argument.Equals("--station-snapshot", StringComparison.OrdinalIgnoreCase));
        if (snapshotArgument >= 0)
        {
            var output = snapshotArgument + 1 < args.Length
                ? args[snapshotArgument + 1]
                : Path.Combine(AppContext.BaseDirectory, "station-preview.png");
            return SelfTests.WriteStationSnapshot(output);
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
