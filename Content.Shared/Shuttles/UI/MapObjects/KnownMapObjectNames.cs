namespace Content.Shared.Shuttles.UI.MapObjects;

public static class KnownMapObjectNames
{
    private static readonly HashSet<string> StationNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Amber",
        "Anomalous Geode",
        "Bagel Station",
        "Bahama Mama's",
        "Barrier Gate",
        "Box Station",
        "Crazy Casey's Casino",
        "Courthouse",
        "Derelict McCargo",
        "Dev",
        "DM01 Entryway",
        "Elkridge Depot",
        "Empty",
        "Exo",
        "Fland Installation",
        "Grifty's Gas n Grub",
        "Listening Point Bravo",
        "Marathon Station",
        "Meteor Arena",
        "NFDev",
        "Oasis",
        "Omnichurch Beacon",
        "Packed",
        "Pirate Cove",
        "Plasma",
        "Reach",
        "Relic",
        "Saltern",
        "SC-RUST/MUR3NA-5.5",
        "Test TEG",
        "The North Pole",
        "The Pit",
        "Tinnia's Rest",
        "Аванпост Пиратов",
        "Аванпост Наёмников",
        "Аванпост СРБС",
        "Аванпост ЭИК",
        "Аванпост Экспедиций",
        "Аномальный Обломок",
        "Верфь А",
        "Верфь LuaTech",
        "Заброшенный МакКарго",
        "Заправка и Закусочная Грифти",
        "Красная Команда",
        "Лаборатория Аномалий",
        "Лаборатория Край",
        "Медицинский Центр",
        "Маяк",
        "ННКСС Тайпан",
        "Нордфолл",
        "Пункт прослушки Браво",
        "Синяя Команда",
        "Судебный Аванпост",
        "Сектор Фронтир",
        "Торговый Аванпост",
        "Торговый Терминал",
        "Торговый Форпост",
        "Центком",
        "Чёрный Рынок",
        "Электростанция Эдисона",
        "Фронтир",
    };

    private static readonly HashSet<string> SuffixedStationNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Торговый Терминал",
    };

    private static readonly HashSet<string> TradingNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Торговый Аванпост",
        "Торговый Терминал",
        "Торговый Форпост",
        "Чёрный Рынок",
    };

    public static bool IsKnownStationOrPoi(string name)
    {
        var normalized = Normalize(name);
        if (normalized.Length == 0)
            return false;

        foreach (var stationName in StationNames)
        {
            var known = Normalize(stationName);
            if (normalized.Equals(known, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        foreach (var stationName in SuffixedStationNames)
        {
            var known = Normalize(stationName);
            if (!normalized.StartsWith($"{known} ", StringComparison.OrdinalIgnoreCase))
                continue;

            var suffix = normalized[(known.Length + 1)..];
            if (suffix.Length == 1 && char.IsLetter(suffix[0]) || int.TryParse(suffix, out _))
                return true;
        }

        return false;
    }

    public static bool IsKnownTradingPoi(string name)
    {
        var normalized = Normalize(name);
        foreach (var tradingName in TradingNames)
        {
            var known = Normalize(tradingName);
            if (normalized.Equals(known, StringComparison.OrdinalIgnoreCase))
                return true;

            if (known.Equals("Торговый Терминал", StringComparison.OrdinalIgnoreCase) &&
                normalized.StartsWith($"{known} ", StringComparison.OrdinalIgnoreCase))
            {
                var suffix = normalized[(known.Length + 1)..];
                if (suffix.Length == 1 && char.IsLetter(suffix[0]) || int.TryParse(suffix, out _))
                    return true;
            }
        }

        return false;
    }

    private static string Normalize(string name)
    {
        return name.Trim().Replace("'", string.Empty).Replace("\"", string.Empty);
    }
}