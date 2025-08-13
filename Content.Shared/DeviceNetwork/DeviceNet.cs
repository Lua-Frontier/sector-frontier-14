//REV
using Robust.Shared.Random;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Content.Shared.DeviceNetwork.Components;

namespace Content.Shared.DeviceNetwork;

/// <summary>
///     Data class for storing and retrieving information about devices connected to a device network.
/// </summary>
/// <remarks>
///     This basically just makes <see cref="DeviceNetworkComponent"/> accessible via their addresses and frequencies on
///     some network.
/// </remarks>
public sealed class DeviceNet
{
    /// <summary>
    ///     Devices, mapped by their "Address", which is just an int that gets converted to Hex for displaying to users.
    ///     This dictionary contains all devices connected to this network, though they may not be listening to any
    ///     specific frequency.
    /// </summary>
    public readonly Dictionary<string, DeviceNetworkComponent> Devices = new();

    /// <summary>
    ///     Devices listening on a given frequency.
    /// </summary>
    public readonly Dictionary<uint, HashSet<DeviceNetworkComponent>> ListeningDevices = new();

    /// <summary>
    ///     Devices listening to all packets on a given frequency, regardless of the intended recipient.
    /// </summary>
    public readonly Dictionary<uint, HashSet<DeviceNetworkComponent>> ReceiveAllDevices = new();

    private readonly IRobustRandom _random;
    private ISawmill _logger = default!;
    public readonly int NetId;

    public DeviceNet(int netId, IRobustRandom random, ISawmill logger)
    {
        _random = random;
        NetId = netId;
        _logger = logger;

    }

    /// <summary>
    ///     Add a device to the network.
    /// </summary>
    public bool Add(DeviceNetworkComponent device)
    {
        if (device == null)
        {
            _logger.Error($"Attempted to add null device to network {NetId}");
            return false;
        }

        try
        {
            if (device.CustomAddress)
            {
                if (string.IsNullOrWhiteSpace(device.Address))
                {
                    _logger.Error($"Device has custom address but it's null/empty. Network: {NetId}");
                    return false;
                }

                return Devices.TryAdd(device.Address, device);
            }

            if (string.IsNullOrWhiteSpace(device.Address) || Devices.ContainsKey(device.Address))
            {
                device.Address = GenerateValidAddressWithFallback(device.Prefix);
                if (device.Address == null)
                {
                    _logger.Error($"Failed to generate address for device. Network: {NetId}");
                    return false;
                }
            }

            Devices[device.Address] = device;
            UpdateFrequencySubscriptions(device);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to add device to network {NetId}. Error: {ex}");
            return false;
        }
    }

    private string GenerateValidAddressWithFallback(string? prefix)
    {
        const int maxAttempts = 10;
        int attempts = 0;

        while (attempts < maxAttempts)
        {
            attempts++;
            try
            {
                var address = GenerateValidAddress(prefix);
                if (!Devices.ContainsKey(address))
                    return address;
            }
            catch (Exception ex)
            {
                _logger.Warning($"Address generation failed (attempt {attempts}). Error: {ex}");
            }
        }

        try
        {
            return $"FALLBACK-{_random.Next():X8}-{Guid.NewGuid():N}";
        }
        catch
        {
            return $"EMG-{Guid.NewGuid():N}";
        }
    }

    /// <summary>
    ///     Remove a device from the network.
    /// </summary>
    public bool Remove(DeviceNetworkComponent device)
    {
        if (device == null || device.Address == null)
        {
            _logger.Error($"Attempted to remove null device or device with null address from network {NetId}");
            return false;
        }

        try
        {
            if (!Devices.Remove(device.Address))
                return false;

            RemoveFromFrequencySubscriptions(device);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to remove device {device.Address} from network {NetId}. Error: {ex}");
            return false;
        }
    }

