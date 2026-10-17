namespace Content.Shared._Arcane.ErpPanel;

[RegisterComponent]
public sealed partial class ErpPanelOwnerComponent : Component
{
    public static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(2);

    public const float MaxMultiplier = 200f; // Lua

    public EntityUid? Target;

    public TimeSpan LastInteraction;
}
