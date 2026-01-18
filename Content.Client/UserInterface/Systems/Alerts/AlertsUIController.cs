using Content.Client.Alerts;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Screens;
using Content.Client.UserInterface.Systems.Alerts.Widgets;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Client.UserInterface.Systems.Hotbar.Widgets;
using Content.Shared.Alert;
using Content.Shared.Lua.CLVar;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using static Robust.Client.UserInterface.Controls.LayoutContainer;

namespace Content.Client.UserInterface.Systems.Alerts;

public sealed class AlertsUIController : UIController, IOnStateEntered<GameplayState>, IOnSystemChanged<ClientAlertsSystem>
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    [UISystemDependency] private readonly ClientAlertsSystem? _alertsSystem = default;

    private AlertsUI? _alertsUi; // Lua
    private Control? _rightAnchorParent; // Lua
    private LayoutPreset _rightAnchorPreset; // Lua
    private bool _rightAnchorUpdatePending; // Lua

    public override void Initialize()
    {
        base.Initialize();

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
        gameplayStateLoad.OnScreenUnload += OnScreenUnload;

        _cfg.OnValueChanged(CLVars.AlertsIconScale, OnAlertsIconScaleChanged, invokeImmediately: true); // Lua
        _cfg.OnValueChanged(CLVars.AlertsPosition, OnAlertsPositionChanged, invokeImmediately: true); // Lua
    }

    private void OnAlertsIconScaleChanged(float value)
    {
        if (_alertsUi == null) return; // Lua
        _alertsUi.SetIconScale(value); // Lua
        RequestRightAnchoringUpdate(); // Lua
    }

    private void OnAlertsPositionChanged(string value) // Lua
    {
        ApplyPosition(value);
        SyncAlerts();
    }

    private void OnScreenUnload() // Lua
    {
        SetRightAnchorParent(null, default);
        if (_alertsUi != null)
        {
            _alertsUi.AlertPressed -= OnAlertPressed;
            _alertsUi.Orphan();
            _alertsUi = null;
        }
    }

    private void OnScreenLoad() // Lua
    {
        _alertsUi = new AlertsUI();
        _alertsUi.AlertPressed += OnAlertPressed;
        _alertsUi.SetIconScale(_cfg.GetCVar(CLVars.AlertsIconScale));

        ApplyPosition(_cfg.GetCVar(CLVars.AlertsPosition));
        SyncAlerts();
    }

    private void OnAlertPressed(object? sender, ProtoId<AlertPrototype> e)
    {
        _alertsSystem?.AlertClicked(e);
    }

    private void SystemOnClearAlerts(object? sender, EventArgs e)
    {
        _alertsUi?.ClearAllControls(); // Lua
    }

    private void SystemOnSyncAlerts(object? sender, IReadOnlyDictionary<AlertKey, AlertState> e)
    {
        if (sender is ClientAlertsSystem system)
        {
            _alertsUi?.SyncControls(system, system.AlertOrder, e); // Lua
            RequestRightAnchoringUpdate();
        }
    }

    public void OnSystemLoaded(ClientAlertsSystem system)
    {
        system.SyncAlerts += SystemOnSyncAlerts;
        system.ClearAlerts += SystemOnClearAlerts;
    }

    public void OnSystemUnloaded(ClientAlertsSystem system)
    {
        system.SyncAlerts -= SystemOnSyncAlerts;
        system.ClearAlerts -= SystemOnClearAlerts;
    }


    public void OnStateEntered(GameplayState state)
    {
        // initially populate the frame if system is available
        SyncAlerts();
    }

    public void SyncAlerts()
    {
        var alerts = _alertsSystem?.ActiveAlerts;
        if (alerts != null)
        {
            SystemOnSyncAlerts(_alertsSystem, alerts);
        }
    }

    public void UpdateAlertSpriteEntity(EntityUid spriteViewEnt, AlertPrototype alert)
    {
        if (_player.LocalEntity is not { } player)
            return;

        if (!EntityManager.TryGetComponent<SpriteComponent>(spriteViewEnt, out var sprite))
            return;

        var ev = new UpdateAlertSpriteEvent((spriteViewEnt, sprite), player, alert);
        EntityManager.EventBus.RaiseLocalEvent(player, ref ev);
        EntityManager.EventBus.RaiseLocalEvent(spriteViewEnt, ref ev);
    }

    private void ApplyPosition(string value) // Lua
    {
        if (_alertsUi == null || UIManager.ActiveScreen is not { } screen) return;
        var rightContainer = screen switch
        {
            DefaultGameScreen d => d.AlertsContainer,
            SeparatedChatGameScreen s => s.AlertsContainer,
            _ => null
        };
        var hotbar = FindChild<HotbarGui>(screen);
        var bottomContainer = hotbar?.AlertsContainer;
        var wantBottom = value == "bottom" && bottomContainer != null;
        _alertsUi.Orphan();
        if (wantBottom)
        {
            SetRightAnchorParent(null, default);
            _alertsUi.SetLayoutMode(AlertsLayoutMode.Bottom);
            bottomContainer!.AddChild(_alertsUi);
        }
        else if (rightContainer != null)
        {
            var preset = LayoutPreset.TopRight;
            SetRightAnchorParent(rightContainer, preset);
            _alertsUi.SetLayoutMode(AlertsLayoutMode.Right);
            rightContainer.AddChild(_alertsUi);
            RequestRightAnchoringUpdate();
        }
    }

    private void SetRightAnchorParent(Control? parent, LayoutPreset preset) // Lua
    {
        if (_rightAnchorParent != null) _rightAnchorParent.OnResized -= RequestRightAnchoringUpdate;
        _rightAnchorParent = parent;
        _rightAnchorPreset = preset;
        if (_rightAnchorParent != null) _rightAnchorParent.OnResized += RequestRightAnchoringUpdate;
    }

    private void RequestRightAnchoringUpdate() // Lua
    {
        if (_alertsUi == null || _rightAnchorParent == null) return;
        if (_alertsUi.LayoutMode != AlertsLayoutMode.Right) return;
        if (_rightAnchorUpdatePending) return;
        _rightAnchorUpdatePending = true;
        _alertsUi.InvalidateMeasure();
        UIManager.DeferAction(() =>
        {
            _rightAnchorUpdatePending = false;
            UpdateRightAnchoringImmediate();
        });
    }

    private void UpdateRightAnchoringImmediate() // Lua
    {
        if (_alertsUi == null || _rightAnchorParent == null) return;
        if (_alertsUi.LayoutMode != AlertsLayoutMode.Right) return;
        SetAnchorAndMarginPreset(_alertsUi, _rightAnchorPreset, LayoutPresetMode.MinSize, margin: 10);
    }

    private static T? FindChild<T>(Control root) where T : Control // Lua
    {
        if (root is T t) return t;
        foreach (var child in root.Children)
        {
            var found = FindChild<T>(child);
            if (found != null) return found;
        }
        return null;
    }
}
