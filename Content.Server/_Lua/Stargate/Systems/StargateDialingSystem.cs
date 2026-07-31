// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server._Lua.Stargate.Components;
using Content.Shared._Lua.Stargate;
using Content.Shared._Lua.Stargate.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Lua.Stargate.Systems;

public sealed class StargateDialingSystem : EntitySystem
{
    private static readonly AudioParams GateSoundParams = AudioParams.Default.WithVolume(
        SharedAudioSystem.GainToVolume(0.25f));
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly StargateSystem _stargate = default!;

    private readonly List<(EntityUid Uid, StargateDialingComponent Dialing, StargateComponent Gate)> _dialingToFinish = new();
    private readonly List<EntityUid> _closingToFinish = new();
    private readonly List<EntityUid> _openingToFinish = new();
    private readonly List<(EntityUid Uid, bool IsOpening)> _irisToProcess = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _dialingToFinish.Clear();
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
                    _dialingToFinish.Add((uid, dialing, gate));
            }
        }
        foreach (var (uid, dialing, gate) in _dialingToFinish)
            _stargate.FinishDialing(uid, dialing, gate);

        _closingToFinish.Clear();
        var closeQuery = AllEntityQuery<StargateClosingComponent>();
        while (closeQuery.MoveNext(out var uid, out var closing))
        {
            closing.Accumulator += frameTime;
            if (closing.Accumulator >= closing.Duration)
                _closingToFinish.Add(uid);
        }
        foreach (var uid in _closingToFinish)
        {
            _stargate.UpdateGateVisualState(uid, StargateVisualState.Off);
            RemComp<StargateClosingComponent>(uid);
        }

        _openingToFinish.Clear();
        var openQuery = AllEntityQuery<StargateOpeningComponent>();
        while (openQuery.MoveNext(out var uid, out var opening))
        {
            opening.Accumulator += frameTime;
            if (opening.Accumulator >= opening.Duration)
                _openingToFinish.Add(uid);
        }
        foreach (var uid in _openingToFinish)
        {
            _stargate.UpdateGateVisualState(uid, StargateVisualState.Idle);
            RemComp<StargateOpeningComponent>(uid);
        }

        _irisToProcess.Clear();
        var irisQuery = AllEntityQuery<StargateIrisAnimatingComponent>();
        while (irisQuery.MoveNext(out var uid, out var iris))
        {
            iris.Accumulator += frameTime;
            if (iris.Accumulator >= iris.Duration)
                _irisToProcess.Add((uid, iris.IsOpening));
        }
        foreach (var (uid, isOpening) in _irisToProcess)
            _stargate.FinishIrisAnimation(uid, isOpening);
    }
}
