using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PixelForgeStudio.Core;

public sealed class BlobForgeBridge(ProjectStore store)
{
    public async Task<BlobForgePreviewResult> Export(PixelProject project, bool launch = false)
    {
        var studio = Directory.GetParent(store.Root) ?? throw new DirectoryNotFoundException("PixelForgeStudio root was not found.");
        var workspace = studio.Parent ?? throw new DirectoryNotFoundException("PROCESS workspace was not found.");
        var blobForge = Path.Combine(workspace.FullName, "BlobForge");
        var sourceAssets = Path.Combine(blobForge, "Assets");
        if (!Directory.Exists(sourceAssets)) throw new DirectoryNotFoundException("BlobForge/Assets was not found beside PixelForgeStudio.");

        var assetName = ResolveAssetName(project);
        var data = project.FrameCount > 1 ? PngCodec.RenderSheet(project) : PngCodec.RenderFrame(project, 0);
        var sourcePath = Path.Combine(sourceAssets, assetName);
        await File.WriteAllBytesAsync(sourcePath, data);

        string? buildPath = null;
        var buildAssets = Path.Combine(blobForge, "bin", "CurrentBuild", "Assets");
        if (Directory.Exists(buildAssets))
        {
            buildPath = Path.Combine(buildAssets, assetName);
            await File.WriteAllBytesAsync(buildPath, data);
        }

        var projectFile = Path.Combine(blobForge, "BlobForge.csproj");
        var registered = File.Exists(projectFile) && (await File.ReadAllTextAsync(projectFile)).Contains($"Assets\\{assetName}", StringComparison.OrdinalIgnoreCase);
        var executable = Path.Combine(blobForge, "bin", "CurrentBuild", "BlobForge.exe");
        var launched = false;
        if (launch)
        {
            if (!File.Exists(executable)) throw new FileNotFoundException("Publish BlobForge/bin/CurrentBuild before launching an art preview.", executable);
            Process.Start(new ProcessStartInfo(executable) { WorkingDirectory = Path.GetDirectoryName(executable)!, UseShellExecute = true });
            launched = true;
        }

        return new BlobForgePreviewResult(assetName, sourcePath, buildPath, registered, launched,
            registered ? null : $"Add Assets\\{assetName} to BlobForge.csproj before relying on published builds.");
    }

    public static string ResolveAssetName(PixelProject project)
    {
        if (!string.IsNullOrWhiteSpace(project.RuntimeAssetName))
        {
            var explicitName = Path.GetFileName(project.RuntimeAssetName.Trim());
            return explicitName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? explicitName : explicitName + ".png";
        }

        var name = Regex.Replace(project.Name, "^blobforge_", "", RegexOptions.IgnoreCase);
        name = Regex.Replace(name, "_v[0-9]+$", "", RegexOptions.IgnoreCase);
        var pascal = string.Concat(name.Split(['_', '-'], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
        return (string.IsNullOrWhiteSpace(pascal) ? "PixelForgeAsset" : pascal) + ".png";
    }
}

public sealed record BlobForgePreviewResult(string AssetName, string SourcePath, string? CurrentBuildPath,
    bool RegisteredInProject, bool Launched, string? Warning);
