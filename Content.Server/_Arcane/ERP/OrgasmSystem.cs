using Content.Server._Arcane.ErpPanel; // Lua
using Content.Shared._Arcane.ERP;
using Content.Shared.Dataset;
using Content.Shared.Humanoid;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Enums;
using Robust.Shared.Player; // Lua
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Arcane.ERP;

public sealed class OrgasmSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ErpPanelSystem _erpPanel = default!; // Lua
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    private static readonly EntProtoId HeartsProto = "EffectHearts";
    private static readonly ProtoId<LocalizedDatasetPrototype> OrgasmMessagesDataset = "OrgasmMessages";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ArousalComponent, ArousalOrgasmEvent>(OnOrgasm);
    }

    private void OnOrgasm(Entity<ArousalComponent> ent, ref ArousalOrgasmEvent args)
    {
        if (!TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
            return;

        DoOrgasmEffects(ent, args.PrivateFilter, humanoid); // Lua
    }

    public void DoOrgasmEffects(EntityUid uid, Filter? privateFilter = null, HumanoidAppearanceComponent? humanoid = null) // Lua
    {
        Resolve(uid, ref humanoid, false);

        Spawn(HeartsProto, _transform.GetMapCoordinates(uid));
        PlayOrgasmSound(uid, humanoid?.Gender ?? Gender.Male); // Lua

        if (_prototype.TryIndex(OrgasmMessagesDataset, out var dataset))
            _erpPanel.SendEmote(uid, Loc.GetString(_random.Pick(dataset.Values)), privateFilter, false); // Lua

        _popup.PopupEntity(Loc.GetString("orgasm-popup-self"), uid, uid, PopupType.MediumCaution);

        var weakness = EnsureComp<OrgasmWeaknessComponent>(uid);
        weakness.ExpiresAt = _timing.CurTime + weakness.WeaknessDuration;
        Dirty(uid, weakness);
    }

    private void PlayOrgasmSound(EntityUid uid, Gender gender)
    {
        var collection = ErpAudio.GetOrgasmSounds(gender); // Lua

        var audioParams = new AudioParams
        {
            Variation = 0.1f,
            // Lua start: linear fade, silent past 2 tiles
            MaxDistance = 2f,
            ReferenceDistance = 0.5f,
            // Lua end
            Volume = 3f,
        };

        _audio.PlayPvs(new SoundCollectionSpecifier(collection), uid, audioParams);
    }
}
