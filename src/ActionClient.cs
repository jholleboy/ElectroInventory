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
        private MemoryMappedFile map;
        private MemoryMappedViewAccessor view;
        private Mutex mutex;
        private long nextProbe, sent;
        private Guid pending;
        public Guid Revision { get; private set; }
        public ActionOffer[] Offers { get; private set; } = new ActionOffer[0];
        public bool Busy => pending != Guid.Empty;
        public ActionResult Result { get; private set; }

        public ActionClient(int pid) { this.pid = pid; }

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
            view?.Dispose(); map?.Dispose(); mutex?.Dispose();
            view = null; map = null; mutex = null;
            Revision = pending = Guid.Empty;
            Offers = new ActionOffer[0];
        }
    }
}
