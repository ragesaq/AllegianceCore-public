using Silk.NET.OpenXR;

namespace Allegiance.ClientCore.Vr;

public enum VrSessionAction
{
    None,
    Begin,
    End,
    TearDown,
}

/// <summary>What the presenter does when the runtime reports a new session
/// state (OpenXR 1.0 section 9.3, the session lifecycle).</summary>
public static class VrSessionLifecycle
{
    public static VrSessionAction OnStateChanged(SessionState state) => state switch
    {
        SessionState.Ready => VrSessionAction.Begin,
        SessionState.Stopping => VrSessionAction.End,
        SessionState.Exiting or SessionState.LossPending => VrSessionAction.TearDown,
        _ => VrSessionAction.None,
    };
}
