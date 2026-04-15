using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Robust.Shared.Random; // RuLocal

namespace Content.Server.Speech.EntitySystems;

public sealed class MothAccentSystem : EntitySystem
{
    private static readonly Regex RegexLowerBuzz = new Regex("z{1,3}");
    private static readonly Regex RegexUpperBuzz = new Regex("Z{1,3}");

    [Dependency] private readonly IRobustRandom _random = default!; // RuLocal

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MothAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, MothAccentComponent component, AccentGetEvent args)
    {
        var message = args.Message;

        // buzzz
        message = RegexLowerBuzz.Replace(message, "zzz");
        // buZZZ
        message = RegexUpperBuzz.Replace(message, "ZZZ");

        // StartRuLocal
        // з => ззз
        message = Regex.Replace(
            message,"з{1,3}",
            _random.Pick(new List<string>() { "зз", "ззз" })
        );
        // З => ЗЗЗ
        message = Regex.Replace(
            message,"З{1,3}",
            _random.Pick(new List<string>() { "ЗЗ", "ЗЗЗ" })
        );
        // ж => жжж
        message = Regex.Replace(
            message,"ж{1,3}",
            _random.Pick(new List<string>() { "жж", "жжж" })
        );
        // Ж => ЖЖЖ
        message = Regex.Replace(
            message,"Ж{1,3}",
            _random.Pick(new List<string>() { "ЖЖ", "ЖЖЖ" })
        );
        // EndRuLocal

        args.Message = message;
    }
}
