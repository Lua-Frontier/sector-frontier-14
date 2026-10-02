namespace Content.Shared._Arcane.ErpPanel;

[RegisterComponent]
public sealed partial class ErpPanelOwnerComponent : Component
{

    public EntityUid? Target;

    public Dictionary<string, TimeSpan> Cooldowns = new();
}
