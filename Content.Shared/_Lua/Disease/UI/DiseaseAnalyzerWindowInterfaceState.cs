// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared.Backmen.Disease;
using Content.Shared._Lua.Disease.Components;

namespace Content.Shared._Lua.Disease.UI;

[Serializable, NetSerializable]
public sealed class DiseaseAnalyzerWindowInterfaceState(
    DiseaseAnalyzerStatus status,
    ProtoId<DiseasePrototype>[]? diseaseIDs,
    int code,
    bool fragile,
    bool filled
    ) : BoundUserInterfaceState
{
    public DiseaseAnalyzerStatus Status = status;
    public ProtoId<DiseasePrototype>[]? DiseaseIDs = diseaseIDs;
    public int Code = code;
    public bool Fragile = fragile;
    public bool Filled = filled;
}
