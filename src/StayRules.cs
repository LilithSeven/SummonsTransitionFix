using System;
using System.Collections.Generic;
using System.Linq;

namespace SummonsTransitionFix
{
    public class StayEntry
    {
        public string GameId = string.Empty;

        public string UnitId = string.Empty;

        public string CreatureName = string.Empty;

        public string AreaName = string.Empty;
    }

    public class StaySnapshot
    {
        public string GameId = string.Empty;

        public long PlayedTicks;

        public long WrittenAtLocalTicks;

        public long WrittenAtUtcTicks;

        public List<StayEntry> Creatures = new List<StayEntry>();
    }

    public static class StayRules
    {
        public const int MaximumEntries = 300;
        public const int MaximumSnapshots = 1000;
        public const long PlayedTicksTolerance = TimeSpan.TicksPerMillisecond * 10;
        public const long WrittenAtTicksTolerance = TimeSpan.TicksPerSecond;

        public static bool IsStaying(IEnumerable<StayEntry?>? entries, string? gameId, string? unitId)
        {
            if (entries == null || string.IsNullOrEmpty(gameId) || string.IsNullOrEmpty(unitId)) return false;

            return entries.Any(entry => IsEntryFor(entry, gameId, unitId));
        }

        public static bool SetStaying(List<StayEntry>? entries, string? gameId, string? unitId, bool staying, string? creatureName = null, string? areaName = null)
        {
            if (entries == null || string.IsNullOrEmpty(gameId) || string.IsNullOrEmpty(unitId)) return false;

            if (staying == IsStaying(entries, gameId, unitId)) return false;

            entries.RemoveAll(entry => IsEntryFor(entry, gameId, unitId));

            if (staying)
            {
                entries.Add(Copy(gameId!, unitId!, creatureName, areaName));

                while (entries.Count > MaximumEntries)
                {
                    entries.RemoveAt(0);
                }
            }

            return true;
        }

        public static bool Describe(IEnumerable<StayEntry?>? entries, string? gameId, string? unitId, string? creatureName, string? areaName)
        {
            if (entries == null || string.IsNullOrEmpty(gameId) || string.IsNullOrEmpty(unitId)) return false;

            var entry = entries.FirstOrDefault(candidate => IsEntryFor(candidate, gameId, unitId));
            if (entry == null) return false;

            string newCreatureName = string.IsNullOrEmpty(creatureName) ? entry.CreatureName ?? string.Empty : creatureName!;
            string newAreaName = string.IsNullOrEmpty(areaName) ? entry.AreaName ?? string.Empty : areaName!;
            if (HasNames(entry, newCreatureName, newAreaName)) return false;

            entry.CreatureName = newCreatureName;
            entry.AreaName = newAreaName;

            return true;
        }

        public static List<StayEntry> ListAbsent(IEnumerable<StayEntry?>? entries, string? gameId, ICollection<string>? presentUnitIds)
        {
            if (string.IsNullOrEmpty(gameId)) return new List<StayEntry>();

            var absent = CreaturesOf(entries, gameId);
            if (presentUnitIds != null)
            {
                absent.RemoveAll(creature => presentUnitIds.Contains(creature.UnitId));
            }

            return absent;
        }

        public static bool RememberSave(
            List<StayEntry>? entries,
            List<StaySnapshot>? snapshots,
            string? gameId,
            long playedTicks,
            long writtenAtLocalTicks,
            long writtenAtUtcTicks)
        {
            if (entries == null || snapshots == null || string.IsNullOrEmpty(gameId)) return false;

            int removed = snapshots.RemoveAll(snapshot =>
                snapshot == null || IsSnapshotOf(snapshot, gameId, playedTicks, writtenAtLocalTicks, writtenAtUtcTicks));

            var creatures = CreaturesOf(entries, gameId);
            if (creatures.Count == 0) return removed > 0;

            foreach (var creature in creatures)
            {
                creature.GameId = string.Empty;
            }

            snapshots.Add(new StaySnapshot
            {
                GameId = gameId!,
                PlayedTicks = playedTicks,
                WrittenAtLocalTicks = writtenAtLocalTicks,
                WrittenAtUtcTicks = writtenAtUtcTicks,
                Creatures = creatures,
            });

            while (snapshots.Count > MaximumSnapshots)
            {
                snapshots.RemoveAt(0);
            }

            return true;
        }

