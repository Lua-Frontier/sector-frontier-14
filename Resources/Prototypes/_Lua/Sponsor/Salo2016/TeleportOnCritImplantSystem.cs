using Content.Shared.Implants.Components;
using Content.Shared.Damage.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Implants.Systems;

public sealed class TeleportOnCritImplantSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IMapManager _map = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<TeleportOnCritImplantComponent, CriticalConditionChangedEvent>(OnCrit);
    }

    private void OnCrit(EntityUid uid, TeleportOnCritImplantComponent component, CriticalConditionChangedEvent args)
    {
        if (!args.IsCritical || component.TeleportTarget == null || component.OrganPrototype == null)
            return;

        var owner = Transform(uid).Coordinates;

        // Телепортируем владельца
        _entMan.GetEntity(uid).Transform.Coordinates = Transform(component.TeleportTarget.Value).Coordinates;

        // Создаём органы на месте старого тела
        for (int i = 0; i < component.OrganCount; i++)
        {
            var organ = _entMan.SpawnEntity(component.OrganPrototype.Value, owner);
            // Можно добавить рандомизацию позиции, состояния и т.д.
        }
    }
}
