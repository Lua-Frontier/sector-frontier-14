using Content.Server.Backmen.Speech.Components;
using Content.Server.Speech;
using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Robust.Shared.Random;
using System.Text;
using System.Text.RegularExpressions;

namespace Content.Server.Backmen.Speech.EntitySystems;

public sealed class XenoAccentSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoAccentComponent, AccentGetEvent>(OnAccent);
    }

    public string Accentuate(string message)
    {
        bool ru = Regex.IsMatch(message, @"[а-яА-ЯёЁ]");
        var words = message.Split();
        var accentedMessage = new StringBuilder(message.Length + 2);

        for (var i = 0; i < words.Length; i++)
        {
            var word = words[i];

            if (_random.NextDouble() >= 0.5)
            {
                if (ru)
                    accentedMessage.Append("Хи");
                else
                    accentedMessage.Append("HI");
                if (word.Length > 1)
                {
                    foreach (var _ in word)
                    {
                        if (ru)
                            accentedMessage.Append('с');
                        else
                            accentedMessage.Append('S');
                    }

                    if (_random.NextDouble() >= 0.3)
                    {
                        if (ru)
                            accentedMessage.Append('с');
                        else
                            accentedMessage.Append('s');
                    }
                }
                else
                {
                    if (ru)
                        accentedMessage.Append('с');
                    else
                        accentedMessage.Append('S');
                }
            }
            else
            {
                if (ru)
                    accentedMessage.Append("Хи");
                else
                    accentedMessage.Append("HI");
                foreach (var _ in word)
                {
                    if (_random.NextDouble() >= 0.8)
                    {
                        if (ru)
                            accentedMessage.Append('х');
                        else
                            accentedMessage.Append('H');
                    }
                    else
                    {
                        if (ru && _random.NextDouble() >= 0.7)
                            accentedMessage.Append('ш');
                        else if (ru)
                            accentedMessage.Append('с');
                        else
                            accentedMessage.Append('S');
                    }
                }

            }

            if (i < words.Length - 1)
                accentedMessage.Append(' ');
        }

        accentedMessage.Append('!');

        return accentedMessage.ToString();
    }

    private void OnAccent(EntityUid uid, XenoAccentComponent component, AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message);
    }
}
