// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.
using System.Globalization;
using System.Numerics;
using System.Text;
using Robust.Shared.GameStates;

namespace Content.Shared._Lua.Beds.Components;

// Ниже шок контент но я иначе ничего не смог придумать... Отрефакторите кто-то пж
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(EntitySystem))]
public sealed partial class DoubleStrapComponent : Component
{
    private List<Vector2> _slots = new()
    {
        new Vector2(0f, 0f),
        new Vector2(0f, 0f)
    };
    private List<Vector2>? _slotsCameraNorth;
    private List<Vector2>? _slotsCameraEast;
    private List<Vector2>? _slotsCameraSouth;
    private List<Vector2>? _slotsCameraWest;

    [DataField, AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public List<Vector2> Slots
    {
        get => _slots;
        set
        {
            _slots = value ?? new List<Vector2>();
            if (Owner.IsValid()) Dirty();
        }
    }

    [DataField, AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public List<Vector2>? SlotsCameraNorth
    {
        get => _slotsCameraNorth;
        set
        {
            _slotsCameraNorth = value;
            if (Owner.IsValid()) Dirty();
        }
    }

    [DataField, AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public List<Vector2>? SlotsCameraEast
    {
        get => _slotsCameraEast;
        set
        {
            _slotsCameraEast = value;
            if (Owner.IsValid()) Dirty();
        }
    }

    [DataField, AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public List<Vector2>? SlotsCameraSouth
    {
        get => _slotsCameraSouth;
        set
        {
            _slotsCameraSouth = value;
            if (Owner.IsValid()) Dirty();
        }
    }

    [DataField, AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public List<Vector2>? SlotsCameraWest
    {
        get => _slotsCameraWest;
        set
        {
            _slotsCameraWest = value;
            if (Owner.IsValid()) Dirty();
        }
    }

    [ViewVariables]
    public List<EntityUid?> Strap { get; } = new();

    [DataField, AutoNetworkedField]
    public EntityUid? StrapOverride;

    [ViewVariables(VVAccess.ReadWrite)]
    public string SlotsEditor
    {
        get => SerializeList(Slots);
        set => Slots = ParseList(value);
    }

    [ViewVariables(VVAccess.ReadWrite)]
    public string SlotsCameraNorthEditor
    {
        get => SerializeList(SlotsCameraNorth);
        set => SlotsCameraNorth = ParseList(value);
    }

    [ViewVariables(VVAccess.ReadWrite)]
    public string SlotsCameraEastEditor
    {
        get => SerializeList(SlotsCameraEast);
        set => SlotsCameraEast = ParseList(value);
    }

    [ViewVariables(VVAccess.ReadWrite)]
    public string SlotsCameraSouthEditor
    {
        get => SerializeList(SlotsCameraSouth);
        set => SlotsCameraSouth = ParseList(value);
    }

    [ViewVariables(VVAccess.ReadWrite)]
    public string SlotsCameraWestEditor
    {
        get => SerializeList(SlotsCameraWest);
        set => SlotsCameraWest = ParseList(value);
    }

    private static string SerializeList(List<Vector2>? list)
    {
        if (list == null || list.Count == 0) return string.Empty;
        var sb = new StringBuilder();
        for (var i = 0; i < list.Count; i++)
        {
            var v = list[i];
            sb.Append(v.X.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(v.Y.ToString(CultureInfo.InvariantCulture));
            if (i < list.Count - 1) sb.Append(';');
        }
        return sb.ToString();
    }

    private static List<Vector2> ParseList(string? value)
    {
        var result = new List<Vector2>();
        if (string.IsNullOrWhiteSpace(value)) return result;
        var entries = value.Split(';');
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry)) continue;
            var parts = entry.Split(',', 2);
            if (parts.Length != 2) continue;
            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)) continue;
            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)) continue;
            result.Add(new Vector2(x, y));
        }

        return result;
    }
}

