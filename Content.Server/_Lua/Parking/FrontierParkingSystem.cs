// LuaWorld/LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld/LuaCorp
// See AGPLv3.txt for details.

using Content.Server._NF.Bank;
using Content.Server.Chat.Managers;
using Content.Server.Database;
using Content.Server.Fax;
using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Server.Preferences.Managers;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Systems;
using Content.Shared._Lua.Frontier.Parking;
using Content.Shared._NF.Bank.BUI;
using Content.Shared._NF.Bank.Components;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared.Chat;
using Content.Shared.Fax.Components;
using Content.Shared.Lua.CLVar;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Preferences;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Threading;
using System.Threading.Tasks;
using static Content.Server._NF.Shipyard.Systems.ShipyardSystem;

namespace Content.Server._Lua.Frontier.Parking;
public sealed class FrontierParkingSystem : EntitySystem
{
    private const PopupType PopupInfoType = PopupType.SmallCaution;
    private const PopupType PopupWarningType = PopupType.SmallCaution;
    private const PopupType PopupFineType = PopupType.SmallCaution;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly FaxSystem _fax = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IResourceManager _res = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IServerPreferencesManager _prefsManager = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    private bool _enabled;
    private TimeSpan _nextScan;
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan FineInterval = TimeSpan.FromMinutes(10);
    private const int FineAmount = 10_000;
    private readonly Dictionary<EntityUid, ParkingState> _tracked = new();
    private readonly Dictionary<EntityUid, Task> _pendingOfflineFines = new();

    public EntityUid? FrontierStation => GetFrontierStation();

    public bool TryGetTracked(EntityUid shuttleUid, out ParkingPublicState state)
    {
        if (_tracked.TryGetValue(shuttleUid, out var s))
        {
            state = s.ToPublicState();
            return true;
        }

        state = default;
        return false;
    }

    public IEnumerable<(EntityUid Shuttle, ShuttleDeedComponent Deed, ParkingPublicState State)> EnumerateTracked()
    {
        foreach (var (uid, state) in _tracked)
        {
            if (!TryComp<ShuttleDeedComponent>(uid, out var deed))
                continue;

            yield return (uid, deed, state.ToPublicState());
        }
    }

    public bool ResetTimer(EntityUid shuttleUid)
    {
        if (!_tracked.TryGetValue(shuttleUid, out var s))
            return false;

        s.CycleStart = _timing.CurTime;
        s.SentFiveMinutes = false;
        s.SentFortyFiveSeconds = false;
        s.ExtraMinutes = 0;
        s.HasViolation = false;
        s.NeedsDisposal = false;
        return true;
    }

    public bool AddTenMinutes(EntityUid shuttleUid)
    {
        if (!_tracked.TryGetValue(shuttleUid, out var s))
            return false;

        s.ExtraMinutes = Math.Min(50, s.ExtraMinutes + 10);
        return true;
    }

