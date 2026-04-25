// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Client.Pinpointer.UI;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;
using System.Numerics;

namespace Content.Client._Lua.StationTeleporter;

public sealed class StationTeleporterNavMapControl : NavMapControl
{
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    private readonly SharedTransformSystem _transformSystem;
    public List<(MapCoordinates A, MapCoordinates B)> LinkedTeleporterPairs = new();

    public StationTeleporterNavMapControl() : base()
    {
        IoCManager.InjectDependencies(this);
        _transformSystem = _entitySystemManager.GetEntitySystem<SharedTransformSystem>();
        PostWallDrawingAction += DrawTeleporterLinks;
        try
        {
            var topContainer = (BoxContainer) GetChild(0);
            var topPanel = (PanelContainer) topContainer.GetChild(0);
            var innerBox = (BoxContainer) topPanel.GetChild(0);
            var beaconsCheckBox = (CheckBox) innerBox.GetChild(1);
            beaconsCheckBox.Pressed = false;
        }
        catch
        {
        }
    }

    private void DrawTeleporterLinks(DrawingHandleScreen handle)
    {
        if (_xform == null) return;
        var offset = GetOffset();
        var invMatrix = _transformSystem.GetInvWorldMatrix(_xform);
        foreach (var (a, b) in LinkedTeleporterPairs)
        {
            if (a.MapId != b.MapId || a.MapId == MapId.Nullspace) continue;
            var posA = Vector2.Transform(a.Position, invMatrix) - offset;
            posA = ScalePosition(new Vector2(posA.X, -posA.Y));
            var posB = Vector2.Transform(b.Position, invMatrix) - offset;
            posB = ScalePosition(new Vector2(posB.X, -posB.Y));
            handle.DrawLine(posA, posB, Color.FromHex("#81ddeb"));
        }
    }
}
