using System.Linq;
using Content.Shared._Mono.Company;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Preferences;
using Robust.Client;
using Robust.Client.Player;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Prototypes;

namespace Content.Client.Lobby
{
    /// <summary>
    ///     Receives <see cref="PlayerPreferences" /> and <see cref="GameSettings" /> from the server during the initial
    ///     connection.
    ///     Stores preferences on the server through <see cref="SelectCharacter" /> and <see cref="UpdateCharacter" />.
    /// </summary>
    public sealed class ClientPreferencesManager : IClientPreferencesManager
    {
        [Dependency] private readonly IClientNetManager _netManager = default!;
        [Dependency] private readonly IBaseClient _baseClient = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;

        public event Action? OnServerDataLoaded;

        public GameSettings Settings { get; private set; } = default!;
        public PlayerPreferences Preferences { get; private set; } = default!;

        public void Initialize()
        {
            _netManager.RegisterNetMessage<MsgPreferencesAndSettings>(HandlePreferencesAndSettings);
            _netManager.RegisterNetMessage<MsgUpdateCharacter>();
            _netManager.RegisterNetMessage<MsgSelectCharacter>();
            _netManager.RegisterNetMessage<MsgDeleteCharacter>();

            _baseClient.RunLevelChanged += BaseClientOnRunLevelChanged;
        }

        private void BaseClientOnRunLevelChanged(object? sender, RunLevelChangedEventArgs e)
        {
            if (e.NewLevel == ClientRunLevel.Initialize)
            {
                Settings = default!;
                Preferences = default!;
            }
        }

        public void SelectCharacter(ICharacterProfile profile)
        {
            SelectCharacter(Preferences.IndexOfCharacter(profile));
        }

        public void SelectCharacter(int slot)
        {
            Preferences = new PlayerPreferences(Preferences.Characters, slot, Preferences.AdminOOCColor, Preferences.ConstructionFavorites);
            var msg = new MsgSelectCharacter
            {
                SelectedCharacterIndex = slot
            };
            _netManager.ClientSendMessage(msg);
        }

        public void UpdateCharacter(ICharacterProfile profile, int slot)
        {
            var collection = IoCManager.Instance!;

            // Verify company exists if this is a humanoid profile
            if (profile is HumanoidCharacterProfile humanoidProfile)
            {
                var protoManager = IoCManager.Resolve<IPrototypeManager>();
                if (!string.IsNullOrEmpty(humanoidProfile.Company) &&
                    humanoidProfile.Company != "None" &&
                    !protoManager.HasIndex<CompanyPrototype>(humanoidProfile.Company))
                {
                    profile = humanoidProfile.WithCompany("None");
                }
            }

            profile.EnsureValid(_playerManager.LocalSession!, collection);
            var characters = new Dictionary<int, ICharacterProfile>(Preferences.Characters) {[slot] = profile};
            Preferences = new PlayerPreferences(characters, Preferences.SelectedCharacterIndex, Preferences.AdminOOCColor, Preferences.ConstructionFavorites);
            var msg = new MsgUpdateCharacter
            {
                Profile = profile,
                Slot = slot
            };
            _netManager.ClientSendMessage(msg);
        }

        public void CreateCharacter(ICharacterProfile profile)
        {
            var characters = new Dictionary<int, ICharacterProfile>(Preferences.Characters);
            var lowest = Enumerable.Range(0, Settings.MaxCharacterSlots)
                .Except(characters.Keys)
                .FirstOrNull();

            if (lowest == null)
            {
                throw new InvalidOperationException("Out of character slots!");
            }

            var l = lowest.Value;
            characters.Add(l, profile);
            Preferences = new PlayerPreferences(characters, Preferences.SelectedCharacterIndex, Preferences.AdminOOCColor, Preferences.ConstructionFavorites);

            UpdateCharacter(profile, l);
        }

        public void DeleteCharacter(ICharacterProfile profile)
        {
            DeleteCharacter(Preferences.IndexOfCharacter(profile));
        }

        public void DeleteCharacter(int slot)
        {
            var characters = Preferences.Characters.Where(p => p.Key != slot);
            Preferences = new PlayerPreferences(characters, Preferences.SelectedCharacterIndex, Preferences.AdminOOCColor, Preferences.ConstructionFavorites);
            var msg = new MsgDeleteCharacter
            {
                Slot = slot
            };
            _netManager.ClientSendMessage(msg);
        }

        public void UpdateConstructionFavorites(List<ProtoId<ConstructionPrototype>> favorites)
        {
            Preferences = new PlayerPreferences(Preferences.Characters, Preferences.SelectedCharacterIndex, Preferences.AdminOOCColor, favorites);
            var msg = new MsgUpdateConstructionFavorites
            {
                Favorites = favorites
            };
            _netManager.ClientSendMessage(msg);
        }

        private void HandlePreferencesAndSettings(MsgPreferencesAndSettings message)
        {
            Preferences = message.Preferences;
            Settings = message.Settings;

            // Check if any character profiles have invalid companies and fix them
            if (Preferences != null)
            {
                var protoManager = IoCManager.Resolve<IPrototypeManager>();
                var needsUpdate = false;
                var characters = new Dictionary<int, ICharacterProfile>();

                foreach (var (slot, profile) in Preferences.Characters)
                {
                    var updatedProfile = profile;

                    if (profile is HumanoidCharacterProfile humanoidProfile &&
                        !string.IsNullOrEmpty(humanoidProfile.Company) &&
                        humanoidProfile.Company != "None" &&
                        !protoManager.HasIndex<CompanyPrototype>(humanoidProfile.Company))
                    {
                        updatedProfile = humanoidProfile.WithCompany("None");
                        needsUpdate = true;
                    }

                    characters[slot] = updatedProfile;
                }

                if (needsUpdate)
                {
                    Preferences = new PlayerPreferences(characters, Preferences.SelectedCharacterIndex, Preferences.AdminOOCColor, Preferences.ConstructionFavorites);

                    // Update the selected character on the server if needed
                    var selectedIndex = Preferences.SelectedCharacterIndex;
                    if (characters.TryGetValue(selectedIndex, out var selectedProfile))
                    {
                        var msg = new MsgUpdateCharacter
                        {
                            Profile = selectedProfile,
                            Slot = selectedIndex
                        };
                        _netManager.ClientSendMessage(msg);
                    }
                }
            }

            OnServerDataLoaded?.Invoke();
        }
    }
}
