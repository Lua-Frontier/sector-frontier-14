using System.Linq;
using System.Numerics;
using Content.Client.Actions;
using Content.Client.Construction;
using Content.Client.Gameplay;
using Content.Client.Hands;
using Content.Client.Interaction;
using Content.Client.Outline;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Actions.Controls;
using Content.Client.UserInterface.Systems.Actions.Widgets;
using Content.Client.UserInterface.Systems.Actions.Windows;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.CCVar;
using Content.Shared.Charges.Systems;
using Content.Shared.Input;
using Content.Shared.Lua.CLVar;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.ContentPack;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using static Content.Client.Actions.ActionsSystem;
using static Content.Client.UserInterface.Systems.Actions.Windows.ActionsWindow;
using static Robust.Client.UserInterface.Control;
using static Robust.Client.UserInterface.Controls.BaseButton;
using static Robust.Client.UserInterface.Controls.LineEdit;
using static Robust.Client.UserInterface.Controls.MultiselectOptionButton<
    Content.Client.UserInterface.Systems.Actions.Windows.ActionsWindow.Filters>;
using static Robust.Client.UserInterface.Controls.TextureRect;
using static Robust.Shared.Input.Binding.PointerInputCmdHandler;

namespace Content.Client.UserInterface.Systems.Actions;

