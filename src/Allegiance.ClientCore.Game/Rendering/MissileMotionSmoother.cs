using System;
using System.Collections.Generic;
using SimVec = Allegiance.Core.Collision.Vector3;

namespace Allegiance.ClientCore.MonoGame.Rendering;

/// <summary>Where a missile is drawn between the server's position updates
/// (MISSILE_UPDATE, about 20 a second). Drawn at the last position only, a
/// missile stood still for several frames and then jumped, which ragesaq saw
/// in the headset as a low frame rate or as a double image (2026-11-26).
///
/// <para>Each update that moves a missile measures its velocity over the time
/// since the one before. Until the next update it is drawn moved on along that
/// velocity for the time since the last one, at most <see cref="MaxLeadSeconds"/>,
/// so a lost update does not carry it far off. A missile's first update, and
/// one after a long gap, draw it where the server put it.</para>
///
/// <para>A frame calls <see cref="BeginFrame"/> once, then <see cref="Observe"/>
/// for each missile, then <see cref="EndFrame"/>, which forgets missiles not
/// seen. A second view of the same frame (the right eye) calls
/// <see cref="Shown"/>, so both eyes see each missile at one position.</para></summary>
public sealed class MissileMotionSmoother
{
    /// <summary>The longest a missile is moved on past its last update.</summary>
    public const double MaxLeadSeconds = 0.1;

    /// <summary>Two updates further apart than this give no velocity.</summary>
    public const double MaxSampleGapSeconds = 0.5;

    private struct Track
    {
        public SimVec Last;
        public double LastChange;
        public SimVec Velocity;
        public bool HasVelocity;
        public SimVec Shown;
        public long Frame;
    }

    private readonly Dictionary<short, Track> _tracks = new();
    private readonly List<short> _stale = new();
    private long _frame;
    private double _now;

    public void BeginFrame(double nowSeconds)
    {
        _frame++;
        _now = nowSeconds;
    }

    /// <summary>Takes a missile's latest server position and returns where to
    /// draw it this frame.</summary>
    public SimVec Observe(short id, SimVec position)
    {
        if (!_tracks.TryGetValue(id, out var t))
            t = new Track { Last = position, LastChange = _now };
        else if (position != t.Last)
        {
            double dt = _now - t.LastChange;
            bool hadVelocity = t.HasVelocity;
            t.HasVelocity = dt > 0 && dt <= MaxSampleGapSeconds;
            if (t.HasVelocity)
            {
                // An update is seen at the first frame after it arrives, so a
                // single interval is off by up to a frame: average it with the
                // last one while the missile keeps its velocity.
                var measured = (position - t.Last) * (float)(1.0 / dt);
                t.Velocity = hadVelocity ? (t.Velocity + measured) * 0.5f : measured;
            }
            t.Last = position;
            t.LastChange = _now;
        }
        double lead = Math.Min(_now - t.LastChange, MaxLeadSeconds);
        t.Shown = t.HasVelocity && lead > 0 ? t.Last + t.Velocity * (float)lead : t.Last;
        t.Frame = _frame;
        _tracks[id] = t;
        return t.Shown;
    }

    /// <summary>Where <see cref="Observe"/> put the missile this frame, or
    /// <paramref name="fallback"/> for one it has not seen this frame.</summary>
    public SimVec Shown(short id, SimVec fallback) =>
        _tracks.TryGetValue(id, out var t) && t.Frame == _frame ? t.Shown : fallback;

    /// <summary>Forgets every missile not seen this frame.</summary>
    public void EndFrame()
    {
        _stale.Clear();
        foreach (var (id, t) in _tracks)
            if (t.Frame != _frame) _stale.Add(id);
        foreach (var id in _stale) _tracks.Remove(id);
    }

    public int Count => _tracks.Count;
}
