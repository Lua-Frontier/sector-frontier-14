using System.Collections.Generic;
using System.Linq;
using Content.Client._RMC14.Announce;
using Content.Client.Options.UI;
using Content.Lua.Shared.Announce;
using Content.Lua.Shared.CCVar;
using Content.Lua.UIKit.Options;
using Content.Shared._RMC14.Announce;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;

namespace Content.Lua.Client.Options;

public sealed class AccessibilityAnnouncementOptionsConfigurator : IAccessibilityAnnouncementOptionsConfigurator
{
    private AnnouncementLayoutEditorWindow? _layoutEditorWindow;

    public void Configure(
        Control controlRow,
        IConfigurationManager cfg,
        Control announcementMaxVisibleSlider,
        Label announcementPresetOverridesLabel,
        BoxContainer announcementPresetOverridesContainer,
        Button announcementLayoutEditorButton,
        Button announcementLayoutResetButton,
        Control announcementLayoutSummaryLabel)
    {
        if (controlRow is not OptionsTabControlRow control)
            return;
        if (announcementMaxVisibleSlider is not OptionSlider maxVisibleSlider)
            return;

        control.AddOptionSlider(
            LuaCCVars.AnnouncementMaxVisible,
            maxVisibleSlider,
            AnnouncementOverlayUIController.MinVisibleAnnouncements,
            AnnouncementOverlayUIController.MaxVisibleAnnouncements);

        AddPerAnnouncementOverrides(control, cfg, announcementPresetOverridesLabel, announcementPresetOverridesContainer);
        RegisterAnnouncementLayoutEditor(
            cfg,
            announcementLayoutEditorButton,
            announcementLayoutResetButton,
            announcementLayoutSummaryLabel);
    }

    private static void AddPerAnnouncementOverrides(
        OptionsTabControlRow control,
        IConfigurationManager cfg,
        Label announcementPresetOverridesLabel,
        BoxContainer announcementPresetOverridesContainer)
    {
        var presets = AnnouncementPresetCatalog.All
            .OrderBy(preset => Loc.GetString(preset.Name))
            .ToList();

        if (presets.Count == 0)
        {
            announcementPresetOverridesLabel.Visible = false;
            return;
        }

        var availablePreferences = new List<AnnouncementDisplayPreference>
        {
            AnnouncementDisplayPreference.Stylized,
            AnnouncementDisplayPreference.Default,
            AnnouncementDisplayPreference.Simplified,
            AnnouncementDisplayPreference.Disabled
        };

        foreach (var preset in presets)
        {
            if (availablePreferences.Count == 0)
                continue;

            var dropDown = new OptionDropDown
            {
                Title = Loc.GetString(preset.Name)
            };

            announcementPresetOverridesContainer.AddChild(dropDown);
            control.AddOption(new AnnouncementPresetOverrideOption(
                control,
                cfg,
                dropDown,
                preset.Id,
                availablePreferences,
                AnnouncementDisplayPreference.Stylized));
        }
    }

    private void RegisterAnnouncementLayoutEditor(
        IConfigurationManager cfg,
        Button announcementLayoutEditorButton,
        Button announcementLayoutResetButton,
        Control announcementLayoutSummaryLabel)
    {
        announcementLayoutEditorButton.OnPressed += _ =>
        {
            if (_layoutEditorWindow != null && !_layoutEditorWindow.Disposed)
            {
                _layoutEditorWindow.OpenCentered();
                return;
            }

            _layoutEditorWindow = new AnnouncementLayoutEditorWindow();
            _layoutEditorWindow.OnClose += () => _layoutEditorWindow = null;
            _layoutEditorWindow.OpenCentered();
        };

        announcementLayoutResetButton.OnPressed += _ =>
        {
            cfg.SetCVar(LuaCCVars.AnnouncementLayoutOverrides, string.Empty);
            UpdateAnnouncementLayoutSummary(cfg, announcementLayoutSummaryLabel);
            cfg.SaveToFile();
        };

        cfg.OnValueChanged(
            LuaCCVars.AnnouncementLayoutOverrides,
            _ => UpdateAnnouncementLayoutSummary(cfg, announcementLayoutSummaryLabel),
            true);
    }

    private static void UpdateAnnouncementLayoutSummary(IConfigurationManager cfg, Control summaryLabel)
    {
        var overrides = AnnouncementLayoutOverrides.Parse(cfg.GetCVar(LuaCCVars.AnnouncementLayoutOverrides));
        var text = Loc.GetString(
            "rmc-ui-options-announcements-layout-summary",
            ("count", overrides.Count));

        switch (summaryLabel)
        {
            case Label label:
                label.Text = text;
                break;
            case RichTextLabel rich:
                rich.Text = text;
                break;
        }
    }

