using Allegiance.ClientCore.MonoGame.Rendering;
using Allegiance.Core.Rendering;
using Microsoft.Xna.Framework;

namespace Allegiance.ClientCore.MonoGame.Tests;

/// <summary>The VR client's second eye redraws the scene without advancing it.
/// These cover the scene helpers that keep state between calls.</summary>
public sealed class VrScenePassTests
{
    private static readonly Matrix Projection = Matrix.CreatePerspectiveFieldOfView(1, 1.5f, 1, 10000);

    private static (VisualOptions Options, DebrisGeo Debris, SubtleDust Dust, List<AmbientDustView> Output) DebrisField()
    {
        var options = new VisualOptions();
        options.SetToggle(VisualFlags.SubtleDustKey, false);   // the native debris field, which keeps state
        var debris = new DebrisGeo(1, new RandomTrailSource(1));
        var output = new List<AmbientDustView>();
        NativeScene.GatherDust(options, new SubtleDust(), debris, default, Vector3.Zero, Matrix.Identity, Projection, 0, output);
        return (options, debris, new SubtleDust(), output);
    }

    [Fact]
    public void A_repeat_view_of_the_debris_field_does_not_step_it()
    {
        var (options, debris, dust, output) = DebrisField();
        var before = debris.Points.ToArray();

        // The second eye, 50 m on and a second later in the field's own terms,
        // as far as a step is concerned; it must only re-project.
        NativeScene.GatherDust(options, dust, debris, default, new Vector3(0, 0, -50), Matrix.Identity, Projection, 1, output,
            advance: false);

        Assert.Equal(before, debris.Points);
    }

    [Fact]
    public void Advancing_the_debris_field_does_step_it()
    {
        var (options, debris, dust, output) = DebrisField();
        var before = debris.Points.ToArray();

        NativeScene.GatherDust(options, dust, debris, default, new Vector3(0, 0, -50), Matrix.Identity, Projection, 1, output);

        Assert.NotEqual(before, debris.Points);
    }
}
