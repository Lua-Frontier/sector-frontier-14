namespace Content.Shared._Arcane.ERP;

public sealed class ArousedEvent(float before, float after) : EntityEventArgs
{
    public readonly float Before = before;
    public readonly float After = after;
    public float GetDelta()
    {
        return After - Before;
    }
}

public sealed class ArousalPhaseChangedEvent(ArousalPhase previous, ArousalPhase current) : EntityEventArgs
{
    public ArousalPhase Previous = previous;
    public ArousalPhase Current = current;
}

[ByRefEvent]
public record struct ArousalOrgasmEvent;
