using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Ships.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class ShuttleCategoryLimitComponent : Component
{
    [DataField("categoryId")]
    public string CategoryId = string.Empty;

    [DataField("maxInCategory")]
    public int MaxInCategory = 1;
}
