using Content.Server.Preferences.Managers;
using Content.Shared._Arcane.ERP;
using Content.Shared.GameTicking;
using Content.Shared._Lua.ERP;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Robust.Shared.Player;

namespace Content.Server._Arcane.ERP;

public sealed class ErpStatusSystem : EntitySystem
{
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);

        SubscribeLocalEvent<HumanoidAppearanceComponent, PlayerAttachedEvent>(OnPlayerAttached);
    }

    private void OnSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (!HasComp<HumanoidAppearanceComponent>(args.Mob))
            return;

        EnsureComp<ArousalComponent>(args.Mob);
        SetStatus(args.Mob, args.Profile.ERPStatus);
    }

    private void OnPlayerAttached(Entity<HumanoidAppearanceComponent> ent, ref PlayerAttachedEvent args)
    {
        EnsureComp<ArousalComponent>(ent);

        if (HasComp<ErpStatusComponent>(ent))
            return;

        var profile = _prefs.GetPreferencesOrNull(args.Player.UserId)?.SelectedCharacter as HumanoidCharacterProfile;

        SetStatus(ent, profile?.ERPStatus ?? EnumERPStatus.NO);
    }

    private void SetStatus(EntityUid uid, EnumERPStatus status)
    {
        var comp = EnsureComp<ErpStatusComponent>(uid);
        if (comp.Status == status)
            return;

        comp.Status = status;
        Dirty(uid, comp);
    }
}
