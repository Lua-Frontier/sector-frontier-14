// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Shared._Lua.Announce;
using Content.Shared._RMC14.Announce;
using Content.Shared._RMC14.Announce.Animations;
using Robust.Shared.Maths;

namespace Content.Client._Lua.Announce;

public static class AnnouncementPresentationCatalog
{
    public static readonly AnnouncementDisplayPreference[] AvailablePreferences =
    {
        AnnouncementDisplayPreference.Stylized,
        AnnouncementDisplayPreference.Default,
        AnnouncementDisplayPreference.Simplified
    };

    public static AnnouncementPresentation Resolve(
        AnnouncementPreset preset,
        AnnouncementDisplayPreference preference)
    {
        var presentation = preference switch
        {
            AnnouncementDisplayPreference.Default => CreateBaseDefault(),
            AnnouncementDisplayPreference.Simplified => CreateBaseSimplified(),
            _ => CreateBaseStylized()
        };

        ApplyPreset(presentation, preset, preference);
        return presentation;
    }

    private static AnnouncementPresentation CreateBaseStylized()
    {
        return new AnnouncementPresentation
        {
            Style = CreateBaseStyle(
                new TypewriterAnimationConfig { PrintSpeed = 0.025f },
                holdDuration: 10f,
                spriteSpacing: 0f,
                titleFontSize: 11f,
                responsiveScaleFactor: 2f,
                enableCrt: true)
        };
    }

    private static AnnouncementPresentation CreateBaseDefault()
    {
        return new AnnouncementPresentation
        {
            Style = CreateBaseStyle(
                new TypewriterAnimationConfig { PrintSpeed = 0.025f },
                holdDuration: 10f,
                spriteSpacing: 0f,
                titleFontSize: 10f,
                responsiveScaleFactor: 1.15f,
                enableCrt: false)
        };
    }

    private static AnnouncementPresentation CreateBaseSimplified()
    {
        var style = CreateBaseStyle(
            new NoneAnimationConfig(),
            holdDuration: 8f,
            spriteSpacing: 20f,
            titleFontSize: 10f,
            responsiveScaleFactor: 1.15f,
            enableCrt: false);
        style.LayoutConfig.SpriteDisplayMode = SpriteDisplayMode.FullSprite;
        style.SpriteConfig.SpriteScale = 0.85f;

        return new AnnouncementPresentation
        {
            ShowSprite = false,
            Style = style
        };
    }

    private static AnnouncementStyle CreateBaseStyle(
        IAnnouncementAnimationConfig animation,
        float holdDuration,
        float spriteSpacing,
        float titleFontSize,
        float responsiveScaleFactor,
        bool enableCrt)
    {
        return new AnnouncementStyle
        {
            AnimationConfig = new AnnouncementAnimationConfig
            {
                Animation = animation,
                HoldDuration = holdDuration,
                FadeOutDuration = 0.5f,
                FlickerChance = 0f,
                EnableCRT = enableCrt
            },
            LayoutConfig = new AnnouncementLayoutConfig
            {
                Position = AnnouncementPosition.TopLeft,
                SpeakerNamePosition = AnnouncementSpeakerNamePosition.Below,
                SpritePosition = AnnouncementSpritePosition.Left,
                SpriteSpacing = spriteSpacing,
                SpriteDisplayMode = SpriteDisplayMode.TopHalf,
                TitlePosition = AnnouncementTitlePosition.Above
            },
            BackgroundConfig = new AnnouncementBackgroundConfig
            {
                ShowBackground = false,
                BackgroundAlpha = 0.8f,
                BackgroundColor = Color.Black
            },
            TextConfig = new AnnouncementTextConfig
            {
                PrimaryColor = Color.White,
                Font = "Cozette",
                FontSize = 20f,
                LineHeight = 48f,
                ShowSpeakerName = false,
                SpeakerNameColor = Color.White,
                SpeakerNameFontSize = 12f
            },
            SpriteConfig = new AnnouncementSpriteConfig
            {
                ShowSpriteBox = false,
                SpriteBoxColor = Color.Black,
                SpriteBoxBorderColor = Color.White,
                SpriteBoxBorderThickness = 2f,
                SpriteBoxPadding = 10f,
                SpriteScale = 1f
            },
            TitleConfig = new AnnouncementTitleConfig
            {
                ShowTitle = true,
                TitleFont = "CozetteBold",
                TitleColor = Color.White,
                TitleFontSize = titleFontSize,
                TitleUnderline = true,
                TitleUnderlineThickness = 2f
            },
            ScalingConfig = new AnnouncementScalingConfig
            {
                EnableResponsiveScaling = true,
                ResponsiveScaleFactor = responsiveScaleFactor,
                MinScale = 0.5f,
                MaxScale = 2f
            }
        };
    }

