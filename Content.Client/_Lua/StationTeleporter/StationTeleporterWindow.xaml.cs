// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Client.Administration.Managers;
using Content.Client.Pinpointer.UI;
using Content.Client.UserInterface.Controls;
using Content.Shared._Lua.StationTeleporter;
using Content.Shared._Lua.StationTeleporter.Components;
using Content.Shared.Shuttles.UI.MapObjects;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Client._Lua.StationTeleporter;

public sealed partial class StationTeleporterWindow : FancyWindow
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IClientAdminManager _adminManager = default!;
    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _spriteSystem;
    private static readonly ResPath BlipTexturePath = new("/Textures/_Lua/Interface/NavMap/ring.png");
    private Texture? _blipTexture;
    public Action<NetEntity>? OnTeleporterClick;
    public Action<string>? OnRename;
    private EntityUid _owner;
    private TeleporterType _currentType;
    private StationTeleporterNavMapControl _navMap = default!;
    private StationTeleporterSectorMapControl _sectorMap = default!;
    private BoxContainer _teleporterList = default!;
    private BoxContainer _renamePanel = default!;
    private Label _stationName = default!;
    public StationTeleporterWindow()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);
        _transform = _entMan.System<SharedTransformSystem>();
        _spriteSystem = _entMan.System<SpriteSystem>();
        _navMap = FindControl<StationTeleporterNavMapControl>("NavMap");
        _sectorMap = FindControl<StationTeleporterSectorMapControl>("SectorMap");
        _teleporterList = FindControl<BoxContainer>("TeleporterList");
        _renamePanel = FindControl<BoxContainer>("RenamePanel");
        _stationName = FindControl<Label>("StationName");
        _blipTexture = _spriteSystem.Frame0(new SpriteSpecifier.Texture(BlipTexturePath));
        FindControl<Button>("RenameBtn").OnPressed += _ =>
        { OnRename?.Invoke(FindControl<LineEdit>("RenameEdit").Text); };
    }

    public void SetOwner(EntityUid owner)
    {
        _owner = owner;
        _navMap.Owner = _owner;
        _navMap.TileColor = Color.FromHex("#1a1a1a");
        _navMap.WallColor = Color.FromHex("#404040");
        if (_entMan.TryGetComponent<TransformComponent>(_owner, out var xform))
        {
            _navMap.MapUid = xform.GridUid;
            if (_entMan.TryGetComponent<MetaDataComponent>(xform.GridUid, out var meta)) _stationName.Text = meta.EntityName;
        }
        _navMap.ForceNavMapUpdate();
        if (_entMan.TryGetComponent<TransformComponent>(_owner, out var ownerXform) && ownerXform.GridUid != null) _sectorMap.InnerMap.SetShuttle(ownerXform.GridUid);
        if (_blipTexture != null && _entMan.TryGetComponent<TransformComponent>(_owner, out var selfXform))
        {
            var selfNet = _entMan.GetNetEntity(_owner);
            _navMap.TrackedEntities[selfNet] = new NavMapBlip(selfXform.Coordinates, _blipTexture, Color.Yellow, false, false, 0.5f);
        }
    }

    public void UpdateState(StationTeleporterState state)
    {
        _currentType = state.TeleporterType;
        _renamePanel.Visible = _currentType == TeleporterType.Local && _adminManager.IsAdmin();
        var isSector = _currentType == TeleporterType.Sector;
        _navMap.Visible = !isSector;
        _sectorMap.Visible = isSector;
        if (isSector) UpdateSectorMap(state);
        else UpdateNavMapBlips(state);
        UpdateList(state);
    }

    private void UpdateSectorMap(StationTeleporterState state)
    {
        if (!_entMan.TryGetComponent<TransformComponent>(_owner, out var xform)) return;
        var mapId = xform.MapID;
        if (mapId == MapId.Nullspace) return;
        var worldPos = _transform.GetWorldPosition(_owner);
        _sectorMap.InnerMap.SetMap(mapId, worldPos);
        var mapObjects = new Dictionary<MapId, List<IMapObject>>();
        var objectList = new List<IMapObject>();
        foreach (var grid in _mapManager.GetAllGrids(mapId))
        {
            var name = _entMan.TryGetComponent<MetaDataComponent>(grid.Owner, out var meta) ? meta.EntityName : string.Empty;
            objectList.Add(new GridMapObject
            {
                Name = name,
                Entity = grid.Owner,
                HideButton = false,
            });
        }
        foreach (var tp in state.Teleporters)
        { objectList.Add(new ShuttleBeaconObject(tp.Uid, tp.Coordinates, string.Empty)); }
        objectList.Add(new ShuttleBeaconObject(state.SelfUid, _entMan.GetNetCoordinates(xform.Coordinates), string.Empty));
        mapObjects[mapId] = objectList;
        _sectorMap.InnerMap.SetMapObjects(mapObjects);
        _sectorMap.LinkedTeleporterPairs.Clear();
        foreach (var tp in state.Teleporters)
        {
            if (tp.LinkedCoordinates == null) continue;
            var aMap = _transform.ToMapCoordinates(_entMan.GetCoordinates(tp.Coordinates));
            var bMap = _transform.ToMapCoordinates(_entMan.GetCoordinates(tp.LinkedCoordinates.Value));
            if (aMap.MapId != MapId.Nullspace && aMap.MapId == bMap.MapId) _sectorMap.LinkedTeleporterPairs.Add((aMap, bMap));
        }
    }

    private void UpdateNavMapBlips(StationTeleporterState state)
    {
        if (_blipTexture == null) return;
        var selfNet = _entMan.GetNetEntity(_owner);
        _navMap.TrackedEntities.Clear();
        _navMap.LinkedTeleporterPairs.Clear();
        if (_entMan.TryGetComponent<TransformComponent>(_owner, out var selfXform)) _navMap.TrackedEntities[selfNet] = new NavMapBlip(selfXform.Coordinates, _blipTexture, Color.Yellow, false, false, 0.5f);
        foreach (var tp in state.Teleporters)
        {
            var isLinked = tp.LinkedCoordinates != null;
            var tpCoords = _entMan.GetCoordinates(tp.Coordinates);
            var blipColor = isLinked ? Color.Cyan : Color.DimGray;
            _navMap.TrackedEntities[tp.Uid] = new NavMapBlip(tpCoords, _blipTexture, blipColor, false, false, 0.5f);
            if (!isLinked) continue;
            var aMap = _transform.ToMapCoordinates(_entMan.GetCoordinates(tp.Coordinates));
            var bMap = _transform.ToMapCoordinates(_entMan.GetCoordinates(tp.LinkedCoordinates!.Value));
            if (aMap.MapId != MapId.Nullspace && aMap.MapId == bMap.MapId) _navMap.LinkedTeleporterPairs.Add((aMap, bMap));
        }
    }

    private void UpdateList(StationTeleporterState state)
    {
        _teleporterList.RemoveAllChildren();
        foreach (var tp in state.Teleporters)
        {
            var isLinked = tp.LinkedCoordinates != null;
            var bgColor = isLinked ? Color.FromHex("#1a3a1a") : Color.FromHex("#2a2a2a");
            var row = new PanelContainer
            {
                Margin = new Thickness(0, 2),
                HorizontalExpand = true,
                PanelOverride = new StyleBoxFlat { BackgroundColor = bgColor }
            };
            var inner = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                Margin = new Thickness(4),
                HorizontalExpand = true
            };
            var nameLabel = new Label
            {
                Text = tp.Name,
                HorizontalExpand = true,
                ClipText = true,
                Margin = new Thickness(0, 0, 0, 2)
            };
            if (!tp.Powered) nameLabel.FontColorOverride = Color.Gray;
            var buttons = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                HorizontalExpand = true
            };
            var capturedCoords = tp.Coordinates;
            var locateBtn = new Button
            {
                Text = Loc.GetString("station-teleporter-btn-locate"),
                HorizontalExpand = true,
                Disabled = !tp.Powered
            };
            locateBtn.OnPressed += _ =>
            {
                if (_navMap.Visible) _navMap.CenterToCoordinates(_entMan.GetCoordinates(capturedCoords));
                else if (_sectorMap.Visible)
                {
                    var mapCoords = _transform.ToMapCoordinates(_entMan.GetCoordinates(capturedCoords));
                    _sectorMap.InnerMap.Offset = mapCoords.Position;
                }
            };

            var linkBtnText = !tp.Powered ? Loc.GetString("station-teleporter-btn-unpowered") : isLinked ? Loc.GetString("station-teleporter-btn-unlink") : Loc.GetString("station-teleporter-btn-link");
            var linkBtn = new Button
            {
                Text = linkBtnText,
                HorizontalExpand = true,
                Margin = new Thickness(4, 0, 0, 0),
                Disabled = !tp.Powered
            };
            var capturedUid = tp.Uid;
            linkBtn.OnPressed += _ => OnTeleporterClick?.Invoke(capturedUid);
            buttons.AddChild(locateBtn);
            buttons.AddChild(linkBtn);
            inner.AddChild(nameLabel);
            inner.AddChild(buttons);
            row.AddChild(inner);
            _teleporterList.AddChild(row);
        }
    }
}