    private sealed class AnnouncementPresetOverrideOption : BaseOption
    {
        private readonly IConfigurationManager _cfg;
        private readonly OptionDropDown _dropDown;
        private readonly string _presetId;
        private readonly HashSet<AnnouncementDisplayPreference> _availablePreferences;
        private readonly AnnouncementDisplayPreference _defaultPreference;
        private readonly Dictionary<AnnouncementDisplayPreference, int> _entryIds = new();
        private Dictionary<string, AnnouncementDisplayPreference> _cachedOverrides = new();

        private AnnouncementDisplayPreference? SelectedPreference
        {
            get
            {
                if (_dropDown.Button.SelectedMetadata is not int value || value < 0)
                    return null;

                var preference = (AnnouncementDisplayPreference) value;
                return _availablePreferences.Contains(preference) ? preference : null;
            }
            set
            {
                var target = value ?? _defaultPreference;
                if (_entryIds.TryGetValue(target, out var id))
                    _dropDown.Button.SelectId(id);
            }
        }

        public AnnouncementPresetOverrideOption(
            OptionsTabControlRow controller,
            IConfigurationManager cfg,
            OptionDropDown dropDown,
            string presetId,
            IReadOnlyCollection<AnnouncementDisplayPreference> availablePreferences,
            AnnouncementDisplayPreference defaultPreference) : base(controller)
        {
            _cfg = cfg;
            _dropDown = dropDown;
            _presetId = presetId;
            _defaultPreference = defaultPreference;
            _availablePreferences = new HashSet<AnnouncementDisplayPreference>(availablePreferences);

            var nextId = 0;
            foreach (var preference in availablePreferences)
            {
                var key = preference switch
                {
                    AnnouncementDisplayPreference.Stylized => "rmc-ui-options-announcements-style-stylized",
                    AnnouncementDisplayPreference.Default => "rmc-ui-options-announcements-style-default",
                    AnnouncementDisplayPreference.Simplified => "rmc-ui-options-announcements-style-simplified",
                    AnnouncementDisplayPreference.Disabled => "rmc-ui-options-announcements-style-disabled",
                    _ => null
                };

                if (key == null)
                    continue;

                _dropDown.Button.AddItem(Loc.GetString(key), nextId);
                _dropDown.Button.SetItemMetadata(_dropDown.Button.GetIdx(nextId), (int) preference);
                _entryIds[preference] = nextId;
                nextId++;
            }

            _dropDown.Button.OnItemSelected += args =>
            {
                _dropDown.Button.SelectId(args.Id);
                ValueChanged();
            };

            _cachedOverrides = AnnouncementPreferenceOverrides.Parse(_cfg.GetCVar(LuaCCVars.AnnouncementStyleOverrides));
            _cfg.OnValueChanged(LuaCCVars.AnnouncementStyleOverrides, OnOverridesChanged);
        }

        private void OnOverridesChanged(string serialized)
        {
            _cachedOverrides = AnnouncementPreferenceOverrides.Parse(serialized);
        }

        public override void LoadValue()
        {
            SelectedPreference = GetStoredPreference();
        }

        public override void SaveValue()
        {
            var overrides = new Dictionary<string, AnnouncementDisplayPreference>(_cachedOverrides);
            var preference = SelectedPreference ?? _defaultPreference;
            if (preference == _defaultPreference)
                overrides.Remove(_presetId);
            else
                overrides[_presetId] = preference;

            _cfg.SetCVar(LuaCCVars.AnnouncementStyleOverrides, AnnouncementPreferenceOverrides.Serialize(overrides));
        }

        public override void ResetToDefault()
        {
            SelectedPreference = null;
        }

        public override bool IsModified()
        {
            var selected = SelectedPreference ?? _defaultPreference;
            var stored = GetStoredPreference() ?? _defaultPreference;
            return selected != stored;
        }

        public override bool IsModifiedFromDefault()
        {
            return (SelectedPreference ?? _defaultPreference) != _defaultPreference;
        }

        private AnnouncementDisplayPreference? GetStoredPreference()
        {
            if (!_cachedOverrides.TryGetValue(_presetId, out var preference))
                return null;

            return _availablePreferences.Contains(preference) ? preference : null;
        }
    }
}
