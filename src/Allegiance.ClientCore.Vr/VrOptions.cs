namespace Allegiance.ClientCore.Vr;

/// <summary>How the desktop head was asked to run VR. VR is opt-in: the
/// <c>--vr</c> switch or <c>ALLEGIANCE_VR=1</c>. The OpenXR loader is found
/// through <c>ALLEGIANCE_OPENXR_LOADER</c> (a path to openxr_loader.dll, for
/// example the one an OpenXR runtime installs), else the default library search.</summary>
public sealed record VrOptions(bool Enabled, string? LoaderPath)
{
    public const string Switch = "--vr";
    public const string EnableVariable = "ALLEGIANCE_VR";
    public const string LoaderVariable = "ALLEGIANCE_OPENXR_LOADER";

    /// <summary>Reads the VR request and removes the switch from
    /// <paramref name="arguments"/>, so the head's own parser never sees it.</summary>
    public static VrOptions Take(ref string[] arguments, Func<string, string?> environment)
    {
        bool requested = arguments.Contains(Switch);
        if (requested) arguments = arguments.Where(a => a != Switch).ToArray();
        requested |= environment(EnableVariable) == "1";
        string? loader = environment(LoaderVariable);
        return new VrOptions(requested, string.IsNullOrWhiteSpace(loader) ? null : loader);
    }

    /// <summary>Library names tried in order: the configured loader first, then
    /// the platform's default loader names.</summary>
    public IReadOnlyList<string> LoaderCandidates()
    {
        var names = new List<string>();
        if (LoaderPath is not null) names.Add(LoaderPath);
        if (OperatingSystem.IsWindows()) names.Add("openxr_loader.dll");
        else names.AddRange(["libopenxr_loader.so.1", "libopenxr_loader.so"]);
        return names;
    }
}
