namespace Content.Server.Backmen.Chat;
// wtf
public sealed class NyanoChatSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
    }
}

public sealed class AfterPsionicChat : HandledEntityEventArgs
{
    public EntityUid Source { get; set; }
    public string MessageWrap { get; set; } = default!;
    public string Message { get; set; } = default!;
    public bool HideChat { get; set; }
}
