using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;

namespace Content.Client.Lobby.UI;

public sealed class CompanyLeaveConfirmationWindow : DefaultWindow
{
    public readonly Button ConfirmButton;
    public readonly Button CancelButton;

    public CompanyLeaveConfirmationWindow()
    {
        Title = Loc.GetString("character-setup-gui-company-leave-window-title");

        var textLabel = new RichTextLabel
        {
            HorizontalExpand = true,
            MaxWidth = 420,
        };
        textLabel.SetMessage(FormattedMessage.FromMarkupPermissive(Loc.GetString("character-setup-gui-company-leave-window-text")));

        var detailsLabel = new RichTextLabel
        {
            HorizontalExpand = true,
            MaxWidth = 420,
        };
        detailsLabel.SetMessage(FormattedMessage.FromMarkupPermissive($"[color=gray]{Loc.GetString("character-setup-gui-company-leave-window-details")}[/color]"));

        Contents.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 10,
            Children =
            {
                textLabel,
                detailsLabel,
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    Children =
                    {
                        (CancelButton = new Button
                        {
                            Text = Loc.GetString("character-setup-gui-company-leave-window-cancel"),
                            MinSize = new Vector2(140, 0),
                        }),
                        new Control
                        {
                            HorizontalExpand = true,
                        },
                        (ConfirmButton = new Button
                        {
                            Text = Loc.GetString("character-setup-gui-company-leave-window-confirm"),
                            StyleClasses = { "Caution" },
                            MinSize = new Vector2(180, 0),
                        }),
                    }
                }
            }
        });
    }
}
