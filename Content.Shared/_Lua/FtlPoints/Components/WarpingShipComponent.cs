using Robust.Shared.Map;

namespace Content.Shared._Lua.FtlPoints.Components;

[RegisterComponent]
public sealed partial class WarpingShipComponent : Component
{
    [ViewVariables]
    public MapId? TargetMap;
}
