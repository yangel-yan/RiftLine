using System;
using System.Collections.Generic;

namespace Riftline
{
    public enum RunPhase
    {
        Menu,
        Countdown,
        Running,
        Paused,
        Failed,
        Completed
    }

    public enum GameCommand
    {
        None,
        Left,
        Right,
        Jump,
        Slide,
        Phase,
        Pause,
        Comfort,
        Confirm,
        Back
    }

    public enum ObstacleKind
    {
        Crate,
        LowBarrier,
        HighBeam,
        MovingGate,
        Drone,
        Gap,
        PhaseWall,
        Collapse,
        Chip,
        Archive,
        TurnLeft,
        TurnRight,
        Fork,
        Rise,
        Fall,
        Boss
    }

    public enum FeedbackKind
    {
        Jump,
        Slide,
        Phase,
        PhaseReady,
        Chip,
        Archive,
        NearMiss,
        Turn,
        Fail,
        Complete,
        Ui,
        Damage,
        Combo
    }

    public enum TrackTemplateCategory
    {
        Straight,
        Turn,
        Fork,
        Height,
        Obstacle,
        Boss
    }

    [Serializable]
    public sealed class ContractDefinition
    {
        public int Id;
        public int Region;
        public string NameZh;
        public string NameEn;
        public string DescriptionZh;
        public string DescriptionEn;
        public float Length;
        public float DurationSeconds;
        public int Seed;
        public int FirstClearReward;
        public int StarScore;
        public int ArchiveId;
        public bool Boss;
        public int ObjectiveChips;
        public int ObjectiveNearMisses;
        public int[] BranchArchiveIds;

        public ContractDefinition Clone()
        {
            var copy = (ContractDefinition)MemberwiseClone();
            copy.BranchArchiveIds = BranchArchiveIds == null ? null : (int[])BranchArchiveIds.Clone();
            return copy;
        }
    }

    [Serializable]
    public sealed class ArchiveDefinition
    {
        public int Id;
        public int Region;
        public string NameZh;
        public string NameEn;
        public string TextZh;
        public string TextEn;
    }

    [Serializable]
    public sealed class OutfitDefinition
    {
        public int Id;
        public int Cost;
        public string NameZh;
        public string NameEn;
        public string DescriptionZh;
        public string DescriptionEn;
        public string ColorHex;
    }

    [Serializable]
    public sealed class TrackTemplateDefinition
    {
        public int Id;
        public string Name;
        public TrackTemplateCategory Category;
        public ObstacleKind Kind;
        public int Difficulty;
        public float Spacing;
        public bool RandomPool;
        public bool RequiresJump;
        public bool RequiresSlide;
        public bool Moving;
        public int Direction;
    }

    [Serializable]
    public sealed class GameSettings
    {
        public string Language = "zh";
        public int ComfortMask = 2;
        public int AlignmentMode;
        public int TriggerHand;
        public bool Seated = true;
        public bool LeftHanded;
        public bool SwapJumpSlide;
        public bool Haptics = true;
        public bool HighContrast = true;
        public bool ReducedFlash = true;
        public bool ReducedParticles;
        public bool ReducedFog;
        public bool RestReminder = true;
        public bool AutoTurn;
        public bool Subtitles = true;
        public float HapticIntensity = .6f;
        public float MusicVolume = .45f;
        public float SfxVolume = .8f;
        public float FontScale = 1f;
        public float RunnerDistance = 5.5f;
        public bool HandTutorialComplete;
        public float HandDirectionDistance = .035f;

        public GameSettings Clone() { return (GameSettings)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class SaveData
    {
        public int Chips;
        public int EquippedOutfit;
        public int EndlessBestScore;
        public int Revision;
        public int[] ContractStars = new int[18];
        public bool[] OwnedOutfits = { true, false, false, false, false, false, false, false };
        public bool[] Archives = new bool[30];
        public GameSettings Settings = new GameSettings();
        public string[] SettledRuns = Array.Empty<string>();

        public SaveData Clone()
        {
            var copy = (SaveData)MemberwiseClone();
            copy.ContractStars = (int[])(ContractStars ?? Array.Empty<int>()).Clone();
            copy.OwnedOutfits = (bool[])(OwnedOutfits ?? Array.Empty<bool>()).Clone();
            copy.Archives = (bool[])(Archives ?? Array.Empty<bool>()).Clone();
            copy.SettledRuns = (string[])(SettledRuns ?? Array.Empty<string>()).Clone();
            copy.Settings = (Settings ?? new GameSettings()).Clone();
            return copy;
        }
    }

    [Serializable]
    public sealed class RunResult
    {
        public bool Victory;
        public bool Endless;
        public int Score;
        public int ChipsEarned;
        public int FirstClearReward;
        public int Stars;
        public int ContractId;
        public int NearMisses;
        public float Distance;
        public string FailureReason;
        public string SettlementId;
        public bool PersistencePending;
        public bool AlreadySettled;
        public float DurationSeconds;
    }

    [Serializable]
    public sealed class TrackEvent
    {
        public int Id;
        public int Lane;
        public int ArchiveId = -1;
        public float Distance;
        public ObstacleKind Kind;
        public bool Resolved;
        public bool Passed;
        public bool TurnCommitted;
        public int TemplateId;
        public int Branch;
        public float Width = 1.2f;
        public int SafeLane = 1;
        public float WarningAngle;
        public bool PhaseCharge;
        public float Depth;
        public bool Entered;
        public bool NearMissAwarded;
        public bool HitRecorded;
        public int SelectedBranch;
        public int ForkId = -1;
        public int RequiredRoute;
        public int Region;
        public int CombinationBudget;
        public float WarningSeconds = 2f;
        public float ObserverClearance = 5.5f;
        public GameCommand RequiredAction;
        public int TutorialLesson = -1;
        public bool BossRecipe;
        // Road elevation is independent of jump clearance and never drives the HMD.
        public float GroundHeight;
        public float GroundTargetHeight;
        public float GroundTransitionStartDistance;
        public float GroundTransitionSeconds;

        public float GroundHeightAtDistance(float distance)
        {
            if (GroundTransitionSeconds <= 0f) return GroundHeight;
            double age = RunSimulation.TimeAtDistance(distance)
                - RunSimulation.TimeAtDistance(GroundTransitionStartDistance);
            double t = Math.Max(0d, Math.Min(1d, age / GroundTransitionSeconds));
            return (float)(GroundHeight + (GroundTargetHeight - GroundHeight) * t * t * (3d - 2d * t));
        }
    }

    [Serializable]
    public sealed class ReplayEntry
    {
        public float Elapsed;
        public float Distance;
        public GameCommand Command;
        public int EventId = -1;
        public string Outcome;
    }

    internal static class CollectionUtility
    {
        public static T[] ResizePreserving<T>(T[] source, int length)
        {
            var result = new T[length];
            if (source != null)
            {
                Array.Copy(source, result, Math.Min(source.Length, length));
            }
            return result;
        }

        public static void AddBounded<T>(List<T> list, T value, int maximum)
        {
            list.Add(value);
            if (list.Count > maximum)
            {
                list.RemoveRange(0, list.Count - maximum);
            }
        }
    }
}
