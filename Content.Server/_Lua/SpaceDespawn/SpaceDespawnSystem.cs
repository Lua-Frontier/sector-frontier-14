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
        var initialCount = 0;
        while (xforms.MoveNext(out var uid, out var xform))
        {
            HandleEntity(uid, xform);
            initialCount++;
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
            var processed = 0;
            var deleted = 0;
            var timers = EntityQueryEnumerator<SpaceDespawnTimerComponent>();
            while (timers.MoveNext(out var uid, out var timer))
            {
                timer.RemainingSeconds -= seconds;
                processed++;
                if (timer.RemainingSeconds <= 0)
                {
                    deleted++;
                    QueueDel(uid);
                }
            }
            if (processed > 0 || deleted > 0) ;
        }
        _scan += frameTime;
        if (_scan < ScanIntervalSecond)
            return;
        _scan = 0f;

        var cleared = 0;
        var started = 0;
        var timerScan = EntityQueryEnumerator<SpaceDespawnTimerComponent, TransformComponent>();
        while (timerScan.MoveNext(out var uid, out _, out var xform))
        {
            if (IsPlayerControlled(uid) || !IsInOpenSpace(xform))
            {
                ClearSpaceTimer(uid);
                cleared++;
            }
        }
        var mindScan = EntityQueryEnumerator<MindContainerComponent, TransformComponent>();
        while (mindScan.MoveNext(out var uid, out var mind, out var xform))
        {
            if (mind.HasMind) continue;
            if (!IsInOpenSpace(xform)) continue;
            if (IsPlayerControlled(uid)) continue;
            if (HasComp<SpaceDespawnTimerComponent>(uid)) continue;
            StartOrRefreshTimer(uid);
            started++;
        }
        if (cleared > 0 || started > 0) ;
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

    private void StartOrRefreshTimer(EntityUid uid)
    {
        var hadTimer = TryComp<SpaceDespawnTimerComponent>(uid, out var existing);
        if (EnsureComp<SpaceDespawnTimerComponent>(uid, out var timer))
        {
            var prev = hadTimer ? existing!.RemainingSeconds : -1f;
            timer.RemainingSeconds = DespawnSeconds;
        }
    }

    private void HandleEntity(EntityUid uid, TransformComponent xform)
    {
        if (IsGridOrMap(uid, xform) || xform.MapUid == null)
            return;
        if (IsPlayerControlled(uid))
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
        if (IsPlayerControlled(uid))
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
