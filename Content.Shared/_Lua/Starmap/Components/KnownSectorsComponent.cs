// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Starmap.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class KnownSectorsComponent : Component
{
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public List<string> LearnedSectorIds = new();
}
