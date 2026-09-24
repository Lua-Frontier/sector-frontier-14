using Robust.Shared.Serialization;
using Content.Shared._NF.Research; // Frontier

namespace Content.Shared.Research.Components
{
    [NetSerializable, Serializable]
    public enum ResearchConsoleUiKey : byte
    {
        Key,
    }

    [Serializable, NetSerializable]
    public sealed class ConsoleUnlockTechnologyMessage : BoundUserInterfaceMessage
    {
        public string Id;

        public ConsoleUnlockTechnologyMessage(string id)
        {
            Id = id;
        }
    }

    [Serializable, NetSerializable]
    public sealed class ConsoleServerSelectionMessage : BoundUserInterfaceMessage
    {

    }

    [Serializable, NetSerializable]
    public sealed class ConsoleCancelProjectMessage : BoundUserInterfaceMessage
    {
        public string Id;

        public ConsoleCancelProjectMessage(string id)
        {
            Id = id;
        }
    }

    [Serializable, NetSerializable]
    public sealed class ResearchProjectUiState
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public int Progress;
        public int Cost;
        public bool Started;
        public int RemainingSeconds;
        public string CurrentStepId = string.Empty;
        public int StepProgress;
        public int StepCost;
        public int PathDone;
        public int PathTotal;
        public List<string> RecipeIds = new();
        public bool CanCancel;
    }

    [Serializable, NetSerializable]
    public sealed class ResearchConsoleBoundInterfaceState : BoundUserInterfaceState
    {
        public int Points;

        public string? ResearchFaction;

        /// <summary>
        /// Frontier field - all researches and their availablities
        /// </summary>
        public Dictionary<string, ResearchAvailability> Researches;

        public List<ResearchProjectUiState> ActiveProjects = new();

        public List<ResearchProjectUiState> QueuedProjects = new();

        public int MaxActiveSlots = 4;

        public Dictionary<string, int> TechnologyProgress = new();

        public ResearchConsoleBoundInterfaceState(
            int points,
            Dictionary<string, ResearchAvailability> researches,
            string? researchFaction = null,
            List<ResearchProjectUiState>? activeProjects = null,
            List<ResearchProjectUiState>? queuedProjects = null,
            int maxActiveSlots = 4,
            Dictionary<string, int>? technologyProgress = null)
        {
            Points = points;
            Researches = researches; // Frontier R&D console rework
            ResearchFaction = researchFaction;
            ActiveProjects = activeProjects ?? new List<ResearchProjectUiState>();
            QueuedProjects = queuedProjects ?? new List<ResearchProjectUiState>();
            MaxActiveSlots = maxActiveSlots;
            TechnologyProgress = technologyProgress ?? new Dictionary<string, int>();
        }
    }
}
