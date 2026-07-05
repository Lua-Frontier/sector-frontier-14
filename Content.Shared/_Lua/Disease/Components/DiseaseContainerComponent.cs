// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;
using Content.Shared.Backmen.Disease;

namespace Content.Shared._Lua.Disease.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class DiseaseContainerComponent : Component
{
    [DataField("diseases")]
    public ProtoId<DiseasePrototype>[]? DiseaseIDs = null;

    [DataField]
    public bool Fragile = false;
}
