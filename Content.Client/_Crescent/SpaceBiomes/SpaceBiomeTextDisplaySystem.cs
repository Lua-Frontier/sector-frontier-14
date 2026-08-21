using System.Numerics;
using Content.Shared._Crescent.SpaceBiomes;
using Content.Shared._Crescent.Vessel;
using Content.Shared._Lua.Company;
using Content.Client.Audio;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Shared.Prototypes;

namespace Content.Client._Crescent.SpaceBiomes;

public sealed class SpaceTextDisplaySystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _protMan = default!;
    [Dependency] private readonly IOverlayManager _overMan = default!;
    [Dependency] private readonly ContentAudioSystem _audioSys = default!;
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IInputManager _input = default!;

    private SpaceBiomeTextOverlay _overlay = default!;
    private bool _dismissKeyHeld;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpaceBiomeSwapMessage>(OnSwap);
        SubscribeLocalEvent<PlayerParentChangedMessage>(OnNewVesselEntered);
        SubscribeNetworkEvent<CompanyBriefingOverlayMessage>(OnCompanyBriefing);
        _overlay = new();
        _overMan.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overMan.RemoveOverlay(_overlay);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_overlay.IsMotdActive)
        {
            _dismissKeyHeld = false;
            return;
        }

        var down = _input.IsKeyDown(Keyboard.Key.Space)
                   || _input.IsKeyDown(Keyboard.Key.Escape);

        if (!down)
        {
            _dismissKeyHeld = false;
            return;
        }

        if (_dismissKeyHeld)
            return;

        _dismissKeyHeld = true;
        _overlay.HandleMotdDismissInput();
    }

    private void OnSwap(ref SpaceBiomeSwapMessage ev)
    {
        _audioSys.DisableAmbientMusic();
        SpaceBiomePrototype biome = _protMan.Index<SpaceBiomePrototype>(ev.Id);
        _overlay.Reset();
        _overlay.ResetDescription();
        _overlay.Text = biome.Name;
        _overlay.TextDescription = biome.Description;
        _overlay.CharInterval = string.IsNullOrEmpty(biome.Name)
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(2f / biome.Name.Length);
        if (_overlay.TextDescription == "")
            _overlay.CharIntervalDescription = TimeSpan.Zero;
        else
            _overlay.CharIntervalDescription = TimeSpan.FromSeconds(2f / biome.Description.Length);
    }

    private void OnNewVesselEntered(ref PlayerParentChangedMessage ev)
    {
        if (ev.Grid == null)
            return;

        var name = MetaData(ev.Grid.Value).EntityName;
        var description = "";
        if (TryComp<VesselInfoComponent>(ev.Grid.Value, out var vesselinfo))
            description = vesselinfo.Description;

        _overlay.Reset();
        _overlay.ResetDescription();

        if (_overlay.Text != null)
            return;

        _overlay.Text = name;
        _overlay.TextDescription = description;

        if (string.IsNullOrEmpty(_overlay.Text))
            _overlay.CharInterval = TimeSpan.Zero;
        else
            _overlay.CharInterval = TimeSpan.FromSeconds(2f / _overlay.Text.Length);

        if (_overlay.TextDescription == "")
            _overlay.CharIntervalDescription = TimeSpan.Zero;
        else
            _overlay.CharIntervalDescription = TimeSpan.FromSeconds(2f / _overlay.TextDescription.Length);
    }

    private void OnCompanyBriefing(CompanyBriefingOverlayMessage ev)
    {
        var size = _clyde.ScreenSize;
        if (size.X <= 0 || size.Y <= 0)
            size = new Vector2i(1280, 720);

        _overlay.ShowMotd(ev.Text, new Vector2(size.X, size.Y));
    }
}
