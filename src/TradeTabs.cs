using System;
using System.Collections.Generic;
using System.Linq;
using ElectroInventory.Wire;

namespace ElectroInventory
{
    internal sealed class TradeTabs
    {
        public const byte Buy = 4, Sell = 5;
        public byte Current { get; private set; } = Buy;

        public static bool HasRows(Snapshot snapshot, byte group) => snapshot.Rows.Any(row => row.Group == group);
        public void Open(Snapshot snapshot) => Current = HasRows(snapshot, Buy) || !HasRows(snapshot, Sell) ? Buy : Sell;
        public bool Toggle(Snapshot snapshot)
        {
            byte next = Current == Buy ? Sell : Buy;
            if (!HasRows(snapshot, next)) return false;
            Current = next;
            return true;
        }

        public static bool CanAct(Entry row, ActionOffer offer)
        {
            ActionKind kind = row.Group == Buy ? ActionKind.Buy : row.Group == Sell ? ActionKind.Sell : ActionKind.None;
            return kind != ActionKind.None && !double.IsNaN(row.Price) && !double.IsInfinity(row.Price) &&
                row.Price >= 0 && offer != null && (offer.Kinds & kind) != 0 && offer.Maximum > 0;
        }

        public static int RestoreSelection(List<Entry> previous, int index, List<Entry> updated)
        {
            if (index < 0 || index >= previous.Count) return 0;
            Entry row = previous[index];
            if (string.IsNullOrEmpty(row.Id)) return 0;
            int match = updated.FindIndex(candidate => candidate.Group == row.Group && candidate.Id == row.Id);
            return match >= 0 && match == updated.FindLastIndex(candidate => candidate.Group == row.Group && candidate.Id == row.Id)
                ? match : 0;
        }
    }
}
