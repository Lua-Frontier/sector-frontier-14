// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Client._Lua.Achievements;
using Content.Client.Gameplay;
using Content.Shared._Lua.Achievements;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Lua.UserInterface.Systems.Achievements;

[UsedImplicitly]
public sealed class AchievementsUIController : UIController, IOnStateExited<GameplayState>
{
    private static readonly TimeSpan ToastDuration = TimeSpan.FromSeconds(4.5);
    private static readonly TimeSpan ToastSlide = TimeSpan.FromSeconds(0.35);
    private const float ToastMarginRight = 16f;
    private const float ToastMarginBottom = 16f;
    private const float ToastGap = 8f;
    private const float ToastSlideHorizontal = 96f;
    private const float ToastSlideVertical = 32f;

    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystems = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private AchievementsWindow? _window;
    private BoxContainer? _toastHost;
    private readonly Queue<string> _toastQueue = new();
    private AchievementToast? _activeToast;
    private TimeSpan _toastStarted;
    private TimeSpan _toastEnds;
    private SpriteSystem? _sprite;

    private SpriteSystem Sprite => _sprite ??= _entitySystems.GetEntitySystem<SpriteSystem>();

    public override void Initialize()
    {
        base.Initialize();

        _net.RegisterNetMessage<RequestAchievementsMessage>();
        _net.RegisterNetMessage<TryUnlockAchievementMessage>();
        _net.RegisterNetMessage<ClaimAchievementRewardMessage>();
        _net.RegisterNetMessage<AchievementsStateMessage>(OnAchievementsState);
        _net.RegisterNetMessage<AchievementUnlockedMessage>(OnAchievementUnlocked);
        _net.RegisterNetMessage<AchievementProgressMessage>(OnAchievementProgress);
        _net.RegisterNetMessage<AchievementRewardClaimedMessage>(OnAchievementRewardClaimed);
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        UpdateToastAnimation();
    }

    public void OnStateExited(GameplayState state)
    {
        ClearToasts();

        if (_window == null)
            return;

        _window.Orphan();
        _window = null;
    }

    public void OpenWindow()
    {
        EnsureWindow();
        _window!.FitNearFullscreen();
        _window.UpdateState([]);
        _window.OpenCentered();
        _window.MoveToFront();
        _net.ClientSendMessage(new RequestAchievementsMessage());
        _net.ClientSendMessage(new TryUnlockAchievementMessage(AchievementIds.TutorialAchievementsWindow));
    }

    public void ToggleWindow()
    {
        EnsureWindow();

        if (_window!.IsOpen)
            _window.Close();
        else
            OpenWindow();
    }

    private void EnsureWindow()
    {
        if (_window is { Disposed: false })
            return;

        _window = UIManager.CreateWindow<AchievementsWindow>();
    }

    private void OnAchievementsState(AchievementsStateMessage message)
    {
        if (_window is not { Disposed: false })
            return;

        _window.UpdateState(message.Entries);
    }

    private void OnAchievementUnlocked(AchievementUnlockedMessage message)
    {
        if (_window is { Disposed: false, IsOpen: true })
            _window.ApplyUnlock(message.AchievementId, message.UnlockedAtTicks);

        EnqueueToast(message.AchievementId);
    }

    private void OnAchievementProgress(AchievementProgressMessage message)
    {
        if (_window is not { Disposed: false, IsOpen: true })
            return;

        _window.ApplyProgress(message.AchievementId, message.Progress, message.ProgressMax);
    }

    private void OnAchievementRewardClaimed(AchievementRewardClaimedMessage message)
    {
        if (_window is not { Disposed: false, IsOpen: true })
            return;

        _window.ApplyRewardClaimed(message.AchievementId);
    }

    private void EnqueueToast(string achievementId)
    {
        if (!_prototypes.HasIndex<AchievementPrototype>(achievementId))
            return;

        _toastQueue.Enqueue(achievementId);
        if (_activeToast == null)
            ShowNextToast();
    }

    private void ShowNextToast()
    {
        while (_toastQueue.Count > 0)
        {
            var id = _toastQueue.Dequeue();
            if (!_prototypes.TryIndex<AchievementPrototype>(id, out var proto))
                continue;

            EnsureToastHost();
            var toast = new AchievementToast();
            toast.Setup(proto, Sprite);
            toast.Modulate = Color.White.WithAlpha(0f);
            _toastHost!.AddChild(toast);
            _activeToast = toast;
            _toastStarted = _timing.RealTime;
            _toastEnds = _toastStarted + ToastDuration;
            ApplyToastPosition(1f);
            return;
        }

        ClearToasts();
    }

    private void EnsureToastHost()
    {
        if (_toastHost is { Disposed: false })
            return;

        _toastHost = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = (int) ToastGap,
            MouseFilter = Control.MouseFilterMode.Ignore,
            MaxWidth = 340,
        };

        UIManager.PopupRoot.AddChild(_toastHost);
        LayoutContainer.SetGrowHorizontal(_toastHost, LayoutContainer.GrowDirection.Begin);
        LayoutContainer.SetGrowVertical(_toastHost, LayoutContainer.GrowDirection.Begin);
        ApplyToastPosition(0f);
    }

    private void ApplyToastPosition(float slideProgress)
    {
        if (_toastHost == null)
            return;
        var bottomMargin = (int) (ToastMarginBottom - slideProgress * ToastSlideVertical);
        var rightMargin = -ToastMarginRight + slideProgress * ToastSlideHorizontal;

        LayoutContainer.SetAnchorAndMarginPreset(_toastHost, LayoutContainer.LayoutPreset.BottomRight, margin: bottomMargin);
        LayoutContainer.SetMarginRight(_toastHost, rightMargin);
    }

    private void UpdateToastAnimation()
    {
        if (_activeToast == null)
            return;

        var now = _timing.RealTime;
        if (now >= _toastEnds)
        {
            _activeToast.Orphan();
            _activeToast = null;
            ShowNextToast();
            return;
        }

        var elapsed = now - _toastStarted;
        var remaining = _toastEnds - now;

        float alpha;
        if (elapsed < ToastSlide)
            alpha = (float) (elapsed / ToastSlide);
        else if (remaining < ToastSlide)
            alpha = (float) (remaining / ToastSlide);
        else
            alpha = 1f;

        _activeToast.Modulate = Color.White.WithAlpha(Math.Clamp(alpha, 0f, 1f));
        var slideProgress = 1f - Math.Clamp(alpha, 0f, 1f);
        ApplyToastPosition(slideProgress);
    }

    private void ClearToasts()
    {
        _toastQueue.Clear();
        _activeToast?.Orphan();
        _activeToast = null;
        _toastHost?.Orphan();
        _toastHost = null;
    }
}
