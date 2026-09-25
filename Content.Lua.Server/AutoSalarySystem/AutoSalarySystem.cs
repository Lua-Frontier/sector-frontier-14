// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Lua.Server.Bank;
using Content.Server.Chat.Managers;
using Content.Server.Popups;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Content.Lua.Common.CLVar;
using Content.Lua.Shared.AutoSalary;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Lua.Server.AutoSalarySystem;

public sealed class AutoSalarySystem : EntitySystem
{
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private float _currentTime;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        _currentTime = _cfg.GetCVar(CLVars.AutoSalaryInterval);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _currentTime -= frameTime;

        if (_currentTime <= 0)
        {
            _currentTime = _cfg.GetCVar(CLVars.AutoSalaryInterval);
            ProcessSalary();
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _currentTime = _cfg.GetCVar(CLVars.AutoSalaryInterval);
    }

    private void ProcessSalary()
    {
        var query = EntityQueryEnumerator<ActorComponent, SalaryTrackingComponent>();
        while (query.MoveNext(out var uid, out var actor, out var salary))
        {
            if (string.IsNullOrEmpty(salary.JobId))
                continue;

            if (!_prototypeManager.TryIndex(new ProtoId<JobPrototype>(salary.JobId), out var job))
                continue;

            if (!_bank.HasAccountBank(uid))
                continue;

            Logger.Info($"DEBUG: {ToPrettyString(uid)} jobID: {salary.JobId}");
            var amount = job.Salary;
            if (_bank.TryBankDeposit(uid, amount) && _bank.TryGetBalance(uid, out var balance))
            {
                NotifySalaryReceived(uid, balance, actor, amount);
            }
        }
    }

    private void NotifySalaryReceived(EntityUid uid, int balance, ActorComponent actor, int salary)
    {
        var changeAmount = $"+{salary}";
        var message = Loc.GetString(
            "bank-program-change-balance-notification",
            ("balance", balance),
            ("change", changeAmount),
            ("currencySymbol", "$")
        );

        _popup.PopupEntity(message, uid, Filter.Entities(uid), true, PopupType.Small);

        _chatManager.ChatMessageToOne(
            ChatChannel.Notifications,
            message,
            message,
            EntityUid.Invalid,
            false,
            actor.PlayerSession.Channel
        );
    }
}
