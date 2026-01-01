using Robust.Shared.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.ContentPack;

namespace Content.IntegrationTests.Tests._Lua;

[TestFixture]
public sealed class SoundCollectionLuaTests
{
    [Test]
    public async Task FilesInCollectionsShouldExistTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var resourceManager = server.ResolveDependency<IResourceManager>();
        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var soundCollection in protoManager.EnumeratePrototypes<SoundCollectionPrototype>())
                {
                    if (soundCollection.PickFiles.Count == 0)
                    {
                        Assert.Fail($"Коллекция {soundCollection.ID} не содержит файлов.");
                    }
                    foreach (var file in soundCollection.PickFiles)
                    {
                        if (!resourceManager.ContentFileExists(file))
                        {
                            Assert.Fail($"Файл {file} из коллекции {soundCollection.ID} не существует.");
                        }
                    }
                }
            });
        });
        await pair.CleanReturnAsync();
    }
}