public sealed class ActionUIController : UIController, IOnStateChanged<GameplayState>, IOnSystemChanged<ActionsSystem>
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IResourceManager _res = default!;

    [UISystemDependency] private readonly ActionsSystem? _actionsSystem = default;
    [UISystemDependency] private readonly InteractionOutlineSystem? _interactionOutline = default;
    [UISystemDependency] private readonly TargetOutlineSystem? _targetOutline = default;
    [UISystemDependency] private readonly SpriteSystem _spriteSystem = default!;

    private const int DefaultPresetIndex = 0;
    private const int ButtonsPerColumn = 10;
    private const int ColumnsPerPreset = 3;
    private const int PresetCount = 10;
    private ActionButtonContainer? _container;
    private readonly List<EntityUid?> _actions = new();
    private readonly Dictionary<int, List<string>> _savedLayout = new();
    private bool _layoutLoaded = false;
    private string? _currentProfileKey;
    private int _currentPresetIndex = DefaultPresetIndex;
    private readonly DragDropHelper<ActionButton> _menuDragHelper;
    private readonly TextureRect _dragShadow;
    private ActionsWindow? _window;

    private static readonly BoundKeyFunction[] PrimaryHotbarKeys =
    {
        ContentKeyFunctions.Hotbar1,
        ContentKeyFunctions.Hotbar2,
        ContentKeyFunctions.Hotbar3,
        ContentKeyFunctions.Hotbar4,
        ContentKeyFunctions.Hotbar5,
        ContentKeyFunctions.Hotbar6,
        ContentKeyFunctions.Hotbar7,
        ContentKeyFunctions.Hotbar8,
        ContentKeyFunctions.Hotbar9,
        ContentKeyFunctions.Hotbar0,
    };

    private static readonly BoundKeyFunction[] PresetBoundKeys =
    {
        ContentKeyFunctions.HotbarShift1,
        ContentKeyFunctions.HotbarShift2,
        ContentKeyFunctions.HotbarShift3,
        ContentKeyFunctions.HotbarShift4,
        ContentKeyFunctions.HotbarShift5,
        ContentKeyFunctions.HotbarShift6,
        ContentKeyFunctions.HotbarShift7,
        ContentKeyFunctions.HotbarShift8,
        ContentKeyFunctions.HotbarShift9,
        ContentKeyFunctions.HotbarShift0,
    };

    private ActionsBar? ActionsBar => UIManager.GetActiveUIWidgetOrNull<ActionsBar>();
    private MenuButton? ActionButton => UIManager.GetActiveUIWidgetOrNull<MenuBar.Widgets.GameTopMenuBar>()?.ActionButton;

    public bool IsDragging => _menuDragHelper.IsDragging;

    private int ButtonsPerPreset => ButtonsPerColumn * ColumnsPerPreset;
    private int CurrentPresetStart => _currentPresetIndex * ButtonsPerPreset;
    private ResPath GetLayoutPath()
    {
        var serverId = _cfg.GetCVar(CCVars.ServerId);
        if (string.IsNullOrWhiteSpace(_currentProfileKey))
            return new($"/lua/actions_layout_/{serverId}.txt");
        return new($"/lua/actions_layout_/{serverId}_{_currentProfileKey}.txt");
    }

    private string? GetMobProfileKey(EntityUid mob)
    {
        if (!EntityManager.TryGetComponent<MetaDataComponent>(mob, out var meta))
            return null;
        return meta.EntityPrototype?.ID;
    }

    /// <summary>
    /// Action slot we are currently selecting a target for.
    /// </summary>
    public EntityUid? SelectingTargetFor { get; private set; }

    public ActionUIController()
    {
        _menuDragHelper = new DragDropHelper<ActionButton>(OnMenuBeginDrag, OnMenuContinueDrag, OnMenuEndDrag);
        _dragShadow = new TextureRect
        {
            MinSize = new Vector2(64, 64),
            Stretch = StretchMode.Scale,
            Visible = false,
            SetSize = new Vector2(64, 64),
            MouseFilter = MouseFilterMode.Ignore
        };
    }

    public override void Initialize()
    {
        base.Initialize();

        _currentPresetIndex = Math.Clamp(_cfg.GetCVar(CLVars.ActionsActivePreset), 0, PresetCount - 1);

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
        gameplayStateLoad.OnScreenUnload += OnScreenUnload;
    }

    private void OnScreenLoad()
    {
       LoadGui();
    }

    private void OnScreenUnload()
    {
        UnloadGui();
    }

    public void OnStateEntered(GameplayState state)
    {
        if (_actionsSystem != null)
        {
            _actionsSystem.OnActionAdded += OnActionAdded;
            _actionsSystem.OnActionRemoved += OnActionRemoved;
            _actionsSystem.ActionsUpdated += OnActionsUpdated;
        }

        UpdateFilterLabel();
        QueueWindowUpdate();

        _dragShadow.Orphan();
        UIManager.PopupRoot.AddChild(_dragShadow);

        var builder = CommandBinds.Builder;
        var hotbarKeys = PrimaryHotbarKeys;
        for (var i = 0; i < hotbarKeys.Length; i++)
        {
            var boundId = i; // This is needed, because the lambda captures it.
            var boundKey = hotbarKeys[i];
            builder = builder.Bind(boundKey, new PointerInputCmdHandler((in PointerInputCmdArgs args) =>
            {
                if (args.State != BoundKeyState.Down)
                    return false;

                TriggerAction(boundId);
                return true;
            }, false, true));
        }

        var secondaryHotbarKeys = PresetBoundKeys;
        for (var i = 0; i < secondaryHotbarKeys.Length; i++)
        {
            var boundId = ButtonsPerColumn + i;
            var boundKey = secondaryHotbarKeys[i];
            builder = builder.Bind(boundKey, new PointerInputCmdHandler((in PointerInputCmdArgs args) =>
            {
                if (args.State != BoundKeyState.Down)
                    return false;

                TriggerAction(boundId);
                return true;
            }, false, true));
        }

        builder
            .Bind(ContentKeyFunctions.OpenActionsMenu,
                InputCmdHandler.FromDelegate(_ => ToggleWindow()))
            .BindBefore(EngineKeyFunctions.Use, new PointerInputCmdHandler(TargetingOnUse, outsidePrediction: true),
                    typeof(ConstructionSystem), typeof(DragDropSystem))
                .BindBefore(EngineKeyFunctions.UIRightClick, new PointerInputCmdHandler(TargetingCancel, outsidePrediction: true))
            .Register<ActionUIController>();
    }

    private bool TargetingCancel(in PointerInputCmdArgs args)
    {
        if (!_timing.IsFirstTimePredicted)
            return false;

        // only do something for actual target-based actions
        if (SelectingTargetFor == null)
            return false;

        StopTargeting();
        return true;
    }

    /// <summary>
    ///     If the user clicked somewhere, and they are currently targeting an action, try and perform it.
    /// </summary>
    private bool TargetingOnUse(in PointerInputCmdArgs args)
    {
        if (!_timing.IsFirstTimePredicted || _actionsSystem == null || SelectingTargetFor is not { } actionId)
            return false;

        if (_playerManager.LocalEntity is not { } user)
            return false;

        if (!EntityManager.TryGetComponent<ActionsComponent>(user, out var comp))
            return false;

        if (_actionsSystem.GetAction(actionId) is not {} action ||
            !EntityManager.TryGetComponent<TargetActionComponent>(action, out var target))
        {
            return false;
        }

        // Is the action currently valid?
        if (!_actionsSystem.ValidAction(action))
        {
            // The user is targeting with this action, but it is not valid. Maybe mark this click as
            // handled and prevent further interactions.
            return !target.InteractOnMiss;
        }

        var ev = new ActionTargetAttemptEvent(args, (user, comp), action);
        EntityManager.EventBus.RaiseLocalEvent(action, ref ev);
        if (!ev.Handled)
        {
            Log.Error($"Action {EntityManager.ToPrettyString(actionId)} did not handle ActionTargetAttemptEvent!");
            return false;
        }

        // stop targeting when needed
        if (ev.FoundTarget ? !target.Repeat : target.DeselectOnMiss)
            StopTargeting();

        return true;
    }

    public void UnloadButton()
    {
        if (ActionButton != null)
            ActionButton.OnPressed -= ActionButtonPressed;
    }

    public void LoadButton()
    {
        if (ActionButton != null)
            ActionButton.OnPressed += ActionButtonPressed;
    }

    private void OnWindowOpened()
    {
        ActionButton?.SetClickPressed(true);

        SearchAndDisplay();
    }

    private void OnWindowClosed()
    {
        ActionButton?.SetClickPressed(false);
    }

    public void OnStateExited(GameplayState state)
    {
        if (_actionsSystem != null)
        {
            _actionsSystem.OnActionAdded -= OnActionAdded;
            _actionsSystem.OnActionRemoved -= OnActionRemoved;
            _actionsSystem.ActionsUpdated -= OnActionsUpdated;
        }

        CommandBinds.Unregister<ActionUIController>();
    }

    private void TriggerAction(int index)
    {
        var globalIndex = CurrentPresetStart + index;
        if (!_actions.TryGetValue(globalIndex, out var actionId) ||
            _actionsSystem?.GetAction(actionId) is not {} action)
        {
            return;
        }

        // TODO: probably should have a clientside event raised for flexibility
        if (EntityManager.TryGetComponent<TargetActionComponent>(action, out var target))
            ToggleTargeting((action, action, target));
        else
            _actionsSystem?.TriggerAction(action);
    }

    private void OnActionAdded(EntityUid actionId)
    {
        if (_actionsSystem?.GetAction(actionId) is not {} action)
            return;

        // TODO: event
        // if the action is toggled when we add it, start targeting
        if (action.Comp.Toggled && EntityManager.TryGetComponent<TargetActionComponent>(actionId, out var target))
            StartTargeting((action, action, target));

        if (_actions.Contains(action))
            return;

        RebuildCurrentLayoutFromSaved();
    }

    private void OnActionRemoved(EntityUid actionId)
    {
        if (actionId == SelectingTargetFor)
            StopTargeting();

        RebuildCurrentLayoutFromSaved();
    }

    private void OnActionsUpdated()
    {
        QueueWindowUpdate();

        if (_actionsSystem != null)
        {
            RebuildCurrentLayoutFromSaved();
            _container?.SetActionData(_actionsSystem, GetCurrentPresetActions());
        }
    }

    private void ChangePreset(int index)
    {
        if (_actionsSystem == null)
            return;

        if (index < 0)
            index = PresetCount - 1;
        else if (index > PresetCount - 1)
            index = 0;

        _currentPresetIndex = index;
        _cfg.SetCVar(CLVars.ActionsActivePreset, _currentPresetIndex);
        _container?.SetActionData(_actionsSystem, GetCurrentPresetActions());

        if (ActionsBar != null)
            ActionsBar.PageButtons.Label.Text = $"{_currentPresetIndex + 1}";
    }

    private void OnLeftArrowPressed(ButtonEventArgs args) => ChangePreset(_currentPresetIndex - 1);

    private void OnRightArrowPressed(ButtonEventArgs args) => ChangePreset(_currentPresetIndex + 1);

    private EntityUid?[] GetCurrentPresetActions()
    {
        var preset = new EntityUid?[ButtonsPerPreset];
        for (var i = 0; i < ButtonsPerPreset; i++)
        {
            var globalIndex = CurrentPresetStart + i;
            if (_actions.TryGetValue(globalIndex, out var action))
                preset[i] = action;
        }

        return preset;
    }

    private void ActionButtonPressed(ButtonEventArgs args)
    {
        ToggleWindow();
    }

    private void ToggleWindow()
    {
        if (_window == null)
            return;

        if (_window.IsOpen)
        {
            _window.Close();
            return;
        }

        _window.Open();
    }

    private void UpdateFilterLabel()
    {
        if (_window == null)
            return;

        if (_window.FilterButton.SelectedKeys.Count == 0)
        {
            _window.FilterLabel.Visible = false;
        }
        else
        {
            _window.FilterLabel.Visible = true;
            _window.FilterLabel.Text = Loc.GetString("ui-actionmenu-filter-label",
                ("selectedLabels", string.Join(", ", _window.FilterButton.SelectedLabels)));
        }
    }

    private bool MatchesFilter(Entity<ActionComponent> ent, Filters filter)
    {
        var (uid, comp) = ent;
        return filter switch
        {
            Filters.Enabled => comp.Enabled,
            Filters.Item => comp.Container != null && comp.Container != _playerManager.LocalEntity,
            Filters.Innate => comp.Container == null || comp.Container == _playerManager.LocalEntity,
            Filters.Instant => EntityManager.HasComponent<InstantActionComponent>(uid),
            Filters.Targeted => EntityManager.HasComponent<TargetActionComponent>(uid),
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, null)
        };
    }

    private void ClearList()
    {
        if (_window?.Disposed == false)
            _window.ResultsGrid.RemoveAllChildren();
    }

    private void PopulateActions(IEnumerable<Entity<ActionComponent>> actions)
    {
        if (_window is not { Disposed: false, IsOpen: true })
            return;

        if (_actionsSystem == null)
            return;

        _window.UpdateNeeded = false;

        List<ActionButton> existing = new(_window.ResultsGrid.ChildCount);
        foreach (var child in _window.ResultsGrid.Children)
        {
            if (child is ActionButton button)
                existing.Add(button);
        }

        int i = 0;
        foreach (var action in actions)
        {
            if (i < existing.Count)
            {
                existing[i++].UpdateData(action, _actionsSystem);
                continue;
            }

            var button = new ActionButton(EntityManager, _spriteSystem, this) {Locked = true};
            button.ActionPressed += OnWindowActionPressed;
            button.ActionUnpressed += OnWindowActionUnPressed;
            button.ActionFocusExited += OnWindowActionFocusExisted;
            button.UpdateData(action, _actionsSystem);
            _window.ResultsGrid.AddChild(button);
        }

        for (; i < existing.Count; i++)
        {
            existing[i].Orphan();
        }
    }

    public void QueueWindowUpdate()
    {
        if (_window != null)
            _window.UpdateNeeded = true;
    }

    private void SearchAndDisplay()
    {
        if (_window is not { Disposed: false, IsOpen: true })
            return;

        if (_actionsSystem == null)
            return;

        if (_playerManager.LocalEntity is not { } player)
            return;

        var search = _window.SearchBar.Text;
        var filters = _window.FilterButton.SelectedKeys;
        var actions = _actionsSystem.GetClientActions();

        if (filters.Count == 0 && string.IsNullOrWhiteSpace(search))
        {
            PopulateActions(actions);
            return;
        }

        actions = actions.Where(action =>
        {
            if (filters.Count > 0 && filters.Any(filter => !MatchesFilter(action, filter)))
                return false;

            if (action.Comp.Keywords.Any(keyword => search.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                return true;

            var name = EntityManager.GetComponent<MetaDataComponent>(action).EntityName;
            if (name.Contains(search, StringComparison.OrdinalIgnoreCase))
                return true;

            if (action.Comp.Container == null || action.Comp.Container == player)
                return false;

            var providerName = EntityManager.GetComponent<MetaDataComponent>(action.Comp.Container.Value).EntityName;
            return providerName.Contains(search, StringComparison.OrdinalIgnoreCase);
        });

        PopulateActions(actions);
    }

    private void SetAction(ActionButton button, EntityUid? actionId, bool updateSlots = true)
    {
        if (_actionsSystem == null)
            return;

        int position;

        if (actionId == null)
        {
            button.ClearData();
            if (_container?.TryGetButtonIndex(button, out position) ?? false)
            {
                var globalIndex = CurrentPresetStart + position;
                if (_actions.Count > globalIndex && globalIndex >= 0)
                    _actions[globalIndex] = null;

                _savedLayout.Remove(globalIndex);
            }
        }
        else if (button.TryReplaceWith(actionId.Value, _actionsSystem) &&
            _container != null &&
            _container.TryGetButtonIndex(button, out position))
        {
            var globalIndex = CurrentPresetStart + position;
            if (globalIndex >= _actions.Count)
            {
                while (_actions.Count <= globalIndex)
                    _actions.Add(null);

                _actions[globalIndex] = actionId;
            }
            else
            {
                _actions[globalIndex] = actionId;
            }

            var reservationKey = GetActionReservationKey(actionId);
            if (!string.IsNullOrWhiteSpace(reservationKey))
            {
                RemovePrototypeReservation(reservationKey);
                AddSlotReservation(globalIndex, reservationKey);
            }
        }

        if (updateSlots)
        {
            _container?.SetActionData(_actionsSystem, GetCurrentPresetActions());
            SaveLayout();
        }
    }

    private void DragAction()
    {
        if (_menuDragHelper.Dragged is not {Action: {} action} dragged)
        {
            _menuDragHelper.EndDrag();
            return;
        }

        EntityUid? swapAction = null;
        var currentlyHovered = UIManager.MouseGetControl(_input.MouseScreenPosition);
        if (currentlyHovered is ActionButton button)
        {
            swapAction = button.Action;
            SetAction(button, action, false);
        }

        if (dragged.Parent is ActionButtonContainer)
            SetAction(dragged, swapAction, false);

        if (_actionsSystem != null)
            _container?.SetActionData(_actionsSystem, GetCurrentPresetActions());

        SaveLayout();

        _menuDragHelper.EndDrag();
    }

    private void OnClearPressed(ButtonEventArgs args)
    {
        if (_window == null)
            return;

        _window.SearchBar.Clear();
        _window.FilterButton.DeselectAll();
        UpdateFilterLabel();
        QueueWindowUpdate();
    }

    private void OnSearchChanged(LineEditEventArgs args)
    {
        QueueWindowUpdate();
    }

    private void OnFilterSelected(ItemPressedEventArgs args)
    {
        UpdateFilterLabel();
        QueueWindowUpdate();
    }

    private void OnWindowActionPressed(GUIBoundKeyEventArgs args, ActionButton action)
    {
        if (args.Function != EngineKeyFunctions.UIClick && args.Function != EngineKeyFunctions.Use)
            return;

        HandleActionPressed(args, action);
    }

    private void OnWindowActionUnPressed(GUIBoundKeyEventArgs args, ActionButton dragged)
    {
        if (args.Function != EngineKeyFunctions.UIClick && args.Function != EngineKeyFunctions.Use)
            return;

        HandleActionUnpressed(args, dragged);
    }

    private void OnWindowActionFocusExisted(ActionButton button)
    {
        _menuDragHelper.EndDrag();
    }

    private void OnActionPressed(GUIBoundKeyEventArgs args, ActionButton button)
    {
        if (args.Function == EngineKeyFunctions.UIRightClick)
        {
            SetAction(button, null);
            args.Handle();
            return;
        }

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        HandleActionPressed(args, button);
    }

    private void HandleActionPressed(GUIBoundKeyEventArgs args, ActionButton button)
    {
        args.Handle();
        if (button.Action != null)
        {
            _menuDragHelper.MouseDown(button);
            return;
        }

        // good job
    }

    private void OnActionUnpressed(GUIBoundKeyEventArgs args, ActionButton button)
    {
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        HandleActionUnpressed(args, button);
    }

    private void HandleActionUnpressed(GUIBoundKeyEventArgs args, ActionButton button)
    {
        if (_actionsSystem == null)
            return;

        args.Handle();

        if (_menuDragHelper.IsDragging)
        {
            DragAction();
            return;
        }

        _menuDragHelper.EndDrag();

        if (button.Action is not {} action)
            return;

        // TODO: make this an event
        if (!EntityManager.TryGetComponent<TargetActionComponent>(action, out var target))
        {
            _actionsSystem?.TriggerAction(action);
            return;
        }

        // for target actions, we go into "select target" mode, we don't
        // message the server until we actually pick our target.

        // if we're clicking the same thing we're already targeting for, then we simply cancel
        // targeting
        ToggleTargeting((action, action.Comp, target));
    }

    private bool OnMenuBeginDrag()
    {
        // TODO ACTIONS
        // The dragging icon shuld be based on the entity's icon style. I.e. if the action has a large icon texture,
        // and a small item/provider sprite, then the dragged icon should be the big texture, not the provider.
        if (_menuDragHelper.Dragged?.Action is {} action)
        {
            if (EntityManager.TryGetComponent(action.Comp.EntityIcon, out SpriteComponent? sprite)
                && sprite.Icon?.GetFrame(RsiDirection.South, 0) is {} frame)
            {
                _dragShadow.Texture = frame;
            }
            else if (action.Comp.Icon is {} icon)
            {
                _dragShadow.Texture = _spriteSystem.Frame0(icon);
            }
            else
            {
                _dragShadow.Texture = null;
            }
        }

        LayoutContainer.SetPosition(_dragShadow, UIManager.MousePositionScaled.Position - new Vector2(32, 32));
        return true;
    }

    private bool OnMenuContinueDrag(float frameTime)
    {
        LayoutContainer.SetPosition(_dragShadow, UIManager.MousePositionScaled.Position - new Vector2(32, 32));
        _dragShadow.Visible = true;
        return true;
    }

    private void OnMenuEndDrag()
    {
        _dragShadow.Texture = null;
        _dragShadow.Visible = false;
    }

    private void UnloadGui()
    {
        SaveLayout();
        _actionsSystem?.UnlinkAllActions();

        if (ActionsBar == null)
        {
            return;
        }

        ActionsBar.PageButtons.LeftArrow.OnPressed -= OnLeftArrowPressed;
        ActionsBar.PageButtons.RightArrow.OnPressed -= OnRightArrowPressed;

        if (_window != null)
        {
            _window.OnOpen -= OnWindowOpened;
            _window.OnClose -= OnWindowClosed;
            _window.ClearButton.OnPressed -= OnClearPressed;
            _window.SearchBar.OnTextChanged -= OnSearchChanged;
            _window.FilterButton.OnItemSelected -= OnFilterSelected;

            if (!_window.Disposed && _window.IsOpen)
                _window.Close();

            _window = null;
        }
    }

    private void LoadGui()
    {
        UnloadGui();
        _window = UIManager.CreateWindow<ActionsWindow>();
        LayoutContainer.SetAnchorPreset(_window, LayoutContainer.LayoutPreset.CenterTop);

        _window.OnOpen += OnWindowOpened;
        _window.OnClose += OnWindowClosed;
        _window.ClearButton.OnPressed += OnClearPressed;
        _window.SearchBar.OnTextChanged += OnSearchChanged;
        _window.FilterButton.OnItemSelected += OnFilterSelected;

        if (ActionsBar == null)
        {
            return;
        }

        ActionsBar.PageButtons.LeftArrow.OnPressed += OnLeftArrowPressed;
        ActionsBar.PageButtons.RightArrow.OnPressed += OnRightArrowPressed;
        ActionsBar.PageButtons.Label.Text = $"{_currentPresetIndex + 1}";

        RegisterActionContainer(ActionsBar.ActionsContainer);

        _actionsSystem?.LinkAllActions();
    }

    public void RegisterActionContainer(ActionButtonContainer container)
    {
        if (_container != null)
        {
            _container.ActionPressed -= OnActionPressed;
            _container.ActionUnpressed -= OnActionUnpressed;
        }

        _container = container;
        _container.ActionPressed += OnActionPressed;
        _container.ActionUnpressed += OnActionUnpressed;
    }

    private void ClearActions()
    {
        _container?.ClearActionData();
    }

    private void AssignSlots(List<SlotAssignment> assignments)
    {
        if (_actionsSystem == null)
            return;

        _actions.Clear();
        foreach (var assign in assignments)
        {
            if (!_actions.Contains(assign.ActionId))
                _actions.Add(assign.ActionId);
        }

        RebuildCurrentLayoutFromSaved();
        _container?.SetActionData(_actionsSystem, GetCurrentPresetActions());
    }

    public void RemoveActionContainer()
    {
        _container = null;
    }

    public void OnSystemLoaded(ActionsSystem system)
    {
        system.LinkActions += OnComponentLinked;
        system.UnlinkActions += OnComponentUnlinked;
        system.ClearAssignments += ClearActions;
        system.AssignSlot += AssignSlots;
    }

    public void OnSystemUnloaded(ActionsSystem system)
    {
        system.LinkActions -= OnComponentLinked;
        system.UnlinkActions -= OnComponentUnlinked;
        system.ClearAssignments -= ClearActions;
        system.AssignSlot -= AssignSlots;
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        _menuDragHelper.Update(args.DeltaSeconds);
        if (_window is {UpdateNeeded: true})
            SearchAndDisplay();
    }

    private void OnComponentLinked(ActionsComponent component)
    {
        if (_actionsSystem == null)
            return;

        _currentProfileKey = GetMobProfileKey(component.Owner);

        LoadDefaultActions();
        if (!_layoutLoaded)
        {
            LoadLayout();
            _layoutLoaded = true;
        }
        RebuildCurrentLayoutFromSaved();
        _container?.SetActionData(_actionsSystem, GetCurrentPresetActions());
        QueueWindowUpdate();
    }

    private string? GetActionReservationKey(EntityUid? actionUid)
    {
        if (actionUid is not { } uid)
            return null;

        if (!EntityManager.TryGetComponent<MetaDataComponent>(uid, out var meta))
            return null;

        var actionPrototype = meta.EntityPrototype?.ID;
        if (string.IsNullOrWhiteSpace(actionPrototype))
            return null;

        if (!EntityManager.TryGetComponent<ActionComponent>(uid, out var actionComp))
            return actionPrototype;

        if (actionComp.Container is not { } container)
            return actionPrototype;

        if (!EntityManager.TryGetComponent<MetaDataComponent>(container, out var containerMeta))
            return actionPrototype;

        var providerPrototype = containerMeta.EntityPrototype?.ID;
        if (string.IsNullOrWhiteSpace(providerPrototype))
            return actionPrototype;

        return $"{actionPrototype}|{providerPrototype}";
    }

    private void SaveLayout()
    {
        try
        {
            var lines = new List<string>();
            foreach (var (slot, prototypeIds) in _savedLayout.OrderBy(x => x.Key))
            {
                foreach (var prototypeId in prototypeIds)
                {
                    if (string.IsNullOrWhiteSpace(prototypeId))
                        continue;

                    lines.Add($"{slot}\t{prototypeId}");
                }
            }
            if (lines.Count == 0) return;
            var layoutPath = GetLayoutPath();
            _res.UserData.CreateDir(layoutPath.Directory);
            using var writer = _res.UserData.OpenWriteText(layoutPath.ToRootedPath());
            foreach (var line in lines)
            { writer.WriteLine(line); }
        }
        catch (Exception e)
        { Log.Warning($"Failed to save actions layout: {e.Message}"); }
    }

    private void LoadLayout()
    {
        try
        {
            var path = GetLayoutPath().ToRootedPath();
            if (!_res.UserData.Exists(path))
            {
                InitializeSavedLayoutFromCurrent();
                return;
            }

            using var reader = _res.UserData.OpenText(path);
            var savedSlots = new Dictionary<int, List<string>>();
            while (reader.ReadLine() is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var split = line.Split('\t', 2);
                if (split.Length != 2)
                    continue;

                if (!int.TryParse(split[0], out var slot) || slot < 0)
                    continue;

                var prototypeId = split[1].Trim();
                if (string.IsNullOrWhiteSpace(prototypeId))
                    continue;

                if (!savedSlots.TryGetValue(slot, out var list))
                {
                    list = new List<string>();
                    savedSlots[slot] = list;
                }

                if (!list.Contains(prototypeId))
                    list.Add(prototypeId);
            }

            if (savedSlots.Count == 0)
            {
                InitializeSavedLayoutFromCurrent();
                return;
            }

            _savedLayout.Clear();
            foreach (var (slot, prototypeIds) in savedSlots)
            {
                _savedLayout[slot] = prototypeIds;
            }
        }
        catch (Exception e)
        {
            Log.Warning($"Failed to load actions layout: {e.Message}");
        }
    }

    private void OnComponentUnlinked()
    {
        SaveLayout();
        _layoutLoaded = false;
        _currentProfileKey = null;
        _container?.ClearActionData();
        QueueWindowUpdate();
        StopTargeting();
    }

    private void LoadDefaultActions()
    {
        if (_actionsSystem == null)
            return;

        var actions = _actionsSystem.GetClientActions().Where(action => action.Comp.AutoPopulate).ToList();
        actions.Sort(ActionComparer);

        _actions.Clear();
        foreach (var (action, _) in actions)
        {
            if (!_actions.Contains(action))
                _actions.Add(action);
        }
    }

    private void InitializeSavedLayoutFromCurrent()
    {
        _savedLayout.Clear();
        for (var i = 0; i < _actions.Count; i++)
        {
            var reservationKey = GetActionReservationKey(_actions[i]);
            if (string.IsNullOrWhiteSpace(reservationKey))
                continue;

            AddSlotReservation(i, reservationKey);
        }
    }

    private void AddSlotReservation(int slot, string prototypeId)
    {
        if (!_savedLayout.TryGetValue(slot, out var list))
        {
            list = new List<string>();
            _savedLayout[slot] = list;
        }

        if (!list.Contains(prototypeId))
            list.Add(prototypeId);
    }

    private void RemovePrototypeReservation(string prototypeId)
    {
        var emptySlots = new List<int>();
        foreach (var (slot, list) in _savedLayout)
        {
            list.RemoveAll(proto => proto == prototypeId);
            if (list.Count == 0)
                emptySlots.Add(slot);
        }

        foreach (var slot in emptySlots)
        {
            _savedLayout.Remove(slot);
        }
    }

    private void RebuildCurrentLayoutFromSaved()
    {
        if (_actionsSystem == null)
            return;

        var available = _actionsSystem.GetClientActions().Select(x => x.Owner).ToList();
        var availableLookup = new Dictionary<string, Queue<EntityUid>>();
        foreach (var action in available)
        {
            var reservationKey = GetActionReservationKey(action);
            if (string.IsNullOrWhiteSpace(reservationKey))
                continue;

            if (!availableLookup.TryGetValue(reservationKey, out var queue))
            {
                queue = new Queue<EntityUid>();
                availableLookup[reservationKey] = queue;
            }

            queue.Enqueue(action);
        }

        var maxSavedIndex = _savedLayout.Count > 0 ? _savedLayout.Keys.Max() : -1;
        var restoredSize = Math.Max(Math.Max(_actions.Count, available.Count), maxSavedIndex + 1);
        var restored = new List<EntityUid?>(Enumerable.Repeat<EntityUid?>(null, restoredSize));
        var used = new HashSet<EntityUid>();

        foreach (var (slot, prototypeIds) in _savedLayout.OrderBy(x => x.Key))
        {
            if (slot < 0 || slot >= restoredSize)
                continue;

            var targetSlot = slot;
            foreach (var prototypeId in prototypeIds)
            {
                if (!availableLookup.TryGetValue(prototypeId, out var queue) || queue.Count == 0)
                    continue;

                EntityUid? selected = null;
                while (queue.Count > 0)
                {
                    var candidate = queue.Dequeue();
                    if (used.Add(candidate))
                    {
                        selected = candidate;
                        break;
                    }
                }

                if (selected == null)
                    continue;

                while (targetSlot < restored.Count && restored[targetSlot] != null)
                    targetSlot++;

                if (targetSlot >= restored.Count)
                    restored.Add(selected);
                else
                    restored[targetSlot] = selected;
            }
        }

        var fillIndex = 0;
        foreach (var actionUid in available)
        {
            if (!used.Add(actionUid))
                continue;

            while (fillIndex < restored.Count && restored[fillIndex] != null)
                fillIndex++;

            if (fillIndex >= restored.Count)
                restored.Add(actionUid);
            else
                restored[fillIndex] = actionUid;
        }

        _actions.Clear();
        _actions.AddRange(restored);
    }

    /// <summary>
    /// If currently targeting with this slot, stops targeting.
    /// If currently targeting with no slot or a different slot, switches to
    /// targeting with the specified slot.
    /// </summary>
    private void ToggleTargeting(Entity<ActionComponent, TargetActionComponent> ent)
    {
        if (SelectingTargetFor == ent)
        {
            StopTargeting();
            return;
        }

        StartTargeting(ent);
    }

    /// <summary>
    /// Puts us in targeting mode, where we need to pick either a target point or entity
    /// </summary>
    private void StartTargeting(Entity<ActionComponent, TargetActionComponent> ent)
    {
        var (uid, action, target) = ent;

        // If we were targeting something else we should stop
        StopTargeting();

        SelectingTargetFor = uid;
        // TODO inform the server
        _actionsSystem?.SetToggled(uid, true);

        // override "held-item" overlay
        var provider = action.Container;

        if (target.TargetingIndicator && _overlays.TryGetOverlay<ShowHandItemOverlay>(out var handOverlay))
        {
            if (action.ItemIconStyle == ItemActionIconStyle.BigItem && action.Container != null)
            {
                handOverlay.EntityOverride = provider;
            }
            else if (action.Toggled && action.IconOn != null)
                handOverlay.IconOverride = _spriteSystem.Frame0(action.IconOn);
            else if (action.Icon != null)
                handOverlay.IconOverride = _spriteSystem.Frame0(action.Icon);
        }

        if (_container != null)
        {
            foreach (var button in _container.GetButtons())
            {
                if (button.Action?.Owner == uid)
                    button.UpdateIcons();
            }
        }

        // TODO: allow world-targets to check valid positions. E.g., maybe:
        // - Draw a red/green ghost entity
        // - Add a yes/no checkmark where the HandItemOverlay usually is

        // Highlight valid entity targets
        if (!EntityManager.TryGetComponent<EntityTargetActionComponent>(uid, out var entity))
            return;

        Func<EntityUid, bool>? predicate = null;
        var attachedEnt = action.AttachedEntity;

        if (!entity.CanTargetSelf)
            predicate = e => e != attachedEnt;

        var range = target.CheckCanAccess ? target.Range : -1;

        _interactionOutline?.SetEnabled(false);
        _targetOutline?.Enable(range, target.CheckCanAccess, predicate, entity.Whitelist, entity.Blacklist, null);
    }

    /// <summary>
    /// Switch out of targeting mode if currently selecting target for an action
    /// </summary>
    private void StopTargeting()
    {
        if (SelectingTargetFor == null)
            return;

        var oldAction = SelectingTargetFor;
        // TODO inform the server
        _actionsSystem?.SetToggled(oldAction, false);

        SelectingTargetFor = null;

        _targetOutline?.Disable();
        _interactionOutline?.SetEnabled(true);

        if (_container != null)
        {
            foreach (var button in _container.GetButtons())
            {
                if (button.Action?.Owner == oldAction)
                    button.UpdateIcons();
            }
        }

        if (!_overlays.TryGetOverlay<ShowHandItemOverlay>(out var handOverlay))
            return;

        handOverlay.IconOverride = null;
        handOverlay.EntityOverride = null;
    }
}
