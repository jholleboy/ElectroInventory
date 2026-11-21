using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace ElectroInventory.Wire
{
    [Flags]
    internal enum ActionKind : byte { None = 0, Use = 1, Discard = 2, Deposit = 4, Take = 8, Buy = 16, Sell = 32 }
    internal enum ActionResult : byte { None, Accepted, Success, Rejected, Expired, Uncertain, Cancelled, Started }

    internal sealed class ActionOffer
    {
        public Guid Token;
        public ActionKind Kinds;
        public int Maximum;
    }

    internal sealed class ActionCommand
    {
        public Guid Id, Session, Revision, Token;
        public ActionKind Kind;
        public int Quantity;
    }

    // The v1 snapshot remains unchanged. This separate channel binds offers to its exact publication stamp.
    internal static class ActionProtocol
    {
        public const int Magic = 0x45494132, Capacity = 65536, Offers = 160, OfferSize = 24;
        public const int FocusRequest = 49000, FocusGrant = 49024, DiagnosticsFlag = 49048, FocusPolicy = 49052;
        public const int EquipmentStamp = 49100, EquipmentLength = 49108, EquipmentPayload = 49112;
        public const int EquipmentCapacity = Capacity - EquipmentPayload;
        public static Snapshot ReadEquipment(byte[] payload, Guid session, int character)
        {
            if (payload == null || payload.Length == 0 || payload.Length > EquipmentCapacity) return null;
            var value = Protocol.Decode(payload);
            return value.Session == session && value.Character == character && value.Mode == 0 ? value : null;
        }

        public static readonly long FocusLifetime = System.Diagnostics.Stopwatch.Frequency * 3 / 4;
        public static readonly long FocusClientMargin = System.Diagnostics.Stopwatch.Frequency / 10;

        public static bool OwnsPool(bool lease, object registered, object candidate) =>
            lease && registered != null && ReferenceEquals(registered, candidate);

        public static bool VisibleMenuTree<T>(T root, Func<T, bool> visible, Func<T, IEnumerable<T>> children) where T : class
        {
            if (root == null) return false;
            var visited = new HashSet<T>();
            var remaining = new Stack<T>();
            remaining.Push(root);
            while (remaining.Count > 0)
            {
                var menu = remaining.Pop();
                if (menu == null || !visited.Add(menu)) continue;
                if (visible(menu)) return true;
                foreach (var child in children(menu)) remaining.Push(child);
            }
            return false;
        }

        public static bool HasFocus(Guid session, Guid grant, long expires, long now, long margin = 0) =>
            session != Guid.Empty && session == grant && expires > now && expires - now > margin;

        public static bool CanGrantFocus(bool eligible, Guid session, Guid request, long requested, long now) =>
            eligible && session != Guid.Empty && session == request && requested > 0
            && requested <= now && now - requested < FocusLifetime;

        public static string Channel(int pid) => @"Local\ElectroInventory.Lsr.actions.v2." + pid;

        public static bool Authorize(ActionCommand command, Guid session, Guid revision, ActionOffer offer) =>
            command != null && command.Id != Guid.Empty && session != Guid.Empty && revision != Guid.Empty
            && command.Session == session && command.Revision == revision && offer != null
            && offer.Token != Guid.Empty && command.Token == offer.Token
            && ValidKind(command.Kind) && (offer.Kinds & command.Kind) != 0
            && command.Quantity > 0 && command.Quantity <= offer.Maximum
            && (command.Kind != ActionKind.Use || command.Quantity == 1);

        public static bool ExactTransfer(float from, float to, int quantity)
        {
            if (quantity <= 0 || float.IsNaN(from) || float.IsInfinity(from) ||
                float.IsNaN(to) || float.IsInfinity(to) || to < 0 || from < quantity)
                return false;
            // Reject amounts where float arithmetic would lose a fractional residue.
            return (double)(from - quantity) == (double)from - quantity &&
                (double)(to + quantity) == (double)to + quantity;
        }

        public static bool ValidPrice(int unit, int quantity, int balance, bool buy)
        {
            if (unit < 0 || quantity <= 0 || balance < 0) return false;
            long total = (long)unit * quantity;
            return buy ? total <= balance : total <= int.MaxValue - (long)balance;
        }

        private static bool ValidKind(ActionKind kind) => kind == ActionKind.Use || kind == ActionKind.Discard
            || kind == ActionKind.Deposit || kind == ActionKind.Take || kind == ActionKind.Buy || kind == ActionKind.Sell;

        public static Guid ReadGuid(MemoryMappedViewAccessor view, int offset)
        {
            var bytes = new byte[16];
            view.ReadArray(offset, bytes, 0, bytes.Length);
            return new Guid(bytes);
        }

        public static void WriteGuid(MemoryMappedViewAccessor view, int offset, Guid guid)
        {
            var bytes = guid.ToByteArray();
            view.WriteArray(offset, bytes, 0, bytes.Length);
        }

        public static bool TryLock(Mutex mutex)
        {
            try { return mutex.WaitOne(0); }
            catch (AbandonedMutexException) { mutex.ReleaseMutex(); return false; }
        }
    }
}
