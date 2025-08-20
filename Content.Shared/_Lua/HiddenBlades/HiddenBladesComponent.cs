using Content.Shared._Lua.HiddenBlades.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._Lua.HiddenBlades.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedHiddenBladesSystem))]
public sealed partial class HiddenBladesComponent : Component
{
    [DataField, AutoNetworkedField]
    public string? ActivatedName = "hidden-blades-gloves-name-active";

    [DataField, AutoNetworkedField]
    public string? ActivatedDescription = "hidden-blades-gloves-desc-active";

    [DataField, AutoNetworkedField]
    public string? ActivatedPopUp = "hidden-blades-gloves-activated";

    [DataField, AutoNetworkedField]
    public string? DeactivatedPopUp = "hidden-blades-gloves-deactivated";
}
