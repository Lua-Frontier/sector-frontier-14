// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Client.UserInterface.Controls;
using Content.Shared._Lua.Stargate;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Map;

namespace Content.Client._Lua.Stargate.UI;

public sealed partial class StarGateLandingBeaconWindow : FancyWindow
{
    public event Action<MapCoordinates, Angle>? OnLandingPicked;
    public event Action? OnRecallPressed;
    private readonly Label _statusLabel;
    private readonly Button _recallButton;
    private readonly StarGateLandingPickerControl _picker;

    public StarGateLandingBeaconWindow()
    {
        RobustXamlLoader.Load(this);
        _statusLabel = FindControl<Label>("StatusLabel");
        _recallButton = FindControl<Button>("RecallButton");
        _picker = FindControl<StarGateLandingPickerControl>("LandingPicker");
        _picker.OnLocationPicked += (coords, angle) => OnLandingPicked?.Invoke(coords, angle);
        _recallButton.OnPressed += _ => OnRecallPressed?.Invoke();
    }

    public void UpdateState(StarGateLandingBeaconBoundUserInterfaceState state)
    {
        if (state.ShuttleName is { } name)
        {
            _statusLabel.Text = Loc.GetString("stargate-shuttle-beacon-ui-shuttle", ("name", name));
            _picker.Visible = true;
        }
        else
        {
            _statusLabel.Text = Loc.GetString("stargate-shuttle-beacon-ui-no-card");
            _picker.Visible = false;
        }

        _recallButton.Visible = state.CanRecall || state.RecallPending;
        _recallButton.Disabled = state.RecallPending;
        _recallButton.Text = state.RecallPending
            ? Loc.GetString("stargate-shuttle-beacon-ui-recall-pending", ("time", state.RecallRemaining.ToString(@"mm\:ss")))
            : Loc.GetString("stargate-shuttle-beacon-ui-recall");
        if (state.BeaconPosition.MapId != MapId.Nullspace)
        {
            _picker.TargetMapId = state.BeaconPosition.MapId;
            _picker.Offset = state.BeaconPosition.Position;
            _picker.TargetOffset = state.BeaconPosition.Position;
            _picker.ShuttleNetEntity = state.ShuttleNetEntity;
        }
    }
}
