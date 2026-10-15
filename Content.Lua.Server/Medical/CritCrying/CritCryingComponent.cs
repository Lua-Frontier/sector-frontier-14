using Robust.Shared.Audio;

namespace Content.Lua.Server.Medical.CritCrying;

[RegisterComponent]
public sealed partial class CritCryingComponent : Component
{
    [DataField]
    public SoundSpecifier MaleSound = new SoundCollectionSpecifier("CritCryingMale", AudioParams.Default.WithVolume(-10f));

    [DataField]
    public SoundSpecifier FemaleSound = new SoundCollectionSpecifier("CritCryingFemale", AudioParams.Default.WithVolume(-10f));

    [DataField]
    public float Chance = 0.4f;

    [DataField]
    public TimeSpan MinInterval = TimeSpan.FromSeconds(13);

    [DataField]
    public TimeSpan MaxInterval = TimeSpan.FromSeconds(32);

    [ViewVariables]
    public TimeSpan? NextCry;

    [ViewVariables]
    public EntityUid? Stream;
}
