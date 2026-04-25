// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using System.Collections.Generic;
using System.Numerics;
using Content.Shared._Lua.JumpAbility;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;

namespace Content.Client._Lua.JumpAbility;

public sealed partial class LuaClientJumpAbilitySystem : SharedLuaJumpAbilitySystem
{
    [Dependency] private readonly AnimationPlayerSystem _animation = default!;

    private const string AnimationKey = "lua_jump";
    private readonly HashSet<EntityUid> _jumping = new();
    private readonly List<EntityUid> _cleanup = new();

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<LuaJumpAbilityComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.IsJumping)
            {
                if (_jumping.Add(uid))
                    PlayJumpAnimation(uid, comp);
            }
            else
            {
                _jumping.Remove(uid);
            }
        }

        if (_jumping.Count == 0)
            return;

        foreach (var uid in _jumping)
        {
            if (!TryComp<LuaJumpAbilityComponent>(uid, out var comp) || !comp.IsJumping)
                _cleanup.Add(uid);
        }

        foreach (var uid in _cleanup)
        {
            _jumping.Remove(uid);
        }

        _cleanup.Clear();
    }

    private void PlayJumpAnimation(EntityUid uid, LuaJumpAbilityComponent comp)
    {
        if (_animation.HasRunningAnimation(uid, AnimationKey))
            return;

        var animation = new Animation
        {
            Length = TimeSpan.FromSeconds(comp.JumpDuration),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    InterpolationMode = AnimationInterpolationMode.Cubic,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0f),
                        new AnimationTrackProperty.KeyFrame(new Vector2(0, 1), comp.JumpDuration / 2),
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, comp.JumpDuration / 2),
                    }
                }
            }
        };

        _animation.Play(uid, animation, AnimationKey);
    }
}
