using Content.Shared.Storage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Lua.Shared.Animals;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class EggAriralLayerComponent : Component
{
    [DataField(required: true)]
    public List<EntitySpawnEntry> EggSpawn = new();

    [DataField]
    public EntProtoId EggLayAction = "ActionAriralLayEgg";

    [DataField]
    public SoundSpecifier EggLaySound = new SoundPathSpecifier("/Audio/Effects/pop.ogg");

    [DataField] public EntityUid? Action;
}
