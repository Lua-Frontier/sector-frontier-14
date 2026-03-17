using Robust.Shared.Prototypes;

namespace Content.Shared._Lua.EmergencyRockCutter;

[RegisterComponent]
public sealed partial class EmergencyRockCutterComponent : Component
{
    [DataField]
    public float Delay = 15f;

    [DataField("effect")]
    public EntProtoId? Effect = "EffectRCDDeconstruct4";
}
