// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Stargate.PlanetQuest;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PlanetQuestTargetComponent : Component
{
    [DataField, AutoNetworkedField]
    public PlanetObjectiveType ObjectiveType = PlanetObjectiveType.DestroyStructures;

    [DataField]
    public EntityUid QuestMap;
}
