// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Disease.Events;

[Serializable, NetSerializable]
public sealed class DiseaseAnalyzerAnalyzeMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DiseaseAnalyzerPrintReportMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DiseaseAnalyzerContainMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DiseaseAnalyzerClearSampleMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DiseaseAnalyzerAnalyzeTimerUpdate(float progress) : BoundUserInterfaceMessage
{
    public float Progress = progress;
}
