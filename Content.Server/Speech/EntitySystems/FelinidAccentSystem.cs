using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems
{
    public sealed class FelinidAccentSystem : EntitySystem
    {
        [Dependency] private readonly IRobustRandom _random = default!;

        private static readonly IReadOnlyDictionary<string, string> SpecialWords = new Dictionary<string, string>()
        {
            { "you", "wu" },
        };

        public override void Initialize()
        {
            SubscribeLocalEvent<FelinidAccentComponent, AccentGetEvent>(OnAccent);
        }

        public string Accentuate(string message)
        {
            foreach (var (word, repl) in SpecialWords)
            {
                message = message.Replace(word, repl);
            }

            return message.Replace("r", "w").Replace("R", "W")
                .Replace("l", "w").Replace("L", "W")
                //Start ru locale
                .Replace("р", "в").Replace("Р", "В")
                .Replace("л", "в").Replace("Л", "В");
        }

        private void OnAccent(EntityUid uid, FelinidAccentComponent component, AccentGetEvent args)
        {
            args.Message = Accentuate(args.Message);
        }
    }
}
