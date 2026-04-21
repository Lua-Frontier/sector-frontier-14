// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.SponsorPlayer;
using Robust.Client.UserInterface;

namespace Content.Client._Lua.SponsorPlayer;

public sealed class SponsorPlayerBoundUserInterface : BoundUserInterface
{
    private SponsorPlayerMenu? _menu;

    public SponsorPlayerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<SponsorPlayerMenu>();
        _menu.OnPlayPressed += playRequested =>
        {
            if (!playRequested)
            {
                SendMessage(new SponsorPlayerStopMessage());
                return;
            }
            var trackId = _menu.GetPlayableTrackId();
            if (trackId == null) return;
            var title = _menu.GetPlayableTrackTitle();
            var hash = _menu.GetPlayableTrackHash();
            SendMessage(new SponsorPlayerPlayTrackMessage(trackId, title, hash));
            _menu.SetNowPlaying(title);
        };
        _menu.OnPreviousPressed += () => SendMessage(new SponsorPlayerPreviousMessage());
        _menu.OnNextPressed += () => SendMessage(new SponsorPlayerNextMessage());
        _menu.OnRepeatToggled += enabled => SendMessage(new SponsorPlayerRepeatMessage(enabled));
        _menu.OnStopPressed += () =>
        {
            SendMessage(new SponsorPlayerStopMessage());
        };
        _menu.OnVolumeChanged += volume =>
        {
            var system = IoCManager.Resolve<IEntityManager>().System<SponsorPlayerSystem>();
            system.Volume = volume;
        };
        var clientSystem = IoCManager.Resolve<IEntityManager>().System<SponsorPlayerSystem>();
        _menu.SetVolumeSlider(clientSystem.Volume);
        Reload();
    }

    public void Reload()
    {
        if (_menu == null || !EntMan.TryGetComponent(Owner, out SponsorPlayerComponent? comp)) return;
        _menu.SetCurrentTrack(comp.CurrentTrackId, comp.CurrentTrackHash, comp.CurrentTrackTitle);
        _menu.SetRepeatEnabled(comp.PlaybackMode == SponsorPlayerPlaybackMode.Repeat);
        _menu.SetIsPlaying(comp.IsPlaying);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not SponsorPlayerBoundUserInterfaceState cast) return;
        if (cast.Error != null) _menu?.ShowError(cast.Error);
        else _menu?.PopulateTracks(cast.Tracks);
        Reload();
    }
}
