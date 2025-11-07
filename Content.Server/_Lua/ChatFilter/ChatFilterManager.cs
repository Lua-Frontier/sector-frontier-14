// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Shared.Database;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using System.Linq;
using System.Text.RegularExpressions;

namespace Content.Server._Lua.ChatFilter;

public sealed class ChatFilterManager
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly ILocalizationManager _loc = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    private readonly Dictionary<NetUserId, Queue<(string Message, TimeSpan Timestamp)>> _messageHistory = new();
    private const int MaxRepeatedMessages = 3;
    private const int MessageHistorySize = 5;
    private static readonly TimeSpan MessageHistoryTimeout = TimeSpan.FromSeconds(10);
    private static readonly Regex SingleWordRegex = new(@"^(\w+)$", RegexOptions.Compiled);
    private static readonly Regex WordBoundaryRegex = new(@"\b(\w+)\b", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> WordReplacements = new()
    {
        {"хохол", "человек"},
        {"хохолом", "человеком"},
        {"хохла", "человека"},
        {"хохлу", "человеку"},
        {"хохлов", "людей"},
        {"хохлы", "люди"},
        {"русня", "люди"},
        {"укроп", "человек"},
        {"москаль", "человек"},
        {"хач", "человек"},
        {"хача", "человека"},
        {"хачу", "человеку"},
        {"хачи", "люди"},
        {"хачей", "людей"},
        {"хачик", "человечек"},
        {"жид", "человек"},
        {"жиду", "человеку"},
        {"жида", "человека"},
        {"жидами", "людьми"},
        {"жиды", "люди"},
        {"жидом", "человеком"},
        {"жидяра", "человек"},
        {"жидяры", "люди"},
        {"жидовня", "люди"},
        {"чурка", "человек"},
        {"чурку", "человеку"},
        {"чурок", "люди"},
        {"чурки", "люди"},
        {"чуркой", "человеком"},
        {"nigger", "афро"},
        {"niger", "афро"},
        {"niga", "афро"},
        {"nigga", "афро"},
        {"naga", "человек"},
        {"нигер", "афро"},
        {"ниггер", "афро"},
        {"нигеры", "афросы"},
        {"ниггеры", "афросы"},
        {"ниггера", "афроса"},
        {"ниггеру", "афросу"},
        {"нигером", "афросом"},
        {"ниггорм", "афросом"},
        {"нигера", "афроса"},
        {"нигеру", "афросу"},
        {"нига", "афро"},
        {"нигга", "афро"},
        {"черномаз", "афро"},
        {"чернозад", "афро"},
        {"черножоп", "афро"},
        {"черномазый", "афро"},
        {"черножопый", "афро"},
        {"черномазых", "афро"},
        {"черномазого", "афро"},
        {"негритос", "афро"},
        {"негр", "афро"},
        {"негры", "афросы"},
        {"негритоса", "афроса"},
        {"негритосов", "афросов"},
        {"нигритос", "афро"},
        {"нигритоса", "афроса"},
        {"нигритосов", "афросов"},
        {"нага", "афро"},
        {"faggot", "мужеложец"},
        {"fagot", "мужеложец"},
        {"fag", "мужеложец"},
        {"fagg", "мужеложец"},
        {"pidor", "мужеложец"},
        {"pidar", "мужеложец"},
        {"piddor", "мужеложец"},
        {"pidorr", "мужеложец"},
        {"pidarr", "мужеложец"},
        {"piddorr", "мужеложец"},
        {"пидр", "мужлжц"},
        {"пидор", "мужеложец"},
        {"пидар", "мужеложец"},
        {"пидорр", "мужеложец"},
        {"пидарр", "мужеложец"},
        {"ппидорр", "мужеложец"},
        {"ппидарр", "мужеложец"},
        {"пидоры", "мужеложцы"},
        {"пидары", "мужеложцы"},
        {"пидора", "мужеложца"},
        {"пидара", "мужеложца"},
        {"пидору", "мужеложцу"},
        {"пидару", "мужеложцу"},
        {"пидарам", "мужеложцам"},
        {"пидором", "мужеложцам"},
        {"пiдорас", "мужеложец"},
        {"пiдарас", "мужеложец"},
        {"пидорас", "мужеложец"},
        {"пидарас", "мужеложец"},
        {"пидорасс", "мужеложец"},
        {"пидарасс", "мужеложец"},
        {"ппидорас", "мужеложец"},
        {"ппидарас", "мужеложец"},
        {"пидорассc", "мужеложец"},
        {"пидарассc", "мужеложец"},
        {"пидорасу", "мужеложцу"},
        {"пидарасу", "мужеложцу"},
        {"пидораса", "мужеложца"},
        {"пидараса", "мужеложца"},
        {"пидорасы", "мужеложцы"},
        {"пидарасы", "мужеложцы"},
        {"пидорасов", "мужеложцев"},
        {"пидарасов", "мужеложцев"},
        {"пидорасина", "мужеложцев"},
        {"пидорасинка", "мужеложец"},
        {"пидарасинка", "мужеложец"},
        {"пидораска", "мужеложец"},
        {"пидараска", "мужеложец"},
        {"пидорасик", "мужеложец"},
        {"пидарасик", "мужеложец"},
        {"педбир", "мужеложец"},
        {"педабир", "мужеложец"},
        {"педеростический", "мужеложный"},
        {"пидеростический", "мужеложный"},
        {"педерастический", "мужеложный"},
        {"пидерастический", "мужеложный"},
        {"пидрил", "мужеложец"},
        {"педрил", "мужеложец"},
        {"пидрила", "мужеложец"},
        {"педрила", "мужеложец"},
        {"пидрилы", "мужеложцы"},
        {"педрилы", "мужеложцы"},
        {"пидрило", "мужеложец"},
        {"пидрик", "мужеложец"},
        {"педрик", "мужеложец"},
        {"педераст", "мужеложец"},
        {"пидераст", "мужеложец"},
        {"педираст", "мужеложец"},
        {"педарас", "мужеложец"},
        {"педарасы", "мужеложцы"},
        {"педерас", "мужеложец"},
        {"педерасы", "мужеложцы"},
        {"гомик", "мужеложец"},
        {"гомики", "мужеложцы"},
        {"гомиков", "мужеложцев"},
        {"гомикав", "мужеложцев"},
        {"гомосек", "мужеложец"},
        {"гомосекк", "мужеложец"},
        {"гомосеки", "мужеложцы"},
        {"гомосеков", "мужеложцев"},
        {"гомосятина", "мужеложец"},
        {"педик", "мужеложец"},
        {"педики", "мужеложцы"},
        {"педика", "мужеложца"},
        {"педиков", "мужеложцев"},
        {"педикав", "мужеложцев"},
        {"говноёб", "мужеложец"},
        {"глиномес", "мужеложец"},
        {"глинамес", "мужеложец"},
        {"глиномесс", "мужеложец"},
        {"глинамесс", "мужеложец"},
        {"уебаны", "дураки"},
        {"даун", "глупый"},
        {"дауны", "глупые"},
        {"даунов", "глупых"},
        {"аутист", "глупый"},
        {"аутисты", "глупые"},
        {"аутистов", "глупых"},
        {"retard", "глупый"},
        {"retards", "глупые"},
        {"ретард", "глупый"},
        {"ретарды", "глупые"},
        {"ретардов", "глупых"},
        {"virgin", "невинный"},
        {"simp", "человек"},
        {"cимп", "человек"},
        {"cимпа", "человека"},
        {"cимпу", "человеку"},
        {"куколд", "человек"},
        {"incel", "человек"},
        {"инцел", "человек"},
        {"cunt", "дурак"},
        {"циганин", "человек"},
        {"пиндос", "космический американец"},
        {"пендос", "космический американец"},
        {"пиндосс", "космический американец"},
        {"пендосс", "космический американец"},
        {"пиндосом", "космическим американцем"},
        {"пендосом", "космическим американцем"},
        {"пендосы", "космические американцы"},
        {"пиндосы", "космические американцы"},
        {"кацап", "человек"},
        {"аллах бабах", "аллах"},
        {"аллах бум", "аллах"},
        {"бабах аллах", "аллах"},
        {"бум аллах", "аллах"},
    };

    private static readonly Dictionary<string, string> SingleWordReplacements = new()
    {
        {"гп", "глава персонала"},
        {"си", "старший инженер"},
        {"гв", "главврач"},
        {"км", "квартирмейстер"},
        {"нр", "научный руководитель"},
        {"гсб", "глава службы безопасности"},
        {"пф", "представитель фронтира"},
        {"та", "торговый аванпост"},
        {"тм", "трафик менеджер"},
        {"па", "пояс астероидов"},
        {"нф", "нордфолл"},
        {"доде", "добрый день"},
        {"удсм", "удачной смены"},
        {"?", "м?"},
    };

    private static readonly List<string> ProhibitedPhrases = new()
    {
        {"немецкий художник был прав"},
        {"был прав немецкий художник"},
        {"художник был прав"},
        {"прав был художник"},
        {"мать ебал"},
        {"ебал мать"},
        {"мать вашу ебал"},
        {"ебал вашу мать"},
        {"вашу мать ебал"},
        {"ебал в рот"},
        {"в рот ебал"},
        {"1488"},
        {"1 4 8 8"},
        {"14 8 8"},
        {"1 48 8"},
        {"1 4 88"},
        {"14 88"},
        {"88 14"},
        {"бигболсteam"},
        {"биг болс team"},
        {"бигболлсteam"},
        {"биг боллс team"},
        {"bigболлсteam"},
        {"big боллс team"},
        {"big balls team"},
        {"team big balls"},
        {"big team balls"},
        {"ballsteam"},
        {"balls team"},
        {"bigballs"},
        {"big balls"},
        {"bbt"},
        {"б б т"},
        {"b b t"},
        {"ебашу чурок"},
        {"чурок ебашу"},
        {"мамаша админа"},
        {"админа мамаша"},
        {"мать админа"},
        {"админа мать"},
        {"zov"},
        {"з о в"},
        {"гойда"},
        {"г о й д а"},
        {"слава гитлеру"},
        {"гитлеру слава"},
        {"гитлеру 1слава"},
        {"хайль гитлер"},
        {"хаиль гитлер"},
        {"гитлер хайль"},
        {"гитлер хаиль"},
        {"хайльгитлер"},
        {"хаильгитлер"},
        {"гитлерхайль"},
        {"гитлерхаиль"},
        {"гитлер был прав"},
        {"гитлербыл прав"},
        {"гитлербылправ"},
        {"гитлер былправ"},
        {"прав был гитлер"},
        {"зигхайль"},
        {"зигхаиль"},
        {"зиг хайль"},
        {"зиг хаиль"},
        {"кончил в рот"},
        {"в рот кончил"},
        {"зазичка"},
        {"зази4ка"},
        {"зазиchка"},
        {"zazicha"},
        {"zazi4ca"},
        {"zazichka"},
        {"ебаладмина"},
        {"ебал админа"},
        {"админаебал"},
        {"админа ебал"},
        {"ебалротадмина"},
        {"ебал ротадмина"},
        {"ебалрот админа"},
        {"ебал рот админа"},
        {"ебал рот"},
        {"рот ебал"},
        {"админпидарас"},
        {"админ пидарас"},
        {"пидарасадмин"},
        {"пидарас админ"},
        {"админпидор"},
        {"админ пидор"},
        {"пидорадмин"},
        {"пидор админ"},
        {"админхуй"},
        {"админ хуй"},
        {"хуйадмин"},
        {"хуй админ"},
        {"админсоси"},
        {"админ соси"},
        {"сосиадмин"},
        {"соси админ"},
        {"сосите админы"},
        {"админы сосите"},
        {"фуррибляди"},
        {"фурри бляди"},
        {"бляди фурри"},
        {"соситедушнилы"},
        {"сосите душнилы"},
        {"душнилысосите"},
        {"душнилы сосите"},
        {"соситевахтёры"},
        {"сосите вахтёры"},
        {"вахтёрысосите"},
        {"вахтёры сосите"},
        {"вахтеры сосите"},
        {"сосите вахтеры"},
        {"сжигаюжидос"},
        {"сжигаю жидос"},
        {"жидоссжигаю"},
        {"жидос сжигаю"},
        {"я ваш рот ебал"},
        {"ваш рот ебал"},
        {"рот ваш ебал"},
        {"хуй хуй хуй"},
        {"член член член"},
        {"пизда пизда"},
        {"ебать ебать"},
        {"z4fuhshqv3"},
        {"ecwipse"},
        {"cwient"},
        {"это не ддос"},
        {"не ддос это"},
        {"ддос не это"},
        {"discowd.gg"},
        {"discord.gg"},
        {"дискорд.гг"},
        {"всяческие экспвойты"},
        {"экспвойты всяческие"},
        {"экспл0йты"},
        {"эксплойты"},
        {"сталкивались с халатностью администрации"},
        {"халатность администрации"},
        {"получали бан без причины"},
        {"бан без причины"},
        {"без причины бан"},
        {"АРАЙЗ"},
        {"ARAIZ"},
        {"ARAЙZ"},
        {"А Р А Й З"},
        {"A R A I Z"},
        {"fucked by"},
        {"плотная от"},
        {"от плотная"},
        {"UNF"},
        {"U N F"},
        {"CSH"},
        {"C S H"},
        {"КСШ"},
        {"К С Ш"},
        {"дискорд сервер"},
        {"сервер дискорд"},
        {"другой сервер"},
        {"лучший сервер"},
        {"играю на другом"},
        {"другой проект"},
        {"наш проект"},
        {"наш сервер"},
        {"мой сервер"},
        {"админы даун"},
        {"даун админ"},
        {"админы уебаны"},
        {"уебаны админы"},
        {"плохие админы"},
        {"админы плохие"},
        {"говноадмины"},
        {"говно админы"},
        {"админы говно"},
        {"рейд на сервер"},
        {"рейдим сервер"},
        {"сервер рейдим"},
        {"набег"},
        {"набегатор"},
        {"набегаторы"},
        {"набег на"},
        {"на набег"},
        {"устроим набег"},
        {"набег устроим"},
        {"идём на набег"},
        {"идем на набег"},
        {"на набег идём"},
        {"на набег идем"},
        {"ты не игрок"},
        {"не игрок ты"},
        {"ты не робаст"},
        {"не робаст ты"},
        {"ты ходячий сбой"},
        {"ходячий сбой ты"},
        {"ты ебанный нпс"},
        {"ебанный нпс ты"},
        {"ты нпс"},
        {"dscrd gg"},
        {"d1scord gg"},
        {"disc0rd gg"},
        {"d1sc0rd gg"},
        {"prod by"},
        {"nabebebegator"},
        {"набebebegator"},
        {"земля плоская"},
        {"плоская земля"},
        {"плоское мышление"},
        {"плоская земля сквад"},
        {"вступи пзс"},
        {"вступи в пзс"},
        {"пзс вступи"},
        {"в пзс вступи"},
        {"сын шаболды"},
        {"шаболды сын"},
        {"педалька"},
    };

    private static readonly List<Regex> ProhibitedPatterns = new()
    {
        new Regex(@"h\s*t\s*t\s*p\s*s?\s*:\s*[:/\s]*", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"d\s*i\s*s\s*c\s*[o0]\s*r\s*d\s*[.\s]*g\s*g", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"д\s*и\s*с\s*к\s*о\s*р\s*д\s*[.\s]*г\s*г", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"discord\.gg", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"дискорд\.гг", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"disc[o0]rd\s*\.\s*gg", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"d[i1l!]sc[o0]rd\s*\.?\s*gg", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"dscrd\s*gg", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"d[i1]scrd\s*gg", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"dsc[o0]?rd\s+gg\s+\w+", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"1\s*4\s*8\s*8", RegexOptions.Compiled),
        new Regex(@"88\s*14", RegexOptions.Compiled),
        new Regex(@"14\s*88", RegexOptions.Compiled),
        new Regex(@"▒|▓|█|░|▀|▁|▂|▃|▄|▅|▆|▇|▉|▊|▋|▌|▐|▍|▎|▏", RegexOptions.Compiled),
        new Regex(@"Ž|ʌ|﹄|ㄱ", RegexOptions.Compiled),
        new Regex(@"(.)\1{20,}", RegexOptions.Compiled),
        new Regex(@"[А-Я]{200,}", RegexOptions.Compiled),
        new Regex(@"[А-Я\s]{300,}", RegexOptions.Compiled),
        new Regex(@"[А-Я]\s+[А-Я]\s+[А-Я]\s+[А-Я]\s+[А-Я]", RegexOptions.Compiled),
        new Regex(@"(чзх\s*){3,}", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"ip\s*:\s*\d+\.\d+\.\d+\.\d+", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"порт\s*:\s*\d+", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"port\s*:\s*\d+", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"\d+\.\d+\.\d+\.\d+:\d+", RegexOptions.Compiled),
    };

    public void Initialize()
    { }

    public string FilterMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return message;
        var filtered = SingleWordRegex.Replace(message, match =>
        {
            var lower = match.Value.ToLower();
            if (SingleWordReplacements.TryGetValue(lower, out var replacement)) return match.Value.All(char.IsUpper) ? replacement.ToUpper() : replacement;
            return match.Value;
        });
        filtered = WordBoundaryRegex.Replace(filtered, match =>
        {
            var lower = match.Value.ToLower();
            if (WordReplacements.TryGetValue(lower, out var replacement)) return match.Value.All(char.IsUpper) ? replacement.ToUpper() : replacement;
            return match.Value;
        });
        return filtered;
    }

    public bool IsAdmin(EntityUid source)
    {
        if (_playerManager.TryGetSessionByEntity(source, out var session)) return _adminManager.IsAdmin(session);
        return false;
    }

    public bool IsProhibitedContent(EntityUid source, string message)
    {
        if (!_playerManager.TryGetSessionByEntity(source, out var session))
            return false;

        if (_adminManager.IsAdmin(session)) return false;
        if (CheckRepeatedMessages(session.UserId, message))
        {
            LogAndNotify(source, "повторяющиеся сообщения");
            session.Channel.Disconnect(_loc.GetString("chat-filter-spam-reason"));
            return true;
        }
        if (!CheckProhibitedContent(message)) return false;
        LogAndNotify(source, message);
        session.Channel.Disconnect(_loc.GetString("chat-filter-kick-reason"));
        return true;
    }

    public bool IsProhibitedContent(ICommonSession source, string message)
    {
        if (_adminManager.IsAdmin(source)) return false;
        if (CheckRepeatedMessages(source.UserId, message))
        {
            LogAndNotify(source, "повторяющиеся сообщения");
            source.Channel.Disconnect(_loc.GetString("chat-filter-spam-reason"));
            return true;
        }

        if (!CheckProhibitedContent(message)) return false;
        LogAndNotify(source, message);
        source.Channel.Disconnect(_loc.GetString("chat-filter-kick-reason"));
        return true;
    }

    private bool CheckProhibitedContent(string message)
    {
        var normalized = message.TrimEnd().ToLower();
        foreach (var phrase in ProhibitedPhrases)
        { if (normalized.Contains(phrase)) return true; }
        foreach (var pattern in ProhibitedPatterns)
        { if (pattern.IsMatch(normalized)) return true; }
        return false;
    }

    private bool CheckRepeatedMessages(NetUserId userId, string message)
    {
        var normalized = message.Trim().ToLower();
        var currentTime = _timing.CurTime;
        if (!_messageHistory.TryGetValue(userId, out var history))
        {
            history = new Queue<(string, TimeSpan)>();
            _messageHistory[userId] = history;
        }
        while (history.Count > 0 && currentTime - history.Peek().Timestamp > MessageHistoryTimeout) history.Dequeue();
        history.Enqueue((normalized, currentTime));
        while (history.Count > MessageHistorySize) history.Dequeue();
        if (history.Count < MaxRepeatedMessages) return false;
        var lastMessages = history.TakeLast(MaxRepeatedMessages).ToList();
        return lastMessages.All(m => m.Message == normalized);
    }

    private void LogAndNotify(EntityUid source, string message)
    {
        _adminLogger.Add(LogType.Chat, LogImpact.High, $"{_entityManager.ToPrettyString(source):user} attempted prohibited message: {message}");
        _chatManager.SendAdminAlert(_loc.GetString("chat-filter-alert", ("player", _entityManager.ToPrettyString(source)), ("message", message)));
        if (_entityManager.System<SharedAudioSystem>() is { } audioSystem)
        { audioSystem.PlayGlobal(new SoundPathSpecifier("/Audio/Effects/Cargo/beep.ogg"), Filter.Empty().AddPlayers(_adminManager.ActiveAdmins), false, AudioParams.Default.WithVolume(5f)); }
    }

    private void LogAndNotify(ICommonSession source, string message)
    {
        _adminLogger.Add(LogType.Chat, LogImpact.High, $"{source} attempted prohibited message: {message}");
        _chatManager.SendAdminAlert(_loc.GetString("chat-filter-alert-session", ("player", source.Name), ("message", message)));
        if (_entityManager.System<SharedAudioSystem>() is { } audioSystem)
        { audioSystem.PlayGlobal(new SoundPathSpecifier("/Audio/Effects/Cargo/beep.ogg"), Filter.Empty().AddPlayers(_adminManager.ActiveAdmins), false, AudioParams.Default.WithVolume(5f)); }
    }
}

