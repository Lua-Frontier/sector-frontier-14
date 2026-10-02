using Content.Shared._Lua.ERP;
using Robust.Shared.GameStates;

namespace Content.Shared._Arcane.ERP;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ErpStatusComponent : Component
{
    [DataField, AutoNetworkedField]
    public EnumERPStatus Status = EnumERPStatus.NO;
}
