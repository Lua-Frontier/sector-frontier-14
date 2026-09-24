using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;

namespace Content.Lua.UIKit.Options;

public interface IAccessibilityAnnouncementOptionsConfigurator
{
    void Configure(
        Control controlRow,
        IConfigurationManager cfg,
        Control announcementMaxVisibleSlider,
        Label announcementPresetOverridesLabel,
        BoxContainer announcementPresetOverridesContainer,
        Button announcementLayoutEditorButton,
        Button announcementLayoutResetButton,
        Control announcementLayoutSummaryLabel);
}
