namespace Allegiance.ClientCore.Vr;

/// <summary>Where the flat frame hangs in the headset: a quad facing the user,
/// <see cref="DistanceMetres"/> ahead of the local space origin, as wide as
/// <see cref="WidthMetres"/> and as tall as the back buffer's aspect gives.</summary>
public static class VirtualScreen
{
    public const float DistanceMetres = 2.0f;
    public const float WidthMetres = 2.4f;

    public static (float Width, float Height) SizeMetres(int pixelWidth, int pixelHeight)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0) throw new ArgumentOutOfRangeException(nameof(pixelWidth), "The back buffer has no area.");
        return (WidthMetres, WidthMetres * pixelHeight / pixelWidth);
    }
}
