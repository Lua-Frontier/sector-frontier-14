// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;
using Content.Shared._Lua.Disease.Components;

namespace Content.Shared._Lua.Disease.UI;

[NetSerializable, Serializable]
public sealed class DiseaseAnalyzerWindowInterfaceState(
    DiseaseAnalyzerStatus status,
    float progress,
    List<string> diseaseNames,
    string code,
    bool fragile,
    bool filled
    ) : BoundUserInterfaceState
{
    public DiseaseAnalyzerStatus Status = status;
    public float Progress = progress;
    public List<string> DiseaseNames = diseaseNames;
    public string Code = code;
    public bool Fragile = fragile;
    public bool Filled = filled;
}
