using Content.Shared._Lua.ShipProtection;
using Robust.Shared.Timing;

namespace Content.Server._Lua.ShipProtection;

public sealed class ShipProtectionSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    private readonly List<EntityUid> _toRemove = new();

    public override void Initialize()
    { base.Initialize(); }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<ShipProtectionComponent>();
        var currentTime = _timing.CurTime;
        _toRemove.Clear();
        while (query.MoveNext(out var uid, out var component))
        { if (currentTime >= component.ProtectionExpiresAt) { _toRemove.Add(uid); } }
        foreach (var uid in _toRemove)
        { RemComp<ShipProtectionComponent>(uid); }
    }

    public void ProtectEntity(EntityUid uid, TimeSpan duration)
    {
        var component = EnsureComp<ShipProtectionComponent>(uid);
        component.ProtectionExpiresAt = _timing.CurTime + duration;
        Dirty(uid, component);
    }

    public bool IsProtected(EntityUid uid)
    {
        if (!TryComp<ShipProtectionComponent>(uid, out var component)) return false;
        return _timing.CurTime < component.ProtectionExpiresAt;
    }

    public int GetRemainingMinutes(EntityUid uid)
    {
        if (!TryComp<ShipProtectionComponent>(uid, out var component)) return 0;
        var remaining = component.ProtectionExpiresAt - _timing.CurTime;
        return Math.Max(0, (int)Math.Ceiling(remaining.TotalMinutes));
    }
}

