// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.GameObjects;

namespace Content.Lua.Shared.Shuttles;

public interface IIFFDecryptionSystem : IEntitySystem
{
    float Range { get; }
    IFFDecryptResult Get(EntityUid viewerGrid, EntityUid targetGrid, string realName, float distance, bool hideLabel);
}

public readonly record struct IFFDecryptResult(IFFDecryptPhase Phase, string Revealed, string Cipher);

public enum IFFDecryptPhase : byte
{
    Plain,
    Unknown,
    Decrypting,
    Known
}
