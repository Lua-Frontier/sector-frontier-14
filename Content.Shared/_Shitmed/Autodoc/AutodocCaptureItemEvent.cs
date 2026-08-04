namespace Content.Shared._Shitmed.Autodoc;

/// <summary>
/// Raised on an autodoc when surgery removes an item that should be captured for storage.
/// </summary>
[ByRefEvent]
public readonly record struct AutodocCaptureItemEvent(EntityUid Item);