    private TimeSpan GetAllowedInterval(ParkingState state)
    {
        var extra = TimeSpan.FromMinutes(Math.Clamp(state.ExtraMinutes, 0, 50));
        return FineInterval + extra;
    }

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_cfg, CLVars.FrontierParkingEnabled, v => _enabled = v, true);
        _nextScan = _timing.CurTime;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_enabled) return;
        if (_timing.CurTime < _nextScan) return;
        _nextScan = _timing.CurTime + ScanInterval;
        var frontierStation = GetFrontierStation();
        if (frontierStation == null) return;
        var exclusions = GetFrontierExclusions(frontierStation.Value);
        if (exclusions.Count == 0) return;
        var inZone = new HashSet<EntityUid>();
        var shuttleQuery = EntityQueryEnumerator<ShuttleDeedComponent, ShuttleComponent, TransformComponent>();
        while (shuttleQuery.MoveNext(out var shuttleUid, out var deed, out _, out var shuttleXform))
        {
            if (shuttleXform.MapID == MapId.Nullspace) continue;
            if (!IsInsideAnyExclusion(shuttleXform, exclusions)) continue;
            inZone.Add(shuttleUid);
            if (!_tracked.TryGetValue(shuttleUid, out var state))
            {
                state = new ParkingState(_timing.CurTime);
                _tracked[shuttleUid] = state;
                SendOwnerNotice(shuttleUid, deed, Loc.GetString("frontier-parking-popup-enter"), PopupInfoType);
            }
            ProcessTimers(frontierStation.Value, shuttleUid, deed, state);
        }
        foreach (var (uid, _) in _tracked)
        { if (!inZone.Contains(uid)) _toRemove.Add(uid); }
        foreach (var uid in _toRemove)
        {
            if (TryComp<ShuttleDeedComponent>(uid, out var deed)) SendOwnerNotice(uid, deed, Loc.GetString("frontier-parking-popup-leave"), PopupInfoType);
            _tracked.Remove(uid);
        }
        _toRemove.Clear();
    }
    private readonly List<EntityUid> _toRemove = new();

    private EntityUid? GetFrontierStation()
    {
        var query = EntityQueryEnumerator<FrontierParkingComponent>();
        while (query.MoveNext(out var uid, out _))
        { return uid; }
        return null;
    }

    private List<(EntityUid Uid, TransformComponent Xform, float Range)> GetFrontierExclusions(EntityUid frontierStation)
    {
        var list = new List<(EntityUid, TransformComponent, float)>();
        var query = EntityQueryEnumerator<FTLExclusionComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var excl, out var xform))
        {
            if (!excl.Enabled) continue;
            var ownerStation = _station.GetOwningStation(uid, xform);
            if (ownerStation != frontierStation) continue;
            list.Add((uid, xform, excl.Range));
        }
        return list;
    }

    private bool IsInsideAnyExclusion(TransformComponent shuttleXform, List<(EntityUid Uid, TransformComponent Xform, float Range)> exclusions)
    {
        var shuttlePos = _xform.GetWorldPosition(shuttleXform);
        var shuttleMap = shuttleXform.MapID;
        foreach (var (_, exclXform, range) in exclusions)
        {
            if (exclXform.MapID != shuttleMap) continue;
            var exclPos = _xform.GetWorldPosition(exclXform);
            if ((shuttlePos - exclPos).Length() <= range) return true;
        }
        return false;
    }

    private void ProcessTimers(EntityUid frontierStation, EntityUid shuttleUid, ShuttleDeedComponent deed, ParkingState state)
    {
        var elapsed = _timing.CurTime - state.CycleStart;
        var allowed = GetAllowedInterval(state);
        var warn5 = allowed - TimeSpan.FromMinutes(5);
        var warn45 = allowed - TimeSpan.FromSeconds(45);

        if (!state.SentFiveMinutes && warn5 > TimeSpan.Zero && elapsed >= warn5)
        {
            state.SentFiveMinutes = true;
            SendOwnerNotice(shuttleUid, deed, Loc.GetString("frontier-parking-popup-remaining-5m"), PopupWarningType);
        }
        if (!state.SentFortyFiveSeconds && warn45 > TimeSpan.Zero && elapsed >= warn45)
        {
            state.SentFortyFiveSeconds = true;
            SendOwnerNotice(shuttleUid, deed, Loc.GetString("frontier-parking-popup-remaining-45s"), PopupWarningType);
        }
        if (elapsed < allowed) return;
        var appliedOrQueued = TryApplyFine(frontierStation, shuttleUid, deed, state);
        if (appliedOrQueued && IsOwnerOnline(shuttleUid))
        {
            SendOwnerNotice(shuttleUid, deed, Loc.GetString("frontier-parking-popup-fined-extended"), PopupFineType);
        }
        state.Reset(_timing.CurTime);
    }

    private bool TryApplyFine(EntityUid frontierStation, EntityUid shuttleUid, ShuttleDeedComponent deed, ParkingState state)
    {
        if (!TryGetOwnerUserId(shuttleUid, deed, out var ownerUserId, out var session)) return false;
        if (session != null && session.AttachedEntity is { } ent)
        {
            if (!_bank.TryBankWithdraw(ent, FineAmount))
            {
                state.NeedsDisposal = true;
                state.HasViolation = true;
                return false;
            }
            _bank.TrySectorDeposit(SectorBankAccount.Frontier, FineAmount, LedgerEntryType.StationDepositFines);
            var notifyText = Loc.GetString("frontier-parking-notification-fined", ("amount", FineAmount));
            _chat.ChatMessageToOne(ChatChannel.Notifications, notifyText, notifyText, EntityUid.Invalid, false, session.Channel);
            TrySendFaxNotice(frontierStation, shuttleUid, deed);
            state.NeedsDisposal = false;
            state.HasViolation = true;
            return true;
        }
        if (_pendingOfflineFines.TryGetValue(shuttleUid, out var existing) && !existing.IsCompleted) return true;
        _pendingOfflineFines[shuttleUid] = ApplyOfflineFine(frontierStation, shuttleUid, deed, ownerUserId);
        return true;
    }

    private bool TryGetOwnerUserId(EntityUid shuttleUid, ShuttleDeedComponent deed, out NetUserId userId, out ICommonSession? session)
    {
        session = null;
        userId = default;
        if (TryComp<ShipOwnershipComponent>(shuttleUid, out var ownership) && ownership.OwnerUserId != default && _playerManager.TryGetSessionById(ownership.OwnerUserId, out var owningSession))
        {
            userId = ownership.OwnerUserId;
            session = owningSession;
            return true;
        }
        if (TryComp<ShipOwnershipComponent>(shuttleUid, out ownership) && ownership.OwnerUserId != default)
        {
            userId = ownership.OwnerUserId;
            _playerManager.TryGetSessionById(userId, out session);
            return true;
        }
        var ownerName = deed.ShuttleOwner;
        if (!string.IsNullOrWhiteSpace(ownerName))
        {
            foreach (var s in _playerManager.Sessions)
            {
                if (s.AttachedEntity is not { } ent) continue;
                if (Name(ent).Equals(ownerName, StringComparison.OrdinalIgnoreCase))
                {
                    userId = s.UserId;
                    session = s;
                    return true;
                }
            }
        }
        return false;
    }

    private bool IsOwnerOnline(EntityUid shuttleUid)
    { return TryComp<ShipOwnershipComponent>(shuttleUid, out var ownership) && ownership.OwnerUserId != default && _playerManager.TryGetSessionById(ownership.OwnerUserId, out _); }

    private async Task ApplyOfflineFine(EntityUid frontierStation, EntityUid shuttleUid, ShuttleDeedComponent deed, NetUserId ownerUserId)
    {
        try
        {
            PlayerPreferences? prefs = null;
            if (!_prefsManager.TryGetCachedPreferences(ownerUserId, out prefs)) prefs = await _db.GetPlayerPreferencesAsync(ownerUserId, CancellationToken.None);
            if (prefs == null || prefs.SelectedCharacter is not HumanoidCharacterProfile profile) return;
            var withdrew = await _bank.TryBankWithdrawOffline(ownerUserId, prefs, profile, FineAmount);
            if (!withdrew)
            {
                if (_tracked.TryGetValue(shuttleUid, out var st))
                {
                    st.NeedsDisposal = true;
                    st.HasViolation = true;
                }
                return;
            }
            _bank.TrySectorDeposit(SectorBankAccount.Frontier, FineAmount, LedgerEntryType.StationDepositFines);
            TrySendFaxNotice(frontierStation, shuttleUid, deed);
            if (_tracked.TryGetValue(shuttleUid, out var st2))
            {
                st2.NeedsDisposal = false;
                st2.HasViolation = true;
            }
        }
        finally { _pendingOfflineFines.Remove(shuttleUid); }
    }

    private void SendOwnerNotice(EntityUid shuttleUid, ShuttleDeedComponent deed, string message, PopupType type)
    {
        if (TryComp<ShipOwnershipComponent>(shuttleUid, out var ownership) && ownership.OwnerUserId != default && _playerManager.TryGetSessionById(ownership.OwnerUserId, out var session) && session.AttachedEntity is { } ent)
        {
            _popup.PopupEntity(message, ent, type);
            return;
        }
        if (deed.DeedHolder == null || !TryComp<ActorComponent>(deed.DeedHolder.Value, out var actor)) return;
        if (_playerManager.TryGetSessionById(actor.PlayerSession.UserId, out var fallbackSession) && fallbackSession.AttachedEntity is { } fallbackEnt)
        { _popup.PopupEntity(message, fallbackEnt, type); }
    }

    private void TrySendFaxNotice(EntityUid frontierStation, EntityUid shuttleUid, ShuttleDeedComponent deed)
    {
        var faxUid = FindFaxOnGrid(shuttleUid);
        if (faxUid == null) return;
        var content = BuildFaxContent(frontierStation, deed);
        const string paperPrototype = "PaperPrintedCommand";
        const string stampState = "paper_stamp-centcom";
        var stampedBy = new List<StampDisplayInfo>
        {
            new StampDisplayInfo
            {
                StampedName = "stamp-component-stamped-name-centcom",
                StampedColor = Color.FromHex("#006600"),
                Type = StampType.RubberStamp,
                Reapply = false
            }
        };
        var printout = new FaxPrintout(content, Loc.GetString("frontier-parking-fax-title"), null, paperPrototype, stampState, stampedBy, locked: true);
        _fax.Receive(faxUid.Value, printout, fromAddress: null);
    }

    private EntityUid? FindFaxOnGrid(EntityUid gridUid)
    {
        if (!TryComp<TransformComponent>(gridUid, out var xform)) return null;
        var stack = new Stack<EntityUid>();
        var enumerator = xform.ChildEnumerator;
        while (enumerator.MoveNext(out var child)) stack.Push(child);
        while (stack.Count > 0)
        {
            var uid = stack.Pop();
            if (HasComp<FaxMachineComponent>(uid)) return uid;
            if (TryComp<TransformComponent>(uid, out var childXform))
            {
                var childEnum = childXform.ChildEnumerator;
                while (childEnum.MoveNext(out var grandChild)) stack.Push(grandChild);
            }
        }
        return null;
    }

    private string BuildFaxContent(EntityUid frontierStation, ShuttleDeedComponent deed)
    {
        var text = _res.ContentFileReadText(new ResPath("/Paperwork/Frontier/2.xml")).ReadToEnd();
        text = text.Replace("DOCUMENT NAME", "НАРУШЕНИЕ СТОЯНКИ НА ФРОНТИРЕ");
        text = text.Replace("{{HOUR.MINUTE.SECOND}}", _ticker.RoundDuration().ToString("hh\\:mm\\:ss"));
        text = text.Replace("{{DAY.MONTH.YEAR}}", DateTime.UtcNow.AddHours(3).ToString("dd.MM") + ".2709");
        text = text.Replace("STATION XX-00", Name(frontierStation));
        text = text.Replace("Я, (ФИО), в должности (полное наименование должности), фиксирую нарушение стоянки на Фронтире",
            "ИИ Автономного Контроля Трафика, фиксирует нарушение стоянки на Станции Фронтир");
        text = text.Replace("Причина: Злоупотребление временем стыковки",
            "Причина: Злоупотребление временем стоянки на территории станции Фронтир");
        var fullName = GetFullName(deed);
        text = text.Replace("Шаттл: (полное наименование шаттла включая номер)",
            $"Шаттл: {fullName}");
        text = text.Replace("(Оплатить на стойке Представителя станции)",
            "Автоматически списано с нарушителя в пользу станции Фронтир");
        return text;
    }

    private sealed class ParkingState
    {
        public TimeSpan CycleStart;
        public bool SentFiveMinutes;
        public bool SentFortyFiveSeconds;
        public int ExtraMinutes;
        public bool HasViolation;
        public bool NeedsDisposal;
        public ParkingState(TimeSpan start)
        { CycleStart = start; }
        public void Reset(TimeSpan now)
        {
            CycleStart = now;
            SentFiveMinutes = false;
            SentFortyFiveSeconds = false;
            ExtraMinutes = 0;
        }

        public ParkingPublicState ToPublicState()
        {
            return new ParkingPublicState(
                CycleStart,
                ExtraMinutes,
                HasViolation,
                NeedsDisposal);
        }
    }
}

public readonly record struct ParkingPublicState(
    TimeSpan CycleStart,
    int ExtraMinutes,
    bool HasViolation,
    bool NeedsDisposal);