    private void RemoveFromFrequencySubscriptions(DeviceNetworkComponent device)
    {
        if (device.ReceiveFrequency is not uint freq)
            return;

        try
        {
            if (ListeningDevices.TryGetValue(freq, out var listeners))
            {
                listeners.Remove(device);
                if (listeners.Count == 0)
                    ListeningDevices.Remove(freq);
            }

            if (device.ReceiveAll && ReceiveAllDevices.TryGetValue(freq, out var receiveAll))
            {
                receiveAll.Remove(device);
                if (receiveAll.Count == 0)
                    ReceiveAllDevices.Remove(freq);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to remove device {device.Address} from frequency subscriptions. Error: {ex}");
        }
    }

    /// <summary>
    ///     Give an existing device a new randomly generated address. Useful if the device's address prefix was updated
    ///     and they want a new address to reflect that, or something like that.
    /// </summary>
    public bool RandomizeAddress(string oldAddress, string? prefix = null)
    {
        if (!Devices.Remove(oldAddress, out var device))
            return false;

        device.Address = GenerateValidAddress(prefix ?? device.Prefix);
        device.CustomAddress = false;
        Devices[device.Address] = device;
        return true;
    }

    /// <summary>
    ///     Update the address of an existing device.
    /// </summary>
    public bool UpdateAddress(string oldAddress, string newAddress)
    {
        if (Devices.ContainsKey(newAddress))
            return false;

        if (!Devices.Remove(oldAddress, out var device))
            return false;

        device.Address = newAddress;
        device.CustomAddress = true;
        Devices[newAddress] = device;
        return true;
    }

    /// <summary>
    ///     Make an existing network device listen to a new frequency.
    /// </summary>
    public bool UpdateReceiveFrequency(string address, uint? newFrequency)
    {
        if (!Devices.TryGetValue(address, out var device))
            return false;

        if (device.ReceiveFrequency == newFrequency)
            return true;

        if (device.ReceiveFrequency is uint freq)
        {
            if (ListeningDevices.TryGetValue(freq, out var listening))
            {
                listening.Remove(device);
                if (listening.Count == 0)
                    ListeningDevices.Remove(freq);
            }

            if (device.ReceiveAll && ReceiveAllDevices.TryGetValue(freq, out var receiveAll))
            {
                receiveAll.Remove(device);
                if (receiveAll.Count == 0)
                    ListeningDevices.Remove(freq);
            }
        }

        device.ReceiveFrequency = newFrequency;

        if (newFrequency == null)
            return true;

        if (!ListeningDevices.TryGetValue(newFrequency.Value, out var devices))
            ListeningDevices[newFrequency.Value] = devices = new();

        devices.Add(device);

        if (!device.ReceiveAll)
            return true;

        if (!ReceiveAllDevices.TryGetValue(newFrequency.Value, out var receiveAlldevices))
            ReceiveAllDevices[newFrequency.Value] = receiveAlldevices = new();

        receiveAlldevices.Add(device);
        return true;
    }

    /// <summary>
    ///     Make an existing network device listen to a new frequency.
    /// </summary>
    public bool UpdateReceiveAll(string address, bool receiveAll)
    {
        if (!Devices.TryGetValue(address, out var device))
            return false;

        if (device.ReceiveAll == receiveAll)
            return true;

        device.ReceiveAll = receiveAll;

        if (device.ReceiveFrequency is not uint freq)
            return true;

        // remove or add to set of listening devices

        HashSet<DeviceNetworkComponent>? devices;
        if (receiveAll)
        {
            if (!ReceiveAllDevices.TryGetValue(freq, out devices))
                ReceiveAllDevices[freq] = devices = new();
            devices.Add(device);
        }
        else if (ReceiveAllDevices.TryGetValue(freq, out devices))
        {
            devices.Remove(device);
            if (devices.Count == 0)
                ReceiveAllDevices.Remove(freq);
        }

        return true;
    }

    /// <summary>
    ///     Generates a valid address by randomly generating one and checking if it already exists on the network.
    /// </summary>
    private string GenerateValidAddress(string? prefix)
    {
        string? processedPrefix = null;
        if (!string.IsNullOrWhiteSpace(prefix))
        {
            try
            {
                processedPrefix = Loc.GetString(prefix).Trim();
            }
            catch (Exception ex)
            {
                _logger.Warning($"Localization failed for prefix '{prefix}'. Using raw value. Error: {ex}");
                processedPrefix = prefix;
            }
        }

        try
        {
            var num1 = _random.Next();
            var num2 = _random.Next();
            var num3 = _random.Next();

            return processedPrefix != null
                ? $"{processedPrefix}-{num1:X4}-{num2:X4}-{num3:X4}"
                : $"{num1:X4}-{num2:X4}-{num3:X4}";
        }
        catch (Exception ex)
        {
            _logger.Error($"Primary address generation failed. Error: {ex}");
            throw;
        }
    }

    private void UpdateFrequencySubscriptions(DeviceNetworkComponent device)
    {
        try
        {
            if (device.ReceiveFrequency is not uint freq)
                return;

            if (!ListeningDevices.TryGetValue(freq, out var listeners))
            {
                listeners = new HashSet<DeviceNetworkComponent>();
                ListeningDevices[freq] = listeners;
            }
            listeners.Add(device);

            if (device.ReceiveAll)
            {
                if (!ReceiveAllDevices.TryGetValue(freq, out var receiveAll))
                {
                    receiveAll = new HashSet<DeviceNetworkComponent>();
                    ReceiveAllDevices[freq] = receiveAll;
                }
                receiveAll.Add(device);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to update frequency subscriptions for device {device.Address}. Error: {ex}");
        }
    }
}
