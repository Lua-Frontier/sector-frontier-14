using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Kitchen.Components;

/// <summary>
/// Used to mark entity that should act as a spike.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
[Access(typeof(SharedKitchenSpikeSystem))]
public sealed partial class KitchenSpikeComponent : Component
{
    /// <summary>
    /// ID of the container where the victim will be stored.
    /// </summary>
    [DataField]
    public string ContainerId = "body";

    /// <summary>
    /// Container where the victim will be stored.
    /// </summary>
    [ViewVariables]
    public ContainerSlot BodyContainer = default!;

    /// <summary>
    /// Sound to play when the victim is hooked or unhooked.
    /// </summary>
    [DataField("sound")]
    public SoundSpecifier SpikeSound = new SoundPathSpecifier("/Audio/Effects/Fluids/splat.ogg");

    /// <summary>
    /// Sound to play when the victim is butchered.
    /// </summary>
    [DataField]
    public SoundSpecifier ButcherSound = new SoundPathSpecifier("/Audio/Weapons/bladeslice.ogg");

    /// <summary>
    /// Damage that will be applied to the victim when they are hooked or unhooked.
    /// </summary>
    [DataField]
    public DamageSpecifier SpikeDamage = new()
    {
        DamageDict = new()
        {
            { "Piercing", 10 },
        },
    };

    /// <summary>
    /// Damage that will be applied to the victim when they are butchered.
    /// </summary>
    [DataField]
    public DamageSpecifier ButcherDamage = new()
    {
        DamageDict = new()
        {
            { "Slash", 20 },
        },
    };

    /// <summary>
    /// Damage that the living victim will receive over time.
    /// </summary>
    [DataField]
    public DamageSpecifier TimeDamage = new()
    {
        DamageDict = new()
        {
            { "Blunt", 1 },
        },
    };

    /// <summary>
    /// The next time when the damage will be applied to the victim.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextDamage;

    /// <summary>
    /// How often the damage should be applied to the victim.
    /// </summary>
    [DataField]
    public TimeSpan DamageInterval = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Time that it will take to put the victim on the spike.
    /// </summary>
    [DataField("delay")]
    public TimeSpan HookDelay = TimeSpan.FromSeconds(7);

    /// <summary>
    /// Time that it will take to put the victim off the spike.
    /// </summary>
    [DataField]
    public TimeSpan UnhookDelay = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Time that is added to the butcher delay while the victim is alive.
    /// </summary>
    [DataField]
    public TimeSpan ButcherDelayAlive = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Value by which the butchering delay will be multiplied if the victim is dead.
    /// </summary>
    [DataField]
    public float ButcherModifierDead = 0.5f;

    /// <summary>
    /// Whether only dead victims can be put on the spike.
    /// </summary>
    [DataField]
    public bool RequireDead = true;

    /// <summary>
    /// Whether a humanoid is gibbed once the last piece of meat is cut off.
    /// If false, the body stays on the spike and can be unhooked.
    /// </summary>
    [DataField]
    public bool GibHumanoids;
}
