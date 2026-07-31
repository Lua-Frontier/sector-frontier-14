using Content.Client.Audio;
using Content.Shared._Lua.Expedition;
using Robust.Client.Player;
using Robust.Shared.GameStates;

namespace Content.Client._Lua.Expedition;

public sealed class ExpeditionSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly ContentAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayAmbientMusicEvent>(OnPlayAmbientMusic);
        SubscribeLocalEvent<ExpeditionMapComponent, ComponentHandleState>(OnExpeditionHandleState);
    }

    private void OnExpeditionHandleState(EntityUid uid, ExpeditionMapComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not ExpeditionMapComponentState state) return;
        component.Stage = state.Stage;
        component.EndTime = state.EndTime;
        if (component.Stage >= ExpeditionStage.MusicCountdown) _audio.DisableAmbientMusic();
    }

    private void OnPlayAmbientMusic(ref PlayAmbientMusicEvent ev)
    {
        if (ev.Cancelled) return;
        var player = _playerManager.LocalEntity;
        if (!TryComp(player, out TransformComponent? xform) || !TryComp<ExpeditionMapComponent>(xform.MapUid, out var expedition) || expedition.Stage < ExpeditionStage.MusicCountdown)
        { return; }
        ev.Cancelled = true;
    }
}
