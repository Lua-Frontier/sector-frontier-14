using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.ErpPanel;

[Serializable, NetSerializable]
public enum ErpPanelKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class ErpPanelBuiState(NetEntity user, NetEntity target) : BoundUserInterfaceState
{
    public NetEntity User { get; } = user;
    public NetEntity Target { get; } = target;
}

[Serializable, NetSerializable]
public sealed partial class ErpPanelSendMessage(string interaction, float customArousal, float customMoaning) : BoundUserInterfaceMessage
{
    public readonly string Interaction = interaction;
    public readonly float CustomArousal = customArousal;
    public readonly float CustomMoaning = customMoaning;
}

[ByRefEvent]
public readonly record struct ErpPanelOpenEvent(EntityUid Target);
