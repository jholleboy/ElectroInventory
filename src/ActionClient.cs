using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using ElectroInventory.Wire;

namespace ElectroInventory
{
    internal sealed class ActionClient : IDisposable
    {
        private readonly int pid;
        private readonly bool diagnostics;
        private MemoryMappedFile map;
        private MemoryMappedViewAccessor view;
        private Mutex mutex;
        private long nextProbe, sent;
        private Guid pending;
        public Guid Revision { get; private set; }
        public ActionOffer[] Offers { get; private set; } = new ActionOffer[0];
        public bool NativeTradeOnly { get; private set; }
        public bool Busy => pending != Guid.Empty;
        public ActionResult Result { get; private set; }

        public ActionClient(int pid, bool diagnostics = false) { this.pid = pid; this.diagnostics = diagnostics; }

        public void Poll(long snapshotStamp, int rows)
        {
            try
            {
                long now = Stopwatch.GetTimestamp();
                if (view == null)
                {
                    if (now < nextProbe) return;
                    nextProbe = now + Stopwatch.Frequency;
                    string channel = ActionProtocol.Channel(pid);
                    mutex = Mutex.OpenExisting(channel + ".Lock");
                    map = MemoryMappedFile.OpenExisting(channel, MemoryMappedFileRights.ReadWrite);
                    view = map.CreateViewAccessor(0, ActionProtocol.Capacity);
                }
                if (!ActionProtocol.TryLock(mutex)) return;
                try
                {
                    if (view.ReadInt32(0) != ActionProtocol.Magic || view.ReadInt32(4) != pid)
                        throw new InvalidDataException();
                    view.Write(ActionProtocol.DiagnosticsFlag, diagnostics ? 1 : 0);
                    if (Busy && ActionProtocol.ReadGuid(view, 40) == pending)
                    {
                        var result = (ActionResult)view.ReadByte(56);
                        if (result >= ActionResult.Success && result <= ActionResult.Started)
                        { Result = result; pending = Guid.Empty; }
                    }
                    if (Busy && now - sent > Stopwatch.Frequency * 8)
                    { Result = ActionResult.Uncertain; pending = Guid.Empty; }
                    long stamp = view.ReadInt64(8);
                    if (stamp != snapshotStamp || !Protocol.Fresh(stamp, now))
                    {
                        Revision = Guid.Empty;
                        Offers = new ActionOffer[0];
                        return;
                    }
                    NativeTradeOnly = view.ReadInt32(ActionProtocol.FocusPolicy) == 2;
                    Guid revision = ActionProtocol.ReadGuid(view, 16);
                    if (revision == Revision) return;
                    int count = view.ReadInt32(32);
                    if (count < 0 || count > Protocol.MaxRows || count > rows)
                        throw new InvalidDataException();
                    var offers = new ActionOffer[count];
                    for (int i = 0; i < count; i++)
                    {
                        int offset = ActionProtocol.Offers + i * ActionProtocol.OfferSize;
                        var kinds = (ActionKind)view.ReadByte(offset + 16);
                        if (kinds != ActionKind.None)
                            offers[i] = new ActionOffer { Token = ActionProtocol.ReadGuid(view, offset),
                                Kinds = kinds, Maximum = view.ReadInt32(offset + 20) };
                    }
                    Offers = offers;
                    Revision = revision;
                }
                finally { mutex.ReleaseMutex(); }
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException ||
                e is UnauthorizedAccessException || e is WaitHandleCannotBeOpenedException)
            {
                if (Busy) Result = ActionResult.Uncertain;
                Dispose();
            }
        }

        // The RC2 grants only the MenuPool associated with this exact live session.
        public bool UpdateFocus(Guid session)
        {
            if (view == null || !ActionProtocol.TryLock(mutex)) return false;
            try
            {
                long now = Stopwatch.GetTimestamp();
                ActionProtocol.WriteGuid(view, ActionProtocol.FocusRequest, session);
                view.Write(ActionProtocol.FocusRequest + 16, session == Guid.Empty ? 0L : now);
                return ActionProtocol.HasFocus(session, ActionProtocol.ReadGuid(view, ActionProtocol.FocusGrant),
                    view.ReadInt64(ActionProtocol.FocusGrant + 16), now, ActionProtocol.FocusClientMargin);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { return false; }
            finally { mutex.ReleaseMutex(); }
        }

        public bool Submit(Guid session, Guid revision, ActionOffer offer, ActionKind kind, int quantity)
        {
            if (view == null || Busy || revision != Revision || offer == null) return false;
            var command = new ActionCommand { Id = Guid.NewGuid(), Session = session, Revision = revision,
                Token = offer.Token, Kind = kind, Quantity = quantity };
            if (!ActionProtocol.Authorize(command, session, Revision, offer) || !ActionProtocol.TryLock(mutex)) return false;
            try
            {
                if (view.ReadInt32(64) != 0 || ActionProtocol.ReadGuid(view, 16) != revision
                    || !Protocol.Fresh(view.ReadInt64(8), Stopwatch.GetTimestamp())) return false;
                ActionProtocol.WriteGuid(view, 68, command.Id);
                ActionProtocol.WriteGuid(view, 84, session);
                ActionProtocol.WriteGuid(view, 100, revision);
                ActionProtocol.WriteGuid(view, 116, offer.Token);
                view.Write(132, (byte)kind);
                view.Write(136, quantity);
                sent = Stopwatch.GetTimestamp();
                view.Write(140, sent);
                view.Write(64, 1);
                pending = command.Id;
                Result = ActionResult.Accepted;
                return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Result = ActionResult.Uncertain;
                return false;
            }
            finally { mutex.ReleaseMutex(); }
        }

        public void Dispose()
        {
            if (view != null) UpdateFocus(Guid.Empty);
            view?.Dispose(); map?.Dispose(); mutex?.Dispose();
            view = null; map = null; mutex = null;
            Revision = pending = Guid.Empty;
            Offers = new ActionOffer[0];
            NativeTradeOnly = false;
        }
    }
}
