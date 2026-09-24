using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Ships;

[RegisterComponent]
[NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class BluespaceFuelComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool HasFuel;

    [DataField, AutoNetworkedField]
    public int Count;

    [DataField, AutoNetworkedField]
    public int MaxCount;

    [DataField, AutoNetworkedField]
    public float RangeBonus = 3000f;
}


