using System.Reflection;
using System.Text.RegularExpressions;

namespace RuinaRPG.Domain;

/// The git commit a build came from, stamped by the Dockerfiles through MSBuild's SourceRevisionId
/// (AssemblyInformationalVersion "x.y.z+<sha>"). "dev" for local builds without it.
public static partial class BuildInfo
{
    public static string ShaOf(Assembly assembly) =>
        ShaFromInformationalVersion(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    public static string ShaFromInformationalVersion(string? informationalVersion)
    {
        var plus = informationalVersion?.IndexOf('+') ?? -1;
        if (plus < 0) return "dev";
        var suffix = informationalVersion![(plus + 1)..];
        return HexSha().IsMatch(suffix) ? suffix : "dev";
    }

    [GeneratedRegex("^[0-9a-f]{7,40}$")]
    private static partial Regex HexSha();
}
