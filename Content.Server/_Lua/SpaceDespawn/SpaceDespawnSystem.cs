using Content.Server._Lua.Despawn;
using Content.Server.NPC.HTN;
using Content.Shared.Mind.Components;
using Robust.Shared.Player;

namespace Content.Server._Lua.SpaceDespawn;

public sealed class SpaceDespawnSystem : EntitySystem
{
    public const float DespawnSeconds = 60f * 30f;
    private const float ScanIntervalSecond = 60f * 10f;
    private float _scan;
    private float _tick;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TransformComponent, MoveEvent>(OnMove);
        var xforms = EntityQueryEnumerator<TransformComponent>();
        while (xforms.MoveNext(out var uid, out var xform))
        {
            HandleEntity(uid, xform);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _tick += frameTime;
        var seconds = (int)MathF.Floor(_tick);
        if (seconds > 0)
        {
            _tick -= seconds;
            var timers = EntityQueryEnumerator<SpaceDespawnTimerComponent>();
            while (timers.MoveNext(out var uid, out var timer))
            {
                timer.RemainingSeconds -= seconds;
                if (timer.RemainingSeconds <= 0)
                {
                    QueueDel(uid);
                }
            }
        }
        _scan += frameTime;
        if (_scan < ScanIntervalSecond)
            return;
        _scan = 0f;

        var timerScan = EntityQueryEnumerator<SpaceDespawnTimerComponent, TransformComponent>();
        while (timerScan.MoveNext(out var uid, out _, out var xform))
        {
            if (ShouldIgnoreSpaceDespawn(uid, xform))
            {
                ClearSpaceTimer(uid);
            }
        }
        var mindScan = EntityQueryEnumerator<MindContainerComponent, TransformComponent>();
        while (mindScan.MoveNext(out var uid, out var mind, out var xform))
        {
            if (mind.HasMind) continue;
            if (!IsInOpenSpace(xform)) continue;
            if (ShouldIgnoreSpaceDespawn(uid, xform)) continue;
            if (HasComp<SpaceDespawnTimerComponent>(uid)) continue;
            StartOrRefreshTimer(uid);
        }
    }

    private void ClearSpaceTimer(EntityUid uid)
    {
        var hadTimer = TryComp<SpaceDespawnTimerComponent>(uid, out _);
        if (hadTimer) RemCompDeferred<SpaceDespawnTimerComponent>(uid);
    }

    private static bool IsInOpenSpace(TransformComponent xform)
    {
        return xform.GridUid == null;
    }

    private static bool IsGridOrMap(EntityUid uid, TransformComponent xform)
    {
        return uid == xform.GridUid || uid == xform.MapUid;
    }

    private bool IsPlayerControlled(EntityUid uid)
    {
        if (TryComp<ActorComponent>(uid, out _))
            return true;
        if (TryComp<MindContainerComponent>(uid, out var mind) && mind.HasMind)
            return true;
        return false;
    }

    private bool ShouldIgnoreSpaceDespawn(EntityUid uid, TransformComponent xform)
    {
        if (IsPlayerControlled(uid) || !IsInOpenSpace(xform)) return true;
        if (HasComp<AutoDespawnExemptComponent>(uid)) return true;
        if (HasComp<HTNComponent>(uid)) return true;
        return false;
    }

    private void StartOrRefreshTimer(EntityUid uid)
    {
        if (EnsureComp<SpaceDespawnTimerComponent>(uid, out var timer))
        {
            timer.RemainingSeconds = DespawnSeconds;
        }
    }

    private void HandleEntity(EntityUid uid, TransformComponent xform)
    {
        if (IsGridOrMap(uid, xform) || xform.MapUid == null)
            return;
        if (ShouldIgnoreSpaceDespawn(uid, xform))
        {
            ClearSpaceTimer(uid);
            return;
        }
        if (IsInOpenSpace(xform))
        { StartOrRefreshTimer(uid); }
        else
        { ClearSpaceTimer(uid); }
    }

    private void OnMove(EntityUid uid, TransformComponent xform, ref MoveEvent args)
    {
        if (IsGridOrMap(uid, xform) || xform.MapUid == null)
            return;
        if (!args.ParentChanged)
            return;
        var inSpace = IsInOpenSpace(xform);
        if (ShouldIgnoreSpaceDespawn(uid, xform))
        {
            ClearSpaceTimer(uid);
            return;
        }
        if (inSpace)
        { StartOrRefreshTimer(uid); }
        else
        { ClearSpaceTimer(uid); }
    }
}
