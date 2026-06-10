// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using Content.Shared.Backmen.Disease;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared._Lua.Disease.Components;

[RegisterComponent]
public sealed partial class DiseaseContainerComponent : Component
{
    [DataField(tag: "diseases", customTypeSerializer: typeof(PrototypeIdArraySerializer<DiseasePrototype>))]
    public string[]? DiseaseIDs;

    [DataField(tag: "fragile")]
    public bool IsFragile;
}
