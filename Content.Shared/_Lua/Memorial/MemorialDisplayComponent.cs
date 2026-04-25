// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Memorial;

[Serializable, NetSerializable]
public enum MemorialDisplayUiKey : byte
{
    Key,
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class MemorialDisplayEntry
{
    [DataField(required: true)]
    public string Nickname = string.Empty;

    [DataField]
    public string? Description;

    [DataField]
    public string? Markup;
}

[RegisterComponent]
public sealed partial class MemorialDisplayComponent : Component
{
    [DataField(required: true)]
    public string DisplayName = "Мемориальная экспозиция";

    [DataField]
    public string DisplayDescription = string.Empty;

    [DataField]
    public List<MemorialDisplayEntry> Entries = new();
}

[Serializable, NetSerializable]
public sealed class MemorialDisplayUiState : BoundUserInterfaceState
{
    public string DisplayName { get; }
    public string DisplayDescription { get; }
    public List<string> Entries { get; }

    public MemorialDisplayUiState(string displayName, string displayDescription, List<string> entries)
    {
        DisplayName = displayName;
        DisplayDescription = displayDescription;
        Entries = entries;
    }
}
