namespace Content.Server._Lua.SpaceDespawn;

[RegisterComponent]
public sealed partial class SpaceDespawnTimerComponent : Component
{
    [DataField]
    public float RemainingSeconds = 60f * 30f;
}
