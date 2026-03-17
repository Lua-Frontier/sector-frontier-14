using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Lua.EmergencyRockCutter;

[Serializable, NetSerializable]
public sealed partial class EmergencyRockCutterDoAfterEvent : DoAfterEvent
{
    [DataField("effect")]
    public NetEntity? Effect { get; private set; }

    private EmergencyRockCutterDoAfterEvent() { }

    public EmergencyRockCutterDoAfterEvent(NetEntity? effect = null)
    {
        Effect = effect;
    }

    public override DoAfterEvent Clone() => this;
}
