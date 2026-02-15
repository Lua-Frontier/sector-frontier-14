// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaCorp
// See AGPLv3.txt for details.
using System.Globalization;
using System.Linq;
using Content.Client.Eui;
using Content.Shared._Lua.Administration.ChunkMonitor;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.Console;
using Robust.Shared.Map.Components;

namespace Content.Client._Lua.Administration.UI.ChunkMonitor;

[UsedImplicitly]
public sealed class ChunkMonitorEui : BaseEui
{
    private const float ChunkSize = 128f;
    [Dependency] private readonly IClientConsoleHost _console = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    private ChunkMonitorWindow? _window;
    private bool _requestedInitial;
    private ChunkMonitorChunkInfo? _selectedChunk;

    public override void Opened()
    {
        base.Opened();
        _window = new ChunkMonitorWindow();
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.MapSelected += map =>
        {
            _selectedChunk = null;
            SendMessage(new ChunkMonitorEuiMsg.RequestMapData(map));
        };
        _window.RefreshPressed += map =>
        {
            _selectedChunk = null;
            SendMessage(new ChunkMonitorEuiMsg.RequestMapData(map));
        };
        _window.ChunkSelected += chunk =>
        {
            _selectedChunk = chunk;
            _window.SetDetails(chunk);
        };
        _window.DeleteSelectedPressed += (map, chunk) =>
        {
            SendMessage(new ChunkMonitorEuiMsg.DeleteChunks(map, [chunk.Coordinates]));
        };
        _window.TeleportSelectedPressed += (map, chunk) =>
        {
            if (!_entMan.TryGetEntity(map, out EntityUid? mapUid) || mapUid is not { } mapEnt)
                return;
            if (!_entMan.TryGetComponent(mapEnt, out MapComponent? mapComp))
                return;
            var mapId = (int) mapComp.MapId;
            var x = chunk.Coordinates.X * ChunkSize + ChunkSize / 2f;
            var y = chunk.Coordinates.Y * ChunkSize + ChunkSize / 2f;
            var xs = x.ToString("0.##", CultureInfo.InvariantCulture);
            var ys = y.ToString("0.##", CultureInfo.InvariantCulture);
            _console.ExecuteCommand($"tp {xs} {ys} {mapId}");
        };
        _window.PurgeSelectedPressed += (map, chunk) =>
        {
            SendMessage(new ChunkMonitorEuiMsg.PurgeChunks(map, [chunk.Coordinates]));
        };
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();
        _window?.Dispose();
        _window = null;
    }

    public override void HandleState(EuiStateBase state)
    {
        var s = (ChunkMonitorEuiState) state;
        if (_window == null)
            return;
        _window.SetMaps(s.Maps, s.SelectedMap);
        _window.SetCounts(s.LoadedCount, s.UnloadedCount, s.DeletedCount);
        _window.SetChunks(s.Chunks);
        if (_selectedChunk is { } selected)
        {
            var found = s.Chunks.FirstOrDefault(c => c.Coordinates == selected.Coordinates);
            if (found != default)
            {
                _selectedChunk = found;
                _window.SetDetails(found);
                _window.SetSelectedChunk(found.Coordinates);
            }
            else
            {
                _selectedChunk = null;
                _window.ClearDetails();
                _window.SetSelectedChunk(null);
            }
        }
        if (!_requestedInitial)
        {
            _requestedInitial = true;
            if (s.SelectedMap.IsValid())
                SendMessage(new ChunkMonitorEuiMsg.RequestMapData(s.SelectedMap));
        }
    }
}
