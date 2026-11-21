using System;
using System.Diagnostics;
using System.IO;
using System.Drawing;
using ElectroInventory.Wire;

internal static class Program
{
    private static void Require(bool success)
    {
        if (!success) throw new Exception("Falha na verificação do protocolo.");
    }

    private static void Main()
    {
        CheckOverlayInput();
        CheckActions();
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
    private static void CheckActions()
    {
        Guid session = Guid.NewGuid(), revision = Guid.NewGuid(), nextRevision = Guid.NewGuid();
        var offer = new ActionOffer { Token = Guid.NewGuid(), Kinds = ActionKind.Use | ActionKind.Discard, Maximum = 7 };
        var command = new ActionCommand { Id = Guid.NewGuid(), Token = offer.Token, Session = session,
            Revision = revision, Kind = ActionKind.Discard, Quantity = 5 };
        Require(ActionProtocol.Authorize(command, session, revision, offer));
        Require(!ActionProtocol.Authorize(command, session, nextRevision, offer)); // revision retired before execution
        Require(!ActionProtocol.Authorize(command, Guid.NewGuid(), revision, offer));
        command.Kind = ActionKind.Use | ActionKind.Discard;
        Require(!ActionProtocol.Authorize(command, session, revision, offer));
        command.Kind = ActionKind.Buy;
        Require(!ActionProtocol.Authorize(command, session, revision, offer));
        command.Kind = ActionKind.Use;
        Require(!ActionProtocol.Authorize(command, session, revision, offer));
        command.Quantity = 1;
        Require(ActionProtocol.Authorize(command, session, revision, offer));
        command.Token = Guid.NewGuid();
        Require(!ActionProtocol.Authorize(command, session, revision, offer));
        command.Token = offer.Token;
        command.Quantity = -1;
        Require(!ActionProtocol.Authorize(command, session, revision, offer));
        Require(ActionProtocol.ExactTransfer(3.25f, 1.125f, 2));
        Require(!ActionProtocol.ExactTransfer(.75f, 1, 1));
        Require(!ActionProtocol.ExactTransfer(3, 16777216, 1));
        Require(!ActionProtocol.ExactTransfer(float.NaN, 0, 1));
        Require(ActionProtocol.ValidPrice(5, 2, 10, true));
        Require(!ActionProtocol.ValidPrice(5, 3, 10, true));
        Require(!ActionProtocol.ValidPrice(int.MaxValue, 2, 1, false));
        var selection = new ElectroInventory.ActionSelection();
        selection.Bind(revision, offer);
        Require(selection.Kind == ActionKind.Use);
        selection.NextKind();
        selection.Adjust(1);
        Require(selection.Kind == ActionKind.Discard && selection.Quantity == 5);
        selection.Adjust(1);
        Require(selection.Quantity == 7);
        selection.Confirming = true;
        selection.Bind(nextRevision, offer);
        Require(!selection.Confirming && selection.Quantity == 1);
        Console.WriteLine("Ações: autorização, revisão aposentada, sessão, token, frações, preço e confirmação passaram.");
    }

    private static void CheckOverlayInput()
    {
        var guard = new ElectroInventory.PauseGuard();
        Require(!guard.Update(false, true)); // GTA owns Esc before opening.
        Require(guard.Update(true, false));
        Require(guard.Update(true, true)); // Closing frame.
        Require(guard.Update(false, true)); // Held after closing.
        Require(guard.Update(false, false)); // Release frame.
        Require(!guard.Update(false, false));
        Require(!guard.Update(false, true)); // Next press belongs to GTA.
        var logical = new Size(1400, 760);
        foreach (float scale in new[] { .7f, 1f, 1.75f })
        {
            var panel = new RectangleF(250, 100, logical.Width * scale, logical.Height * scale);
            Require(ElectroInventory.OverlayPointer.TryMap(panel.Left + panel.Width / 2,
                panel.Top + panel.Height / 2, panel, logical, out Point mapped)
                && mapped == new Point(700, 380));
            Require(!ElectroInventory.OverlayPointer.TryMap(panel.Left - .1f, panel.Top, panel, logical, out _));
            Require(!ElectroInventory.OverlayPointer.TryMap(panel.Right, panel.Bottom, panel, logical, out _));
        }
        Require(!ElectroInventory.OverlayPointer.TryMap(float.NaN, 10, RectangleF.Empty, logical, out _));
        Console.WriteLine("Entrada: Esc pressionado/solto e mapeamento do cursor passaram.");
    }

}
