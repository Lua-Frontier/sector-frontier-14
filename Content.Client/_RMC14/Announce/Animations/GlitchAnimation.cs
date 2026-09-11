using System.Collections.Generic;
using Content.Shared._RMC14.Announce.Animations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Client._RMC14.Announce.Animations;

public sealed class GlitchAnimation : IAnnouncementAnimation
{
    private const float MinTickInterval = 0.005f;
    private const int MaxAdvancePerUpdate = 8;
    private const float VisualGlitchSpeed = 0.32f;

    private readonly GlitchAnimationConfig _config;
    private int _currentLine;
    private int _currentChar;
    private float _timer;
    private float _burstTimer;

    public GlitchAnimation(GlitchAnimationConfig config) => _config = config;

    public void Reset(AnnouncementAnimationContext context)
    {
        _currentLine = 0;
        _currentChar = 0;
        _timer = 0f;
        _burstTimer = 0f;
        if (_config.EnableVisualGlitch)
            ResetVisualState(context);

        for (var i = context.TitleOffset; i < context.Labels.Length; i++)
        {
            (context.Labels[i] as RichTextLabel)?.SetMessage(FormattedMessage.FromMarkupPermissive(string.Empty));
        }
    }

    public AnnouncementAnimationStatus Update(AnnouncementAnimationContext context, float deltaTime)
    {
        var intensity = GetIntensity(_config.GlitchChance);
        if (_config.EnableVisualGlitch)
            UpdatePortraitGlitch(context, intensity, deltaTime);

        var printInterval = MathF.Max(MinTickInterval, _config.PrintSpeed);
        _timer += deltaTime;
        if (_timer < printInterval)
            return AnnouncementAnimationStatus.Running;

        var advanced = 0;
        var changed = false;
        while (_timer >= printInterval && advanced < MaxAdvancePerUpdate)
        {
            _timer -= printInterval;
            advanced++;

            var finished = Advance(context, out var printed);
            changed |= printed;

            if (finished)
            {
                if (changed)
                    UpdateDisplay(context);

                return AnnouncementAnimationStatus.Finished;
            }
        }

        if (changed)
            UpdateDisplay(context);

        return AnnouncementAnimationStatus.Running;
    }

    private void UpdatePortraitGlitch(AnnouncementAnimationContext context, float intensity, float deltaTime)
    {
        if (_burstTimer > 0f)
        {
            _burstTimer = MathF.Max(0f, _burstTimer - deltaTime);
        }
        else if (context.Random.Prob(GetBurstStartChancePerFrame(intensity, deltaTime)))
        {
            _burstTimer = context.Random.NextFloat(0.12f, 0.30f);
        }

        UpdateVisual(context, intensity, _burstTimer > 0f, deltaTime);
    }

    private bool Advance(AnnouncementAnimationContext context, out bool printed)
    {
        printed = false;
        var cleanText = context.CleanText;

        if (_currentLine >= cleanText.Length)
            return true;

        var lineText = cleanText[_currentLine];
        if (_currentChar >= lineText.Length)
        {
            _currentLine++;
            _currentChar = 0;
            return _currentLine >= cleanText.Length;
        }

        _currentChar++;
        printed = true;
        return false;
    }

    private void UpdateDisplay(AnnouncementAnimationContext context)
    {
        var originalText = context.OriginalText;
        var cleanText = context.CleanText;
        var style = context.Style;

        for (var i = context.TitleOffset; i < context.Labels.Length; i++)
        {
            var textIndex = i - context.TitleOffset;
            if (textIndex < _currentLine)
            {
                var message = context.FormatMessage(originalText[textIndex], style);
                (context.Labels[i] as RichTextLabel)?.SetMessage(message);
            }
            else if (textIndex == _currentLine)
            {
                var currentLineText = cleanText[textIndex];
                var maxLength = Math.Min(_currentChar, currentLineText.Length);
                var partialText = currentLineText[..maxLength];
                var message = context.FormatMessage(partialText, style);
                (context.Labels[i] as RichTextLabel)?.SetMessage(message);
            }
            else
            {
                (context.Labels[i] as RichTextLabel)?.SetMessage(FormattedMessage.FromMarkupPermissive(string.Empty));
            }
        }
    }

    private static float GetIntensity(float glitchChance)
    {
        return Math.Clamp(0.15f + glitchChance * 6f, 0.15f, 1f);
    }

    private static float GetBurstStartChancePerFrame(float intensity, float deltaTime)
    {
        var startsPerSecond = (0.30f + intensity * 1.0f) * VisualGlitchSpeed;
        return Math.Clamp(startsPerSecond * deltaTime, 0f, 1f);
    }

    private static void ResetVisualState(AnnouncementAnimationContext context)
    {
        if (context.VisualContainer == null)
            return;

        context.VisualContainer.Margin = new Thickness(0f);
        ApplyVisualTintRecursive(context.VisualContainer, Color.White);
    }

    private static void UpdateVisual(AnnouncementAnimationContext context, float intensity, bool burstActive, float deltaTime)
    {
        var visual = context.VisualContainer;
        if (visual == null)
            return;

        var burstFactor = burstActive ? 1f : 0.35f;
        var jitterAmount = 1.2f + intensity * (burstActive ? 7f : 4f);
        var jitterChance = Math.Clamp(
            (0.10f + intensity * 0.35f) * burstFactor * deltaTime * 60f * VisualGlitchSpeed,
            0f,
            1f);

        if (context.Random.Prob(jitterChance))
        {
            var jitterX = context.Random.NextFloat(-jitterAmount, jitterAmount);
            var jitterY = context.Random.NextFloat(-jitterAmount * 0.30f, jitterAmount * 0.30f);
            visual.Margin = new Thickness(jitterX, jitterY, 0f, 0f);
        }
        else
        {
            visual.Margin = new Thickness(0f);
        }

        var flickerChance = Math.Clamp(
            (0.015f + intensity * 0.10f) * burstFactor * deltaTime * 60f * VisualGlitchSpeed,
            0f,
            1f);
        if (context.Random.Prob(flickerChance))
        {
            var tint = context.Random.NextFloat(0.78f, 1.0f);
            var tintColor = new Color(tint, MathF.Min(1f, tint * 1.06f), tint, 1f);
            ApplyVisualTintRecursive(visual, tintColor);
        }
        else
        {
            ApplyVisualTintRecursive(visual, Color.White);
        }
    }

    private static void ApplyVisualTintRecursive(Control root, Color tint)
    {
        var stack = new Stack<Control>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            node.Modulate = new Color(tint.R, tint.G, tint.B, node.Modulate.A);

            foreach (var child in node.Children)
            {
                stack.Push(child);
            }
        }
    }
}
