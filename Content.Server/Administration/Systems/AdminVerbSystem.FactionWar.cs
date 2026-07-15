using Content.Shared._Mono.Company;
using Content.Server._Lua.Company;
using Content.Server._Lua.Company.Components;
using Content.Shared.Database;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.Administration.Systems;

public sealed partial class AdminVerbSystem
{
    [Dependency] private readonly FactionCaptureSystem _factionCapture = default!;
    [Dependency] private readonly FactionOwnedStationSystem _factionOwnedStations = default!;

    private void AddFactionWarVerbs(GetVerbsEvent<Verb> args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor) || actor == null)
            return;

        var player = actor.PlayerSession;
        if (!_adminManager.IsAdmin(player))
            return;

        if (!TryComp<FactionOwnedStationComponent>(args.Target, out var ownedStation))
            return;

        args.Verbs.Add(new Verb
        {
            Text = "Set faction station owner",
            Category = VerbCategory.Debug,
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/outfit.svg.192dpi.png")),
            Act = () =>
            {
                _quickDialog.OpenDialog<string>(player, "Set faction station owner", "Company id or None", companyId =>
                {
                    var normalized = NormalizeCompanyInput(companyId);
                    if (normalized != null && !_prototypeManager.HasIndex<CompanyPrototype>(normalized))
                    {
                        _popup.PopupEntity("Unknown company id.", args.User, args.User);
                        return;
                    }

                    _factionOwnedStations.SetOwner(args.Target, normalized, ownedStation);
                    _popup.PopupEntity($"Station owner set to {normalized ?? "None"}.", args.User, args.User);
                });
            },
            Impact = LogImpact.Medium,
            ConfirmationPopup = true,
        });

        if (_factionOwnedStations.TryGetOriginalOwner(args.Target, out var originalOwner))
        {
            args.Verbs.Add(new Verb
            {
                Text = "Restore original faction owner",
                Category = VerbCategory.Debug,
                Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/rejuvenate.svg.192dpi.png")),
                Act = () =>
                {
                    _factionOwnedStations.SetOwner(args.Target, originalOwner, ownedStation);
                    _popup.PopupEntity($"Station owner restored to {originalOwner}.", args.User, args.User);
                },
                Impact = LogImpact.Medium,
                ConfirmationPopup = true,
            });
        }

        if (HasComp<FactionCaptureComponent>(args.Target))
        {
            args.Verbs.Add(new Verb
            {
                Text = "Reset faction capture progress",
                Category = VerbCategory.Debug,
                Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/rejuvenate.svg.192dpi.png")),
                Act = () =>
                {
                    _factionCapture.ResetCaptureState(args.Target);
                    _popup.PopupEntity("Capture progress reset.", args.User, args.User);
                },
                Impact = LogImpact.Medium,
            });
        }
    }

    private static string? NormalizeCompanyInput(string? companyId)
    {
        if (string.IsNullOrWhiteSpace(companyId))
            return null;

        var trimmed = companyId.Trim();
        return string.Equals(trimmed, "None", StringComparison.OrdinalIgnoreCase)
            ? null
            : trimmed;
    }
}
