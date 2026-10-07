using System;
using System.Collections.Generic;
using System.Linq;

namespace SummonsTransitionFix
{
    public class StayEntry
    {
        public string GameId = string.Empty;

        public string UnitId = string.Empty;
    }

    public class StaySnapshot
    {
        public string GameId = string.Empty;

        public long PlayedTicks;

        public long WrittenAtLocalTicks;

        public long WrittenAtUtcTicks;

        public List<string> UnitIds = new List<string>();
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

        public static bool SetStaying(List<StayEntry>? entries, string? gameId, string? unitId, bool staying)
        {
            if (entries == null || string.IsNullOrEmpty(gameId) || string.IsNullOrEmpty(unitId)) return false;

            if (staying == IsStaying(entries, gameId, unitId)) return false;

            entries.RemoveAll(entry => IsEntryFor(entry, gameId, unitId));

            if (staying)
            {
                entries.Add(new StayEntry { GameId = gameId!, UnitId = unitId! });

                while (entries.Count > MaximumEntries)
                {
                    entries.RemoveAt(0);
                }
            }

            return true;
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

            var unitIds = UnitIdsOf(entries, gameId);
            if (unitIds.Count == 0) return removed > 0;

            snapshots.Add(new StaySnapshot
            {
                GameId = gameId!,
                PlayedTicks = playedTicks,
                WrittenAtLocalTicks = writtenAtLocalTicks,
                WrittenAtUtcTicks = writtenAtUtcTicks,
                UnitIds = unitIds,
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

            var savedUnitIds = (snapshot?.UnitIds ?? new List<string>())
                .Where(unitId => !string.IsNullOrEmpty(unitId))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var currentUnitIds = UnitIdsOf(entries, gameId);

            bool hasNullEntries = entries.Any(entry => entry == null);
            if (!hasNullEntries && savedUnitIds.Count == currentUnitIds.Count && !savedUnitIds.Except(currentUnitIds, StringComparer.Ordinal).Any())
            {
                return false;
            }

            entries.RemoveAll(entry => entry == null || IsSameGame(entry, gameId));
            entries.AddRange(savedUnitIds.Select(unitId => new StayEntry { GameId = gameId!, UnitId = unitId }));

            return true;
        }

        public static bool ForgetGame(List<StayEntry>? entries, string? gameId)
        {
            if (entries == null) return false;

            return entries.RemoveAll(entry => entry == null || IsSameGame(entry, gameId)) > 0;
        }

        private static List<string> UnitIdsOf(IEnumerable<StayEntry?> entries, string? gameId)
        {
            return entries
                .Where(entry => entry != null && IsSameGame(entry, gameId) && !string.IsNullOrEmpty(entry.UnitId))
                .Select(entry => entry!.UnitId)
                .Distinct(StringComparer.Ordinal)
                .ToList();
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
