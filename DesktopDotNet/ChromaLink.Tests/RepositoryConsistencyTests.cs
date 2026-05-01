using Xunit;

namespace ChromaLink.Tests;

public class RepositoryConsistencyTests
{
    [Fact]
    public void CanonicalVersion_MatchesAddonMetadataFiles()
    {
        var repoRoot = FindRepoRoot();
        var version = File.ReadAllText(Path.Combine(repoRoot, "VERSION")).Trim();

        Assert.False(string.IsNullOrWhiteSpace(version));

        var configLua = File.ReadAllText(Path.Combine(repoRoot, "Core", "Config.lua"));
        var abilityExportLua = File.ReadAllText(Path.Combine(repoRoot, "RIFT", "AbilityExport.lua"));
        var toc = File.ReadAllText(Path.Combine(repoRoot, "RiftAddon.toc"));

        Assert.Contains($"addonVersion = \"{version}\"", configLua, StringComparison.Ordinal);
        Assert.Contains($"or \"{version}\"", abilityExportLua, StringComparison.Ordinal);
        Assert.Contains($"Version = \"{version}\"", toc, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var versionPath = Path.Combine(current.FullName, "VERSION");
            var tocPath = Path.Combine(current.FullName, "RiftAddon.toc");
            var corePath = Path.Combine(current.FullName, "Core");
            var riftPath = Path.Combine(current.FullName, "RIFT");

            if (File.Exists(versionPath) &&
                File.Exists(tocPath) &&
                Directory.Exists(corePath) &&
                Directory.Exists(riftPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the ChromaLink repository root.");
    }
}