    private static void ApplyPreset(
        AnnouncementPresentation presentation,
        AnnouncementPreset preset,
        AnnouncementDisplayPreference preference)
    {
        switch (preset)
        {
            case AnnouncementPreset.Comms:
                ApplyComms(presentation, preference);
                break;
            case AnnouncementPreset.Faction:
                ApplyFaction(presentation, preference);
                break;
            case AnnouncementPreset.Alert:
                ApplyAlert(presentation, preference);
                break;
            case AnnouncementPreset.OnboardComputer:
                ApplyOnboardComputer(presentation, preference);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(preset), preset, null);
        }
    }

    private static void ApplyComms(
        AnnouncementPresentation presentation,
        AnnouncementDisplayPreference preference)
    {
        var style = presentation.Style;
        style.TextConfig.PrimaryColor = Color.FromHex("#a8d6ff");
        style.TitleConfig.Title = "lua-announcement-title-comms-fallback";
        style.TitleConfig.TitleColor = Color.FromHex("#4a9eff");

        if (preference == AnnouncementDisplayPreference.Simplified)
            return;

        style.TextConfig.ShowSpeakerName = true;
        style.TextConfig.SpeakerNameColor = Color.White;
        style.TextConfig.SpeakerNameFontSize = 14f;
        style.SpriteConfig.ShowSpriteBox = true;
        style.SpriteConfig.SpriteBoxColor = Color.FromHex("#0a1a2d");
        style.SpriteConfig.SpriteBoxBorderColor = Color.FromHex("#4a9eff");
        style.SpriteConfig.SpriteBoxBorderThickness = 2f;
        style.SpriteConfig.SpriteBoxPadding = 18f;
        style.SpriteConfig.SpriteBoxShader = "RMCMarineAnnouncementGrid";
    }

    private static void ApplyFaction(
        AnnouncementPresentation presentation,
        AnnouncementDisplayPreference preference)
    {
        var style = presentation.Style;
        style.TextConfig.PrimaryColor = Color.FromHex("#a8d6ff");
        style.TitleConfig.Title = "lua-announcement-title-faction";
        style.TitleConfig.TitleColor = Color.FromHex("#4a9eff");

        if (preference == AnnouncementDisplayPreference.Simplified)
            return;

        presentation.ShowSprite = false;
        style.SpriteConfig.ShowSpriteBox = true;
        style.SpriteConfig.SpriteBoxColor = Color.FromHex("#0a1a2d");
        style.SpriteConfig.SpriteBoxBorderColor = Color.FromHex("#4a9eff");
        style.SpriteConfig.SpriteBoxBorderThickness = 2f;
        style.SpriteConfig.SpriteBoxPadding = 18f;

        if (preference == AnnouncementDisplayPreference.Stylized)
        {
            style.AnimationConfig.Animation = new GlitchAnimationConfig
            {
                PrintSpeed = 0.03f,
                GlitchChance = 0.005f
            };
            style.AnimationConfig.FlickerChance = 0.02f;
        }
        else
        {
            style.SpriteConfig.SpriteBoxShader = "RMCMarineAnnouncementGrid";
        }
    }

    private static void ApplyAlert(
        AnnouncementPresentation presentation,
        AnnouncementDisplayPreference preference)
    {
        var style = presentation.Style;
        style.TextConfig.PrimaryColor = Color.FromHex("#c43232");
        style.TextConfig.LineHeight = 28f;
        style.TitleConfig.Title = "lua-announcement-title-alert";
        style.TitleConfig.TitleColor = Color.FromHex("#8B0000");

        if (preference == AnnouncementDisplayPreference.Simplified)
        {
            style.TitleConfig.TitleFontSize = 14f;
            return;
        }

        presentation.PortraitRsi = "/Textures/_RMC14/Structures/Machines/status_display.rsi";
        presentation.PortraitState = "alert";
        presentation.TintPortrait = true;
        style.SpriteConfig.ShowSpriteBox = true;
        style.SpriteConfig.SpriteBoxColor = Color.FromHex("#0a1a2d");
        style.SpriteConfig.SpriteBoxBorderColor = style.TitleConfig.TitleColor;
        style.SpriteConfig.SpriteBoxPadding = 2f;
        style.SpriteConfig.SpriteScale = 1f;
        style.SpriteConfig.SpriteOffset = Vector2.Zero;
        style.LayoutConfig.SpriteDisplayMode = SpriteDisplayMode.FullSprite;
        style.TextConfig.FontSize = 26f;
        style.TitleConfig.TitleFontSize = 20f;

        if (preference == AnnouncementDisplayPreference.Stylized)
        {
            style.AnimationConfig.Animation = new GlitchAnimationConfig
            {
                PrintSpeed = 0.03f,
                GlitchChance = 0.005f
            };
            style.AnimationConfig.FlickerChance = 0f;
            style.ScalingConfig.ResponsiveScaleFactor = 1.15f;
        }
    }

    private static void ApplyOnboardComputer(
        AnnouncementPresentation presentation,
        AnnouncementDisplayPreference preference)
    {
        var style = presentation.Style;
        presentation.PortraitPrototype = "XenoborgEngiPrinted";
        style.TextConfig.PrimaryColor = Color.FromHex("#a8d6ff");
        style.TitleConfig.Title = "chat-manager-sender-announcement";
        style.TitleConfig.TitleColor = Color.FromHex("#4a9eff");

        if (preference == AnnouncementDisplayPreference.Simplified)
            return;

        style.SpriteConfig.ShowSpriteBox = true;
        style.SpriteConfig.SpriteBoxColor = Color.FromHex("#0a1a2d");
        style.SpriteConfig.SpriteBoxBorderColor = Color.FromHex("#4a9eff");
        style.SpriteConfig.SpriteBoxBorderThickness = 2f;
        style.SpriteConfig.SpriteBoxPadding = 18f;
        style.SpriteConfig.SpriteBoxShader = "RMCMarineAnnouncementGrid";
    }
}
