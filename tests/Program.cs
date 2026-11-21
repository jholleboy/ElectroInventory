using System;
using System.Diagnostics;
using System.IO;
using ElectroInventory.Wire;

internal static class Program
{
    private static void Require(bool success)
    {
        if (!success) throw new Exception("Falha na verificação do protocolo.");
    }

    private static void Main()
    {
        var snapshot = new Snapshot { Session = Guid.NewGuid(), Character = 41, Title = "Inventário" };
        var empty = Protocol.Decode(Protocol.Encode(snapshot));
        Require(empty.Rows.Count == 0 && empty.Session == snapshot.Session);
        const uint highBitHandle = 0x80000001u;
        snapshot.Character = unchecked((int)highBitHandle);
        Require(unchecked((uint)Protocol.Decode(Protocol.Encode(snapshot)).Character) == highBitHandle);
        snapshot.Character = 41;
        snapshot.Rows.Add(new Entry { Id = "agua", Name = "Água mineral", Quantity = 1.25 });
        snapshot.Rows.Add(new Entry { Id = "agua", Name = "Água mineral", Quantity = .125, Group = 1,
            Description = new string('ã', 8000) + "🧪" });
        snapshot.Rows.Add(new Entry { Id = "arma", Name = new string('Í', 6000), Group = 2 });
        snapshot.Rows.Add(new Entry { Id = "cash", Name = "Dinheiro", Quantity = 500, Group = 3 });
        snapshot.Mode = 1;
        snapshot.BlocksInput = true;
        var storage = Protocol.Decode(Protocol.Encode(snapshot));
        Require(storage.BlocksInput && storage.Mode == 1 && storage.Rows.Count == 4
            && storage.Rows[0].Quantity == 1.25 && storage.Rows[1].Quantity == .125
            && storage.Rows[1].Id == storage.Rows[0].Id && storage.Rows[0].Name == "Água mineral"
            && storage.Rows[0].Description == "" && storage.Rows[2].Name.Length <= 1024);
        snapshot.Rows.Clear();
        snapshot.Rows.Add(new Entry { Group = 4, Name = "Arma", Detail = "Preço no menu original." });
        snapshot.Rows.Add(new Entry { Group = 5, Name = "Produto", Price = 33 });
        snapshot.Mode = 2;
        snapshot.Session = Guid.NewGuid();
        snapshot.Character++;
        var trade = Protocol.Decode(Protocol.Encode(snapshot));
        Require(trade.Mode == 2 && trade.Session != storage.Session && trade.Character != storage.Character
            && double.IsNaN(trade.Rows[0].Price) && trade.Rows[1].Price == 33);
        for (int i = 0; i < Protocol.MaxRows * 2; i++)
            snapshot.Rows.Add(new Entry { Name = new string('ç', 1024) });
        var payload = Protocol.Encode(snapshot);
        Require(payload.Length <= Protocol.Capacity - Protocol.Header && Protocol.Decode(payload).Incomplete);
        var shortPayload = new byte[payload.Length - 1];
        Array.Copy(payload, shortPayload, shortPayload.Length);
        bool rejected = false;
        try { Protocol.Decode(shortPayload); } catch (EndOfStreamException) { rejected = true; }
        Require(rejected);
        long now = Stopwatch.GetTimestamp();
        Require(Protocol.Fresh(now, now) && !Protocol.Fresh(0, now) &&
            !Protocol.Fresh(now - 3 * Stopwatch.Frequency, now) && !Protocol.Fresh(now + 1, now));
        Require(Protocol.Channel(41) != Protocol.Channel(42));
        Console.WriteLine("Protocolo: frações, UTF-8, duplicatas, vazio, truncamento, sessão, expiração e tamanho passaram.");
    }
}
