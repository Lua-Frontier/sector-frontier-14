using Content.Shared.Item.ItemToggle.Components;
using Content.Lua.Shared.HiddenBlades.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Lua.Shared.HiddenBlades.Systems;

public abstract class SharedHiddenBladesSystem : EntitySystem
{
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly ClothingSystem _clothing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HiddenBladesComponent, ItemToggledEvent>(OnToggled);
    }

    private void OnToggled(EntityUid uid, HiddenBladesComponent component, ref ItemToggledEvent args)
    {
        UpdateHiddenBladesState(uid, args.Activated, component);

        if (!TryComp<ClothingComponent>(uid, out var clothing))
            return;

        _clothing.SetEquippedPrefix(uid, args.Activated ? "activated" : null, clothing);

        if (args.User != null)
        {
            var message = args.Activated ? Loc.GetString(component.ActivatedPopUp!) : Loc.GetString(component.DeactivatedPopUp!);
            _popup.PopupClient(message, uid, args.User.Value);
        }
    }

    private void UpdateHiddenBladesState(EntityUid uid, bool activated, HiddenBladesComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var meta = MetaData(uid);
        var protoId = meta.EntityPrototype?.ID;

        if (protoId == null)
            return;

        if (!_prototypeManager.TryIndex<EntityPrototype>(protoId, out var prototype))
            return;

        string name = activated
            ? Loc.GetString(component.ActivatedName!)
            : prototype.Name;

        string description = activated
            ? Loc.GetString(component.ActivatedDescription!)
            : prototype.Description;

        _metaData.SetEntityName(uid, name);
        _metaData.SetEntityDescription(uid, description);
    }
}
