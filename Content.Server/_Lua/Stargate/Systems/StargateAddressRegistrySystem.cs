// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.Stargate.Components;
using Content.Shared.GameTicking;
using Robust.Shared.Random;

namespace Content.Server._Lua.Stargate.Systems;

public sealed class StargateAddressRegistrySystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    private int _roundSeed;

    private readonly Dictionary<string, (EntityUid MapUid, int Seed)> _registry = new();
    private readonly HashSet<string> _assignedPrivateKeys = new();

    private readonly List<byte[]> _addressPool = new();
    private int _nextPoolIndex;

    private const int PoolSize = 35187; // 99.999 valide percent, using 39 symbols, lenght 6.
    private const int AddressLength = 6;
    private const byte MinSymbol = 2;
    private const byte MaxSymbol = 40;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundCleanup);
        _roundSeed = _random.Next();
        GeneratePool();
    }

    private void OnRoundCleanup(RoundRestartCleanupEvent ev)
    {
        _registry.Clear();
        _assignedPrivateKeys.Clear();
        _roundSeed = _random.Next();
        GeneratePool();
    }

    private void GeneratePool()
    {
        _addressPool.Clear();
        _nextPoolIndex = 0;

        var usedKeys = new HashSet<string>();
        var random = new Random(_roundSeed);
        var symbols = new List<byte>();
        for (byte s = MinSymbol; s <= MaxSymbol; s++)
            symbols.Add(s);

        while (_addressPool.Count < PoolSize)
        {
            var address = new byte[AddressLength];
            var available = new List<byte>(symbols);

            for (var i = 0; i < AddressLength; i++)
            {
                var idx = random.Next(available.Count);
                address[i] = available[idx];
                available.RemoveAt(idx);
            }

            var key = AddressToKey(address);
            if (usedKeys.Add(key))
                _addressPool.Add(address);
        }
    }

    public void AssignAddress(EntityUid gateUid, StargateComponent gate)
    {
        if (gate.Address != null)
            return;

        if (_nextPoolIndex < _addressPool.Count)
        {
            gate.Address = _addressPool[_nextPoolIndex];
            _nextPoolIndex++;
        }
        else
        {
            var address = GenerateRandomAddress();
            gate.Address = address;
        }
    }

    public void AssignPrivateAddress(EntityUid gateUid, StargateComponent gate)
    {
        if (gate.Address != null)
            return;

        for (var attempt = 0; attempt < 1000; attempt++)
        {
            var address = GenerateRandomAddress();
            var key = AddressToKey(address);

            if (IsPoolAddress(address) || !_assignedPrivateKeys.Add(key))
                continue;

            gate.Address = address;
            return;
        }

        gate.Address = GenerateRandomAddress();
    }

    private byte[] GenerateRandomAddress()
    {
        var address = new byte[AddressLength];
        var available = new List<byte>();
        for (byte s = MinSymbol; s <= MaxSymbol; s++)
            available.Add(s);

        for (var i = 0; i < AddressLength; i++)
        {
            var idx = _random.Next(available.Count);
            address[i] = available[idx];
            available.RemoveAt(idx);
        }

        return address;
    }

    public static string AddressToKey(byte[] address)
    {
        return string.Join("-", address);
    }

    public int ComputeSeed(byte[] address)
    {
        var hash = _roundSeed;
        foreach (var symbol in address)
        {
            hash = unchecked(hash * 31 + symbol);
        }
        return hash;
    }

    public bool TryGetDestination(byte[] address, out EntityUid mapUid, out int seed)
    {
        var key = AddressToKey(address);
        if (_registry.TryGetValue(key, out var entry))
        {
            mapUid = entry.MapUid;
            seed = entry.Seed;
            return true;
        }

        mapUid = EntityUid.Invalid;
        seed = 0;
        return false;
    }

    public void RegisterDestination(byte[] address, EntityUid mapUid, int seed)
    {
        var key = AddressToKey(address);
        _registry[key] = (mapUid, seed);
    }

    public void UnregisterDestination(byte[] address)
    {
        var key = AddressToKey(address);
        _registry.Remove(key);
    }

    public int GetPoolSize()
    {
        return _addressPool.Count;
    }

    public byte[]? GetRandomPoolAddress()
    {
        if (_addressPool.Count == 0)
            return null;

        return _addressPool[_random.Next(_addressPool.Count)];
    }

    public bool IsPoolAddress(byte[] address)
    {
        var key = AddressToKey(address);
        foreach (var poolAddr in _addressPool)
        {
            if (AddressToKey(poolAddr) == key)
                return true;
        }
        return false;
    }

    public static bool ValidateAddress(byte[] symbols, byte addressLength, byte symbolCount)
    {
        if (symbols.Length != addressLength)
            return false;

        var seen = new HashSet<byte>();
        foreach (var s in symbols)
        {
            if (s < 1 || s > symbolCount)
                return false;
            if (!seen.Add(s))
                return false;
        }

        return true;
    }
}
