// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Lua.JumpAbility;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedLuaJumpAbilitySystem))]
public sealed partial class LuaJumpAbilityComponent : Component
{
    [DataField]
    public EntProtoId? DirectionalAction = "ActionLuaDirectionalJump";

    [DataField, AutoNetworkedField]
    public EntityUid? DirectionalActionEntity;

    [DataField]
    public EntProtoId? PointAction = "ActionLuaJumpToPoint";

    [DataField, AutoNetworkedField]
    public EntityUid? PointActionEntity;

    [DataField, AutoNetworkedField]
    public float JumpDistance = 5f;

    [DataField, AutoNetworkedField]
    public float JumpThrowSpeed = 10f;

    [DataField, AutoNetworkedField]
    public bool CanCollide = false;

    [DataField, AutoNetworkedField]
    public TimeSpan CollideKnockdown = TimeSpan.FromSeconds(2);

    [DataField, AutoNetworkedField]
    public float Strength = 3f;

    [DataField, AutoNetworkedField]
    public float JumpDuration = 1f;

    [DataField, AutoNetworkedField]
    public float Interval = 0.01f;

    [DataField, AutoNetworkedField]
    public float SprintCostFraction = 0.65f;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? JumpSound;

    [DataField, AutoNetworkedField]
    public LocId? JumpFailedPopup = "lua-jump-ability-failure";

    [DataField, AutoNetworkedField]
    public bool IsJumping = false;
    public EntityCoordinates? JumpTarget = null;
    public TimeSpan TimeUntilEndJump = TimeSpan.Zero;
    public TimeSpan TimeUntilNextJump = TimeSpan.Zero;
}

public sealed partial class LuaDirectionalJumpEvent : InstantActionEvent;

public sealed partial class LuaJumpToPointEvent : WorldTargetActionEvent;
