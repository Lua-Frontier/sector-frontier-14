using Content.Shared.Corvax.TTS;
using Content.Shared.Labels.Components;
using Content.Shared.Roles;
using Content.Shared.Speech;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using System.Collections.Generic;
using System.Globalization;

namespace Content.IntegrationTests.Tests.Localization;

public sealed class RuLocalizationCoverageTest
{
    [Test]
    public async Task TestRuLocalizationCoverage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ProtoMan;
        var compFactory = server.ResolveDependency<IComponentFactory>();
        var locMan = server.ResolveDependency<ILocalizationManager>();

        var previousCulture = locMan.DefaultCulture;
        var ruCulture = new CultureInfo("ru-RU");
        locMan.SetCulture(ruCulture);

        var missing = new List<string>();

        void CheckKey(string? key, string context)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                missing.Add($"{context}: <empty>");
                return;
            }

            if (!locMan.HasString(key))
                missing.Add($"{context}: {key}");
        }

        foreach (var voice in protoMan.EnumeratePrototypes<TTSVoicePrototype>())
        {
            if (!voice.RoundStart)
                continue;

            CheckKey(voice.Name, $"TTSVoice {voice.ID}");
        }

        foreach (var verb in protoMan.EnumeratePrototypes<SpeechVerbPrototype>())
        {
            CheckKey(verb.Name, $"SpeechVerb {verb.ID}");
        }

        foreach (var job in protoMan.EnumeratePrototypes<JobPrototype>())
        {
            if (!string.IsNullOrWhiteSpace(job.Description) &&
                job.Description.StartsWith("job-description-", StringComparison.Ordinal))
            {
                CheckKey(job.Description, $"Job {job.ID}");
            }
        }

        foreach (var proto in protoMan.EnumeratePrototypes<EntityPrototype>())
        {
            if (!proto.TryGetComponent<LabelComponent>(out var label, compFactory))
                continue;

            var currentLabel = label.CurrentLabel;
            if (string.IsNullOrWhiteSpace(currentLabel))
                continue;

            if (currentLabel.StartsWith("holopad-", StringComparison.Ordinal) ||
                currentLabel.StartsWith("reagent-name-", StringComparison.Ordinal))
                CheckKey(currentLabel, $"Label {proto.ID}");
        }

        if (previousCulture != null)
            locMan.SetCulture(previousCulture);

        Assert.That(missing, Is.Empty, $"Missing ru-RU localization keys:\n{string.Join("\n", missing)}");

        await pair.CleanReturnAsync();
    }
}
