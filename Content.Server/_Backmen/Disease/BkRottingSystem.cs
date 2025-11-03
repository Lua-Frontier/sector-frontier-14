using Content.Shared.Backmen.Disease;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Disease;

public sealed class BkRottingSystem : SharedBkRottingSystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    public readonly List<ProtoId<DiseasePrototype>> MiasmaDiseasePool = new()
    {
        "VentCough",
        "AMIV",
        "SpaceCold",
        "SpaceFlu",
        "BirdFlew",
        //"VanAusdallsRobovirus",
        "BleedersBite",
        "Plague",
        "TongueTwister",
        "MemeticAmirmir"
    };

    private string _poolDisease = "";
    private TimeSpan _diseaseTime = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _poolRepickTime = TimeSpan.FromMinutes(5);


    public override void Initialize()
    {
        base.Initialize();
        _poolDisease = _random.Pick(MiasmaDiseasePool);
    }

    public override string RequestPoolDisease()
    {
        _diseaseTime = _timing.CurTime + _poolRepickTime;
        return _poolDisease;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime >= _diseaseTime)
        {
            _diseaseTime = _timing.CurTime + _poolRepickTime;
            _poolDisease = _random.Pick(MiasmaDiseasePool);
        }
    }
}
