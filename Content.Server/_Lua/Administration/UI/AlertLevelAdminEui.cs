// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using System.Linq;
using Content.Server._Lua.Sectors;
using Content.Server._NF.SectorServices;
using Content.Server.Administration.Managers;
using Content.Server.AlertLevel;
using Content.Server.EUI;
using Content.Shared._Lua.Administration.AlertLevel;
using Content.Shared.Administration;
using Content.Shared.Eui;
using Robust.Shared.Map;

namespace Content.Server._Lua.Administration.UI;

public sealed class AlertLevelAdminEui : BaseEui
{
    [Dependency] private readonly IAdminManager _admins = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;

    private readonly AlertLevelSystem _alertLevel;
    private readonly SectorServiceSystem _sectorService;
    private readonly SectorSystem _sectors;
    private string _statusText = string.Empty;

    public AlertLevelAdminEui()
    {
        IoCManager.InjectDependencies(this);
        _alertLevel = _entMan.System<AlertLevelSystem>();
        _sectorService = _entMan.System<SectorServiceSystem>();
        _sectors = _entMan.System<SectorSystem>();
    }

    public override void Opened()
    {
        base.Opened();

        if (!EnsureAuthorized())
            return;

        _statusText = Loc.GetString("admin-alert-level-status-idle");
        StateDirty();
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (!EnsureAuthorized())
            return;

        switch (msg)
        {
            case AlertLevelAdminEuiMsg.RefreshRequest:
                StateDirty();
                break;

            case AlertLevelAdminEuiMsg.SetSectorRequest setSector:
                if (!TrySetSector(setSector.SectorId, setSector.Level, setSector.Locked, out var sectorError))
                    _statusText = sectorError ?? Loc.GetString("admin-alert-level-status-failed");
                else
                    _statusText = Loc.GetString("admin-alert-level-status-sector-set",
                        ("sector", GetSectorName(setSector.SectorId)),
                        ("level", GetLevelName(setSector.Level)));
                StateDirty();
                break;

            case AlertLevelAdminEuiMsg.SetGlobalRequest setGlobal:
                if (!TrySetGlobal(setGlobal.Level, setGlobal.Locked, out var globalError))
                    _statusText = globalError ?? Loc.GetString("admin-alert-level-status-failed");
                else
                    _statusText = Loc.GetString("admin-alert-level-status-global-set",
                        ("level", GetLevelName(setGlobal.Level)));
                StateDirty();
                break;
        }
    }

    public override EuiStateBase GetNewState()
    {
        return new AlertLevelAdminEuiState
        {
            Sectors = BuildSectors(),
            Levels = BuildLevels(),
            StatusText = _statusText,
        };
    }

    private List<AlertLevelAdminSectorInfo> BuildSectors()
    {
        var list = new List<AlertLevelAdminSectorInfo>();
        foreach (var (mapId, service) in _sectorService.GetServicesWithMaps().OrderBy(s => _sectors.GetSectorDisplayName(s.MapId)))
        {
            if (!_sectors.TryGetSectorId(mapId, out var sectorId))
                sectorId = mapId.ToString();

            if (!_entMan.TryGetComponent<AlertLevelComponent>(service, out var alert) ||
                alert.AlertLevels == null)
                continue;

            var color = Color.White;
            if (alert.AlertLevels.Levels.TryGetValue(alert.CurrentLevel, out var details))
                color = details.Color;

            list.Add(new AlertLevelAdminSectorInfo
            {
                SectorId = sectorId,
                SectorName = _sectors.GetSectorDisplayName(mapId),
                CurrentLevel = alert.CurrentLevel,
                Locked = alert.IsLevelLocked,
                LevelColorHex = color.ToHex(),
            });
        }

        return list;
    }

    private List<AlertLevelAdminLevelInfo> BuildLevels()
    {
        foreach (var service in _sectorService.GetServiceEntities())
        {
            if (!_entMan.TryGetComponent<AlertLevelComponent>(service, out var alert) ||
                alert.AlertLevels == null)
                continue;

            return alert.AlertLevels.Levels.Select(pair =>
            {
                var name = pair.Key;
                if (Loc.TryGetString($"alert-level-{pair.Key}", out var locName))
                    name = locName;

                return new AlertLevelAdminLevelInfo
                {
                    Id = pair.Key,
                    Name = name,
                    ColorHex = pair.Value.Color.ToHex(),
                    Selectable = true,
                };
            }).ToList();
        }

        return new List<AlertLevelAdminLevelInfo>();
    }

    private bool TrySetSector(string sectorId, string level, bool locked, out string? error)
    {
        error = null;
        if (!_sectors.TryGetMapId(sectorId, out var mapId) || mapId == MapId.Nullspace)
        {
            error = Loc.GetString("admin-alert-level-status-unknown-sector");
            return false;
        }

        if (!_sectorService.TryGetServiceEntity(mapId, out var service))
        {
            error = Loc.GetString("admin-alert-level-status-unknown-sector");
            return false;
        }

        if (!_entMan.TryGetComponent<AlertLevelComponent>(service, out var alert) ||
            alert.AlertLevels == null ||
            !alert.AlertLevels.Levels.ContainsKey(level))
        {
            error = Loc.GetString("admin-alert-level-status-invalid-level");
            return false;
        }

        if (!_alertLevel.SetLevel(service, level, true, true, true, locked, mapOverride: mapId))
        {
            error = Loc.GetString("admin-alert-level-status-failed");
            return false;
        }

        return true;
    }

    private bool TrySetGlobal(string level, bool locked, out string? error)
    {
        error = null;
        var levels = BuildLevels();
        if (levels.All(l => l.Id != level))
        {
            error = Loc.GetString("admin-alert-level-status-invalid-level");
            return false;
        }

        _alertLevel.SetLevelGlobal(level, true, true, true, locked);
        return true;
    }

    private string GetSectorName(string sectorId)
    {
        if (_sectors.TryGetMapId(sectorId, out var mapId))
            return _sectors.GetSectorDisplayName(mapId);
        return sectorId;
    }

    private static string GetLevelName(string level)
    {
        return Loc.TryGetString($"alert-level-{level}", out var name) ? name : level;
    }

    private bool EnsureAuthorized()
    {
        if (_admins.HasAdminFlag(Player, AdminFlags.Fun))
            return true;

        Close();
        return false;
    }
}
