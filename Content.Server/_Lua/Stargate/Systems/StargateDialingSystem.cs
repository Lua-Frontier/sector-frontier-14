// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server._Lua.Stargate.Components;
using Content.Shared._Lua.Stargate;
using Content.Shared._Lua.Stargate.Components;
using Robust.Shared.Audio;
using System.Collections.Generic;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Lua.Stargate.Systems;

public sealed class StargateDialingSystem : EntitySystem
{
    private static readonly AudioParams GateSoundParams = AudioParams.Default.WithVolume(
        SharedAudioSystem.GainToVolume(0.25f));
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly StargateSystem _stargate = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var dialingToFinish = new List<(EntityUid Uid, StargateDialingComponent Dialing, StargateComponent Gate)>();
        var dialQuery = AllEntityQuery<StargateDialingComponent, StargateComponent>();
        while (dialQuery.MoveNext(out var uid, out var dialing, out var gate))
        {
            dialing.Accumulator += frameTime;

            if (!dialing.InKawoosh)
            {
                if (dialing.Accumulator >= dialing.ChevronDelay)
                {
                    dialing.Accumulator -= dialing.ChevronDelay;

                    _audio.PlayPvs(gate.ChevronSound, uid, GateSoundParams);
                    dialing.ChevronIndex++;

                    if (dialing.ChevronIndex >= dialing.Symbols.Length)
                    {
                        dialing.InKawoosh = true;
                        dialing.Accumulator = 0f;
                        _audio.PlayPvs(gate.OpenSound, uid, GateSoundParams);
                        _stargate.UpdateGateVisualState(uid, StargateVisualState.Opening);
                    }
                }
            }
            else
            {
                if (dialing.Accumulator >= dialing.KawooshDelay)
                    dialingToFinish.Add((uid, dialing, gate));
            }
        }
        foreach (var (uid, dialing, gate) in dialingToFinish)
        {
            _stargate.FinishDialing(uid, dialing, gate);
        }

        var closingToFinish = new List<EntityUid>();
        var closeQuery = AllEntityQuery<StargateClosingComponent>();
        while (closeQuery.MoveNext(out var uid, out var closing))
        {
            closing.Accumulator += frameTime;
            if (closing.Accumulator >= closing.Duration)
                closingToFinish.Add(uid);
        }
        foreach (var uid in closingToFinish)
        {
            _stargate.UpdateGateVisualState(uid, StargateVisualState.Off);
            RemComp<StargateClosingComponent>(uid);
        }

        var openingToFinish = new List<EntityUid>();
        var openQuery = AllEntityQuery<StargateOpeningComponent>();
        while (openQuery.MoveNext(out var uid, out var opening))
        {
            opening.Accumulator += frameTime;
            if (opening.Accumulator >= opening.Duration)
                openingToFinish.Add(uid);
        }
        foreach (var uid in openingToFinish)
        {
            _stargate.UpdateGateVisualState(uid, StargateVisualState.Idle);
            RemComp<StargateOpeningComponent>(uid);
        }

        var irisToProcess = new List<(EntityUid Uid, float Accumulator, float Duration, bool IsOpening)>();
        var irisQuery = AllEntityQuery<StargateIrisAnimatingComponent>();
        while (irisQuery.MoveNext(out var uid, out var iris))
        {
            iris.Accumulator += frameTime;
            if (iris.Accumulator >= iris.Duration)
                irisToProcess.Add((uid, iris.Accumulator, iris.Duration, iris.IsOpening));
        }
        foreach (var (uid, _, _, isOpening) in irisToProcess)
        {
            _stargate.FinishIrisAnimation(uid, isOpening);
        }
    }
}
