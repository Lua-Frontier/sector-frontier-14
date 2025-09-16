using Content.Shared._Lua.Chat.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lua.Language;

[Prototype("language")]
public sealed class LanguagePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set;  } = default!;

    [DataField("obfuscation")]
    public ObfuscationMethod Obfuscation = ObfuscationMethod.Default;

    [DataField("speech")]
    public SpeechOverrideInfo SpeechOverride = new();

    public string Name => Loc.GetString($"language-{ID}-name");
    public string Description => Loc.GetString($"language-{ID}-description");
}

[DataDefinition]
public sealed partial class SpeechOverrideInfo
{
    [DataField]
    public Color? Color = null;

    [DataField]
    public string? FontId;

    [DataField]
    public int? FontSize;

    [DataField]
    public bool AllowRadio = true;

    [DataField]
    public bool RequireSpeech = true;

    [DataField]
    public InGameICChatType? ChatTypeOverride;

    [DataField]
    public List<LocId>? SpeechVerbOverrides;

    [DataField]
    public Dictionary<InGameICChatType, LocId> MessageWrapOverrides = new();
}


