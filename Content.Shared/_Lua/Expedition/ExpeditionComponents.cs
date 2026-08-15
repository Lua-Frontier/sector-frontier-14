// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.Audio;
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Lua.Expedition;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ExpeditionDataComponent : Component
{
    [ViewVariables]
    public bool Claimed => ActiveMission != 0;
    [ViewVariables(VVAccess.ReadWrite), DataField("cooldown")]
    public bool Cooldown = false;

    [ViewVariables(VVAccess.ReadWrite), DataField("nextOffer", customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextOffer;

    [ViewVariables]
    public readonly Dictionary<ushort, ExpeditionMissionParams> Missions = new();

    [ViewVariables]
    public ushort ActiveMission;

    public ushort NextIndex = 1;

    [ViewVariables(VVAccess.ReadWrite), DataField]
    public bool CanFinish = false;

    [ViewVariables(VVAccess.ReadWrite), DataField]
    public TimeSpan CooldownTime;

    [ViewVariables(VVAccess.ReadWrite), DataField]
    public EntityUid? ReturnMapUid;

    [ViewVariables(VVAccess.ReadWrite), DataField]
    public Vector2 ReturnWorldPosition;

    [ViewVariables]
    public bool Generating;
    [ViewVariables]
    public float GenerationProgress;

    [ViewVariables]
    public bool HasLandingCoords;

    [ViewVariables]
    public int LandingCoordsX;

    [ViewVariables]
    public int LandingCoordsY;

    [ViewVariables]
    public string LandingCoordCode = string.Empty;

    [ViewVariables]
    public EntityUid? InitiatingActor;
}

[RegisterComponent]
public sealed partial class ExpeditionShuttleComponent : Component;

[RegisterComponent]
public sealed partial class ExpeditionCrewMemberComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public EntityUid ExpeditionMap;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class ExpeditionPlanetComponent : Component
{
    [DataField]
    public Vector2i LandingOrigin;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class ExpeditionMapComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("stage")]
    public ExpeditionStage Stage = ExpeditionStage.Added;
    [DataField("station")]
    public EntityUid Station;

    [ViewVariables(VVAccess.ReadWrite), DataField("endTime", customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan EndTime;

    [ViewVariables]
    public bool Completed;

    [ViewVariables]
    public bool DepartureStarted;

    [ViewVariables(VVAccess.ReadWrite), DataField]
    public int Seed;

    [ViewVariables(VVAccess.ReadWrite), DataField]
    public SoundSpecifier Sound = new SoundCollectionSpecifier("ExpeditionEnd")
    {
        Params = AudioParams.Default.WithVolume(-5),
    };
    [ViewVariables]
    public ResolvedSoundSpecifier SelectedSong;
}

[Serializable, NetSerializable]
public sealed class ExpeditionMapComponentState : ComponentState
{
    public ExpeditionStage Stage;
    public TimeSpan EndTime;
}
