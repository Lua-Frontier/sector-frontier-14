using Content.Shared._Lua.ERP;
using Content.Shared.Preferences;
using Robust.Shared.GameStates;

namespace Content.Shared.DetailExaminable;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DetailExaminableComponent : Component
{
    [DataField, AutoNetworkedField] // Erida-Edit | Removed: "required: true"
    public string Content = string.Empty;

    [DataField("ERPStatus", required: true)]
    [ViewVariables(VVAccess.ReadWrite)]
    public EnumERPStatus ERPStatus = EnumERPStatus.NO;

    public string GetERPStatusName()
    {
        switch (ERPStatus)
        {
            case EnumERPStatus.HALF:
                return Loc.GetString("humanoid-erp-status-half");
            case EnumERPStatus.FULL:
                return Loc.GetString("humanoid-erp-status-full");
            default:
                return Loc.GetString("humanoid-erp-status-no");
        }
    }
    // Erida-Start
    [DataField, AutoNetworkedField]
    public string CharacterContent { get; set; } = string.Empty;

    [DataField, AutoNetworkedField]
    public string OOCContent { get; set; } = string.Empty;

    [DataField, AutoNetworkedField]
    public string TagsContent { get; set; } = string.Empty;

    [DataField, AutoNetworkedField]
    public string LinksContent { get; set; } = string.Empty;

    [DataField, AutoNetworkedField]
    public string GreenContent { get; set; } = string.Empty;

    [DataField, AutoNetworkedField]
    public string YellowContent { get; set; } = string.Empty;

    [DataField, AutoNetworkedField]
    public string RedContent { get; set; } = string.Empty;

    [DataField, AutoNetworkedField]
    public string NSFWContent { get; set; } = string.Empty;

    [DataField, AutoNetworkedField]
    public string NSFWOOCContent { get; set; } = string.Empty;

    [DataField, AutoNetworkedField]
    public string NSFWLinksContent { get; set; } = string.Empty;

    [DataField, AutoNetworkedField]
    public string NSFWTagsContent { get; set; } = string.Empty;

    public void SetProfile(HumanoidCharacterProfile profile)
    {
        Content = profile.FlavorText;
        CharacterContent = profile.CharacterFlavorText;
        OOCContent = profile.OOCFlavorText;
        TagsContent = profile.TagsFlavorText;
        LinksContent = profile.LinksFlavorText;
        GreenContent = profile.GreenFlavorText;
        YellowContent = profile.YellowFlavorText;
        RedContent = profile.RedFlavorText;
        NSFWContent = profile.NSFWFlavorText;
        NSFWOOCContent = profile.NSFWOOCFlavorText;
        NSFWLinksContent = profile.NSFWLinksFlavorText;
        NSFWTagsContent = profile.NSFWTagsFlavorText;
        ERPStatus = profile.ERPStatus;
    }
    // Erida-End
}
