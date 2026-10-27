using System;
using System.Collections.Generic;
using System.Linq;

namespace Riftline
{
    public sealed class GameProgress
    {
        private readonly GameCatalog catalog;
        private readonly GameSaveStore store;
        private readonly HashSet<string> settledRuns;
        private readonly Dictionary<string, RunResult> pendingResults = new Dictionary<string, RunResult>();

        public SaveData Save { get; private set; }
        public bool HasPendingSave { get; private set; }
        public string LastPersistenceError { get; private set; } = string.Empty;

        public bool IsEndlessUnlocked
        {
            get { return Save.ContractStars.Length > 5 && Save.ContractStars[5] > 0; }
        }

        public GameProgress(GameCatalog catalog, SaveData save, GameSaveStore store)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            Save = save ?? throw new ArgumentNullException(nameof(save));
            settledRuns = new HashSet<string>(Save.SettledRuns ?? Array.Empty<string>());
        }

        public bool IsRegionUnlocked(int region)
        {
            if (region < 0 || region > 2)
            {
                return false;
            }
            if (region == 0)
            {
                return true;
            }

            int previous = region - 1;
            return catalog.Contracts
                .Where(contract => contract.Region == previous)
                .Count(contract => contract.Id >= 0
                    && contract.Id < Save.ContractStars.Length
                    && Save.ContractStars[contract.Id] > 0) >= 5;
        }

        public bool IsContractUnlocked(int contractId)
        {
            ContractDefinition contract = catalog.Contracts.FirstOrDefault(item => item.Id == contractId);
            return contract != null && IsRegionUnlocked(contract.Region);
        }

        public void CollectArchive(int archiveId)
        {
            TryCollectArchive(archiveId);
        }

        public bool TryCollectArchive(int archiveId)
        {
            if (archiveId < 0 || archiveId >= Save.Archives.Length)
            {
                return false;
            }

            if (!Save.Archives[archiveId])
            {
                Save.Archives[archiveId] = true;
                HasPendingSave = true;
            }
            return TrySavePending();
        }

        public bool TrySavePending()
        {
            if (!HasPendingSave) return true;
            try
            {
                store.Save(Save);
                HasPendingSave = false;
                LastPersistenceError = string.Empty;
                foreach (RunResult result in pendingResults.Values) result.PersistencePending = false;
                pendingResults.Clear();
                return true;
            }
            catch (Exception error)
            {
                if (!(error is System.IO.IOException) && !(error is UnauthorizedAccessException))
                    throw;
                LastPersistenceError = error.GetType().Name + ": " + error.Message;
                return false;
            }
        }

        public bool TrySettleRun(RunSimulation simulation, out RunResult result)
        {
            result = SettleRun(simulation);
            return !result.PersistencePending;
        }

        public RunResult SettleRun(RunSimulation simulation)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }
            if (simulation.Phase != RunPhase.Completed && simulation.Phase != RunPhase.Failed)
            {
                throw new InvalidOperationException("Only completed or failed runs can be settled.");
            }

            RunResult result = simulation.CreateResult();
            RunResult pending;
            if (pendingResults.TryGetValue(result.SettlementId, out pending))
            {
                TrySavePending();
                return pending;
            }
            if (string.IsNullOrEmpty(result.SettlementId) || settledRuns.Contains(result.SettlementId))
            {
                TrySavePending();
                result.FirstClearReward = 0;
                result.AlreadySettled = true;
                result.PersistencePending = HasPendingSave;
                if (result.Victory && !result.Endless)
                    result.Stars = CalculateStars(catalog.Contracts.First(item => item.Id == result.ContractId), result);
                return result;
            }

            settledRuns.Add(result.SettlementId);
            Save.Chips += Math.Max(0, result.ChipsEarned);

            if (result.Victory && !result.Endless && result.ContractId >= 0 && result.ContractId < Save.ContractStars.Length)
            {
                bool firstClear = Save.ContractStars[result.ContractId] == 0;
                ContractDefinition contract = catalog.Contracts.First(item => item.Id == result.ContractId);
                result.Stars = CalculateStars(contract, result);
                Save.ContractStars[result.ContractId] = Math.Max(Save.ContractStars[result.ContractId], result.Stars);
                if (firstClear)
                {
                    result.FirstClearReward = contract.FirstClearReward;
                    Save.Chips += result.FirstClearReward;
                }
            }

            if (result.Endless && !simulation.UsedAutoTurn)
            {
                Save.EndlessBestScore = Math.Max(Save.EndlessBestScore, result.Score);
            }

            var orderedSettlements = new List<string>(Save.SettledRuns ?? Array.Empty<string>());
            orderedSettlements.Remove(result.SettlementId);
            orderedSettlements.Add(result.SettlementId);
            Save.SettledRuns = orderedSettlements.ToArray();
            HasPendingSave = true;
            result.PersistencePending = true;
            pendingResults.Add(result.SettlementId, result);
            TrySavePending();
            return result;
        }

        public bool BuyOutfit(int outfitId)
        {
            OutfitDefinition outfit = catalog.Outfits.FirstOrDefault(item => item.Id == outfitId);
            if (outfit == null || outfitId < 0 || outfitId >= Save.OwnedOutfits.Length)
            {
                return false;
            }
            if (Save.OwnedOutfits[outfitId])
            {
                return TrySavePending();
            }
            if (Save.Chips < outfit.Cost)
            {
                return false;
            }

            Save.Chips -= outfit.Cost;
            Save.OwnedOutfits[outfitId] = true;
            HasPendingSave = true;
            return TrySavePending();
        }

        public void EquipOutfit(int outfitId)
        {
            if (outfitId < 0 || outfitId >= Save.OwnedOutfits.Length || !Save.OwnedOutfits[outfitId])
            {
                return;
            }

            Save.EquippedOutfit = outfitId;
            HasPendingSave = true;
            TrySavePending();
        }

        public static int CalculateStars(ContractDefinition contract, RunResult result)
        {
            int stars = 1;
            if (result.Score >= contract.StarScore)
            {
                stars++;
            }
            if (result.Score >= contract.StarScore
                && result.ChipsEarned >= contract.ObjectiveChips
                && result.NearMisses >= contract.ObjectiveNearMisses)
            {
                stars++;
            }
            return stars;
        }
    }
}
