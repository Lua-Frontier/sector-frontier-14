using Content.Server.Holiday;
using Content.Shared.ADT;
using Content.Shared.CCVar;
using Robust.Shared.Random;
using System.Linq;

namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    [ViewVariables]
    public string? LobbyBackground { get; private set; }

    [ViewVariables]
    private List<string>? _lobbyBackgrounds;

    private void InitializeLobbyBackground()
    {
        SubscribeLocalEvent<HolidaysRefreshedEvent>(OnHolidaysRefreshedLobbyBackground);
        RandomizeLobbyBackground();
    }

    private void OnHolidaysRefreshedLobbyBackground(HolidaysRefreshedEvent ev)
    {
        RandomizeLobbyBackground();
        if (RunLevel == GameRunLevel.PreRoundLobby)
            SendStatusToAll();
    }

    private void RandomizeLobbyBackground()
    {
        var holidaysEnabled = _cfg.GetCVar(CCVars.HolidaysEnabled);
        var available = AnimatedLobbyScreenPrototype.GetAvailable(
            _prototypeManager,
            DateTime.Now,
            holidaysEnabled);

        _lobbyBackgrounds = available.Select(x => x.Path).ToList();
        LobbyBackground = _lobbyBackgrounds.Count > 0
            ? _robustRandom.Pick(_lobbyBackgrounds)
            : null;
    }
}
