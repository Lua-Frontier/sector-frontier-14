using Robust.Shared.GameObjects;

namespace Content.Client._CE.IconSmoothing;

[RegisterComponent]
public sealed partial class CEIconSmoothComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("enabled")]
    public bool Enabled = true;

    public (EntityUid?, Vector2i)? LastPosition;

    [ViewVariables(VVAccess.ReadWrite), DataField("key")]
    public string? SmoothKey { get; private set; }

    [DataField]
    public List<string> AdditionalKeys = new();

    internal int UpdateGeneration { get; set; }
}
