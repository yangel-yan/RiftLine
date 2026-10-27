using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Riftline
{
    public sealed class GameSaveStore
    {
        private const int SlotCount = 3;
        private const int EnvelopeVersion = 1;

        [Serializable]
        private sealed class SaveEnvelope
        {
            public int Version;
            public long Sequence;
            public string Payload;
            public string Checksum;
        }

        private readonly string directory;

        public GameSaveStore(string directory = null)
        {
            this.directory = string.IsNullOrWhiteSpace(directory)
                ? Path.Combine(Application.persistentDataPath, "Riftline")
                : directory;
        }

        public SaveData Load()
        {
            SaveEnvelope newest = null;
            for (int slot = 0; slot < SlotCount; slot++)
            {
                SaveEnvelope candidate;
                if (TryReadEnvelope(SlotPath(slot), out candidate)
                    && (newest == null || candidate.Sequence > newest.Sequence))
                {
                    newest = candidate;
                }
            }

            if (newest == null)
            {
                return Normalize(new SaveData());
            }

            try
            {
                SaveData data = JsonUtility.FromJson<SaveData>(newest.Payload);
                return Normalize(data ?? new SaveData());
            }
            catch (Exception)
            {
                // The checksum validates bytes, but a future or manually edited payload may still be invalid JSON.
                return LoadNextValidBefore(newest.Sequence);
            }
        }

        public void Save(SaveData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            Directory.CreateDirectory(directory);
            SaveData candidate = Normalize(data.Clone());
            candidate.Revision = Math.Max(0, data.Revision) + 1;

            long sequence = HighestValidSequence() + 1;
            string payload = JsonUtility.ToJson(candidate);
            var envelope = new SaveEnvelope
            {
                Version = EnvelopeVersion,
                Sequence = sequence,
                Payload = payload,
                Checksum = ComputeChecksum(EnvelopeVersion, sequence, payload)
            };

            string destination = SlotPath((int)(sequence % SlotCount));
            string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonUtility.ToJson(envelope), new UTF8Encoding(false));
                ReplaceAtomically(temporary, destination);
                data.Revision = candidate.Revision;
            }
            finally
            {
                DeleteBestEffort(temporary);
            }
        }

        private SaveData LoadNextValidBefore(long sequence)
        {
            SaveEnvelope fallback = null;
            for (int slot = 0; slot < SlotCount; slot++)
            {
                SaveEnvelope candidate;
                if (TryReadEnvelope(SlotPath(slot), out candidate)
                    && candidate.Sequence < sequence
                    && (fallback == null || candidate.Sequence > fallback.Sequence))
                {
                    fallback = candidate;
                }
            }

            if (fallback == null)
            {
                return Normalize(new SaveData());
            }

            try
            {
                return Normalize(JsonUtility.FromJson<SaveData>(fallback.Payload) ?? new SaveData());
            }
            catch (Exception)
            {
                return LoadNextValidBefore(fallback.Sequence);
            }
        }

        private long HighestValidSequence()
        {
            long highest = 0;
            for (int slot = 0; slot < SlotCount; slot++)
            {
                SaveEnvelope envelope;
                if (TryReadEnvelope(SlotPath(slot), out envelope))
                {
                    highest = Math.Max(highest, envelope.Sequence);
                }
            }
            return highest;
        }

        private bool TryReadEnvelope(string path, out SaveEnvelope envelope)
        {
            envelope = null;
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                SaveEnvelope candidate = JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(path, Encoding.UTF8));
                if (candidate == null
                    || candidate.Version != EnvelopeVersion
                    || candidate.Sequence <= 0
                    || string.IsNullOrEmpty(candidate.Payload)
                    || !FixedTimeEquals(candidate.Checksum, ComputeChecksum(candidate.Version, candidate.Sequence, candidate.Payload)))
                {
                    return false;
                }

                // Validate the payload before it can suppress an older, usable slot.
                if (JsonUtility.FromJson<SaveData>(candidate.Payload) == null)
                {
                    return false;
                }

                envelope = candidate;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private string SlotPath(int slot)
        {
            return Path.Combine(directory, "save-" + slot + ".json");
        }

        private static SaveData Normalize(SaveData data)
        {
            data.ContractStars = CollectionUtility.ResizePreserving(data.ContractStars, 18);
            data.OwnedOutfits = CollectionUtility.ResizePreserving(data.OwnedOutfits, 8);
            data.OwnedOutfits[0] = true;
            data.Archives = CollectionUtility.ResizePreserving(data.Archives, 30);
            data.Settings = data.Settings ?? new GameSettings();
            if (data.Settings.HandDirectionDistance <= 0f)
                data.Settings.HandDirectionDistance = .035f;
            data.Settings.HandDirectionDistance = Mathf.Clamp(data.Settings.HandDirectionDistance, .025f, .06f);
            string[] validRuns = (data.SettledRuns ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct()
                .ToArray();
            data.SettledRuns = validRuns;
            data.Chips = Math.Max(0, data.Chips);
            data.EndlessBestScore = Math.Max(0, data.EndlessBestScore);
            data.EquippedOutfit = data.EquippedOutfit >= 0
                && data.EquippedOutfit < data.OwnedOutfits.Length
                && data.OwnedOutfits[data.EquippedOutfit]
                    ? data.EquippedOutfit
                    : 0;
            for (int index = 0; index < data.ContractStars.Length; index++)
            {
                data.ContractStars[index] = Math.Max(0, Math.Min(3, data.ContractStars[index]));
            }
            return data;
        }

        private static string ComputeChecksum(int version, long sequence, string payload)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(version + "|" + sequence + "|" + payload);
            using (SHA256 sha = SHA256.Create())
            {
                return Convert.ToBase64String(sha.ComputeHash(bytes));
            }
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            byte[] a = Encoding.UTF8.GetBytes(left);
            byte[] b = Encoding.UTF8.GetBytes(right);
            if (a.Length != b.Length)
            {
                return false;
            }

            int difference = 0;
            for (int index = 0; index < a.Length; index++)
            {
                difference |= a[index] ^ b[index];
            }
            return difference == 0;
        }

        private static void ReplaceAtomically(string temporary, string destination)
        {
            if (!File.Exists(destination))
            {
                File.Move(temporary, destination);
                return;
            }

            string backup = destination + ".bak";
            try
            {
                File.Replace(temporary, destination, backup, true);
                DeleteBestEffort(backup);
            }
            catch (PlatformNotSupportedException)
            {
                ReplaceWithMove(temporary, destination, backup);
            }
            catch (IOException)
            {
                ReplaceWithMove(temporary, destination, backup);
            }
        }

        private static void ReplaceWithMove(string temporary, string destination, string backup)
        {
            if (File.Exists(backup))
            {
                File.Delete(backup);
            }
            File.Move(destination, backup);
            try
            {
                File.Move(temporary, destination);
                DeleteBestEffort(backup);
            }
            catch
            {
                if (!File.Exists(destination) && File.Exists(backup))
                {
                    File.Move(backup, destination);
                }
                throw;
            }
        }

        private static void DeleteBestEffort(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