        public static bool RestoreSave(
            List<StayEntry>? entries,
            IEnumerable<StaySnapshot?>? snapshots,
            string? gameId,
            long playedTicks,
            long writtenAtLocalTicks,
            long writtenAtUtcTicks)
        {
            if (entries == null || string.IsNullOrEmpty(gameId)) return false;

            var snapshot = snapshots?
                .Where(candidate => candidate != null && IsSnapshotOf(candidate, gameId, playedTicks, writtenAtLocalTicks, writtenAtUtcTicks))
                .OrderBy(candidate => Math.Abs(candidate!.WrittenAtUtcTicks - writtenAtUtcTicks))
                .FirstOrDefault();

            var saved = CreaturesOf(snapshot?.Creatures?.Select(creature => creature == null ? null : Copy(gameId!, creature.UnitId, creature.CreatureName, creature.AreaName)), gameId);
            var current = CreaturesOf(entries, gameId);

            int storedCount = entries.Count(entry => entry == null || IsSameGame(entry, gameId));
            if (storedCount == current.Count && DescribeTheSameCreatures(saved, current))
            {
                return false;
            }

            entries.RemoveAll(entry => entry == null || IsSameGame(entry, gameId));
            entries.AddRange(saved);

            return true;
        }

        public static bool ForgetGame(List<StayEntry>? entries, string? gameId)
        {
            if (entries == null) return false;

            return entries.RemoveAll(entry => entry == null || IsSameGame(entry, gameId)) > 0;
        }

        private static StayEntry Copy(string gameId, string? unitId, string? creatureName, string? areaName)
        {
            return new StayEntry
            {
                GameId = gameId,
                UnitId = unitId ?? string.Empty,
                CreatureName = creatureName ?? string.Empty,
                AreaName = areaName ?? string.Empty,
            };
        }

        private static List<StayEntry> CreaturesOf(IEnumerable<StayEntry?>? entries, string? gameId)
        {
            var creatures = new List<StayEntry>();
            if (entries == null) return creatures;

            foreach (var entry in entries)
            {
                if (entry == null || !IsSameGame(entry, gameId) || string.IsNullOrEmpty(entry.UnitId)) continue;
                if (creatures.Any(known => string.Equals(known.UnitId, entry.UnitId, StringComparison.Ordinal))) continue;

                creatures.Add(Copy(gameId ?? string.Empty, entry.UnitId, entry.CreatureName, entry.AreaName));
            }

            return creatures;
        }

        private static bool DescribeTheSameCreatures(List<StayEntry> first, List<StayEntry> second)
        {
            if (first.Count != second.Count) return false;

            foreach (var creature in first)
            {
                var twin = second.FirstOrDefault(candidate => string.Equals(candidate.UnitId, creature.UnitId, StringComparison.Ordinal));
                if (twin == null || !HasNames(twin, creature.CreatureName, creature.AreaName)) return false;
            }

            return true;
        }

        private static bool HasNames(StayEntry entry, string creatureName, string areaName)
        {
            return string.Equals(entry.CreatureName ?? string.Empty, creatureName, StringComparison.Ordinal)
                && string.Equals(entry.AreaName ?? string.Empty, areaName, StringComparison.Ordinal);
        }

        private static bool IsSnapshotOf(StaySnapshot? snapshot, string? gameId, long playedTicks, long writtenAtLocalTicks, long writtenAtUtcTicks)
        {
            if (snapshot == null) return false;
            if (!string.Equals(snapshot.GameId ?? string.Empty, gameId ?? string.Empty, StringComparison.Ordinal)) return false;
            if (!IsWithin(snapshot.PlayedTicks, playedTicks, PlayedTicksTolerance)) return false;

            return IsWithin(snapshot.WrittenAtLocalTicks, writtenAtLocalTicks, WrittenAtTicksTolerance)
                || IsWithin(snapshot.WrittenAtUtcTicks, writtenAtUtcTicks, WrittenAtTicksTolerance);
        }

        private static bool IsWithin(long first, long second, long tolerance)
        {
            long gap = first > second ? first - second : second - first;

            return gap >= 0 && gap <= tolerance;
        }

        private static bool IsEntryFor(StayEntry? entry, string? gameId, string? unitId)
        {
            return entry != null
                && string.Equals(entry.UnitId, unitId, StringComparison.Ordinal)
                && IsSameGame(entry, gameId);
        }

        private static bool IsSameGame(StayEntry entry, string? gameId)
        {
            return string.Equals(entry.GameId ?? string.Empty, gameId ?? string.Empty, StringComparison.Ordinal);
        }
    }
}
