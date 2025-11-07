using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._Lua.Horror;

[RegisterComponent]
public sealed partial class AutoBooComponent : Component
{
    [DataField("booInterval")]
    public TimeSpan BooInterval = TimeSpan.FromSeconds(10);

    [ViewVariables]
    public TimeSpan NextBoo = TimeSpan.Zero;

    [DataField("soundInterval")]
    public TimeSpan SoundInterval = TimeSpan.FromSeconds(48);

    [ViewVariables]
    public TimeSpan NextSound = TimeSpan.Zero;

    [DataField("booSound")]
    public SoundSpecifier BooSound = new SoundPathSpecifier("/Audio/Ambience/anomaly_scary.ogg");

    [DataField("booRadius")]
    public float BooRadius = 8f;

    [DataField("booMaxTargets")]
    public int BooMaxTargets = 3;

    [DataField("spawnAnomalyOnInit")]
    public EntProtoId? SpawnAnomalyOnInit;

    [ViewVariables]
    public bool AnomalySpawned = false;
}

