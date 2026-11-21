using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.IO.MemoryMappedFiles;
using System.Threading;
using ElectroInventory.Wire;

namespace ElectroInventory
{
    internal interface IDataSource : IDisposable
    {
        Snapshot Read();
    }

    internal sealed class UnavailableDataSource : IDataSource
    {
        public Snapshot Read() => null;
        public void Dispose() { }
    }

    internal sealed class DemoDataSource : IDataSource
    {
        private readonly Snapshot sample = new Snapshot { Session = Guid.NewGuid(), Character = 1, Title = "Demonstração local",
            Rows = { new Entry { Id = "demo-water", Name = "Água mineral", Category = "Bebidas", Quantity = 2.5,
                Description = "Exemplo fictício de pilha fracionária." } } };
        public Snapshot Read() => sample;
        public void Dispose() { }
    }

    internal sealed class LsrReadOnlyDataSource : IDataSource
    {
        private readonly int pid;
        private readonly Action<string> report;
        private Mutex mutex;
        private MemoryMappedFile map;
        private MemoryMappedViewAccessor view;
        private Snapshot cached;
        private byte[] previousPayload;
        private long stamp, probeAfter;
        private string previous;
        private Guid reportedSession;
        private int reportedCount = -1;

        public LsrReadOnlyDataSource(int pid, Action<string> report) { this.pid = pid; this.report = report; }

        public Snapshot Read()
        {
            long now = Stopwatch.GetTimestamp();
            if (view == null && now < probeAfter) return null;
            try
            {
                if (view == null)
                {
                    probeAfter = now + Stopwatch.Frequency;
                    string channel = Protocol.Channel(pid);
                    mutex = Mutex.OpenExisting(channel + ".Lock");
                    map = MemoryMappedFile.OpenExisting(channel, MemoryMappedFileRights.Read);
                    view = map.CreateViewAccessor(0, Protocol.Capacity, MemoryMappedFileAccess.Read);
                }
                bool held = false;
                try
                {
                    try { held = mutex.WaitOne(0); }
                    catch (AbandonedMutexException) { held = true; throw new IOException("Publicação interrompida."); }
                    if (!held) return Protocol.Fresh(stamp, now) ? cached : null;
                    if (view.ReadInt32(0) != Protocol.Magic || view.ReadInt32(4) != Protocol.Version || view.ReadInt32(8) != pid)
                        throw new InvalidDataException("Cabeçalho ou versão incompatível.");
                    long next = view.ReadInt64(16);
                    if (next == 0)
                    {
                        DisposeSnapshot("Sessão encerrada.");
                        return null;
                    }
                    if (!Protocol.Fresh(next, Stopwatch.GetTimestamp()))
                        throw new IOException("Snapshot expirado.");
                    if (stamp == next) return cached;
                    int length = view.ReadInt32(12);
                    if (length <= 0 || length > Protocol.Capacity - Protocol.Header)
                        throw new InvalidDataException("Tamanho inválido.");
                    var bytes = new byte[length];
                    view.ReadArray(Protocol.Header, bytes, 0, length);
                    if (cached == null || previousPayload == null || !bytes.SequenceEqual(previousPayload))
                        cached = Protocol.Decode(bytes);
                    previousPayload = bytes;
                    stamp = next;
                    if (reportedSession != cached.Session || reportedCount != cached.Rows.Count)
                    {
                        reportedSession = cached.Session;
                        reportedCount = cached.Rows.Count;
                        Report($"RC2, protocolo {Protocol.Version}, contexto {cached.Mode}, {reportedCount} linhas.");
                    }
                    return cached;
                }
                finally { if (held) mutex.ReleaseMutex(); }
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is ArgumentException || e is UnauthorizedAccessException || e is WaitHandleCannotBeOpenedException)
            {
                Dispose();
                DisposeSnapshot(e is FileNotFoundException || e is WaitHandleCannotBeOpenedException
                    ? "LS RED indisponível." : e is InvalidDataException ? "Snapshot inválido ou incompatível." : "Leitura interrompida ou snapshot vencido.");
                probeAfter = now + Stopwatch.Frequency;
                return null;
            }
        }

        private void DisposeSnapshot(string reason)
        {
            stamp = 0;
            cached = null;
            previousPayload = null;
            reportedSession = Guid.Empty;
            Report(reason);
        }
        private void Report(string message) { if (message != previous) { previous = message; report(message); } }
        public void Dispose() { view?.Dispose(); map?.Dispose(); mutex?.Dispose(); view = null; map = null; mutex = null; stamp = 0; cached = null; previousPayload = null; }
    }
}
