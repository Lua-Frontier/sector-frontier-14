using Robust.Shared.Prototypes;
using Content.Shared.NPC.Prototypes;

namespace Content.Lua.Shared.Turrets;

public enum OwnerDefenseTurretMode : byte
{
    Faction = 0,
    Owner = 1,
}

[RegisterComponent]
public sealed partial class OwnerDefenseTurretComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? Owner;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public OwnerDefenseTurretMode Mode = OwnerDefenseTurretMode.Faction;

    [DataField]
    public HashSet<ProtoId<NpcFactionPrototype>> OwnerFactions = new();

    [DataField]
    public ProtoId<NpcFactionPrototype> OwnerOnlyFaction = "HostileUniversally";
}
