using System.Collections.Generic;
using System.Linq;
using Allegiance.ClientCore.MonoGame.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace Allegiance.ClientCore.MonoGame.Tests;

/// <summary>The VR flight reference image is drawn at twice its layout's pixels;
/// its text then takes the 2x bake at half the scale, so it keeps its size and
/// gains glyph pixels (ragesaq, 2026-11-26: the ring's text was low resolution).</summary>
public sealed class VrDenseTextTests
{
    private static SpriteFont Font(int size)
    {
        var chars = Enumerable.Range(32, 95).Select(c => (char)c).ToList();
        return new SpriteFont(null!, chars.Select(_ => new Rectangle(0, 0, size, 2 * size)).ToList(),
            chars.Select(_ => new Rectangle(0, 0, size, 2 * size)).ToList(), chars,
            2 * size, 0f, chars.Select(_ => new Vector3(0, size, 0)).ToList(), null);
    }

    private static readonly SpriteFont One = Font(8), Two = Font(16), Other = Font(8);
    private static readonly NativeFonts Fonts = new(new Dictionary<NativeFont, (SpriteFont, SpriteFont)>
    {
        [NativeFont.MedVerdana] = (One, Two),
    });

    [Fact]
    public void A_one_x_bake_has_its_two_x_bake_and_nothing_else_has_one()
    {
        Assert.True(Fonts.TryDenser(One, out var dense));
        Assert.Same(Two, dense);
        Assert.False(Fonts.TryDenser(Two, out _));
        Assert.False(Fonts.TryDenser(Other, out _));
    }

    [Fact]
    public void At_density_two_text_takes_the_two_x_bake_at_half_the_scale()
    {
        var (font, scale) = HudRenderer.DenseText(Fonts, 2f, One, 1.2f);
        Assert.Same(Two, font);
        Assert.Equal(0.6f, scale, 5);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void Below_density_two_text_is_drawn_as_given(float density)
    {
        var (font, scale) = HudRenderer.DenseText(Fonts, density, One, 1.2f);
        Assert.Same(One, font);
        Assert.Equal(1.2f, scale, 5);
    }

    [Fact]
    public void Without_native_fonts_or_a_bake_text_is_drawn_as_given()
    {
        Assert.Same(One, HudRenderer.DenseText(null, 2f, One, 1f).Font);
        var (font, scale) = HudRenderer.DenseText(Fonts, 2f, Other, 1f);
        Assert.Same(Other, font);
        Assert.Equal(1f, scale, 5);
    }
}
