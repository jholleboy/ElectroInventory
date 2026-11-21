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
        CheckNativeCommerce();
        CheckOverlayInput();
        CheckV4();
        CheckV31();
        CheckV3();
        CheckFocusAndClicks();
        CheckActions();
        CheckTrade();
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

    private static void CheckTrade()
    {
        var tabs = new ElectroInventory.TradeTabs();
        var shop = new Snapshot { Mode = 2, Session = Guid.NewGuid(), Character = 1, Balance = 10 };
        var buy = new Entry { Group = 4, Id = "item", Price = 4, Quantity = 2 };
        var sell = new Entry { Group = 5, Id = "item", Price = 2, Quantity = 1 };
        shop.Rows.Add(buy);
        tabs.Open(shop);
        Require(!tabs.Toggle(shop) && tabs.Current == 4);
        shop.Rows.Clear();
        shop.Rows.Add(sell);
        tabs.Open(shop);
        Require(!tabs.Toggle(shop) && tabs.Current == 5);
        shop.Rows.Add(buy);
        Require(tabs.Toggle(shop) && tabs.Current == 4 && tabs.Toggle(shop) && tabs.Current == 5);
        Require(ElectroInventory.TradeTabs.RestoreSelection(new System.Collections.Generic.List<Entry> { sell }, 0,
            new System.Collections.Generic.List<Entry> { new Entry { Group = 5, Id = "other" }, sell }) == 1);

        var offer = new ActionOffer { Token = Guid.NewGuid(), Kinds = ActionKind.Buy, Maximum = 2 };
        Require(ElectroInventory.TradeTabs.CanAct(buy, offer) && !ElectroInventory.TradeTabs.CanAct(sell, offer));
        buy.Price = double.NaN;
        Require(!ElectroInventory.TradeTabs.CanAct(buy, offer));
        Require(!TradeRules.QuoteAllows(true, 4, 5, 1, 2, 10, true));
        Require(!TradeRules.QuoteAllows(true, 4, 4, 3, 2, 100, true));
        Require(!TradeRules.QuoteAllows(false, 2, 2, 1, 2, 10, false));
        Require(TradeRules.SellLimit(.5f, -1, 0) == 0 && TradeRules.BuyLimit(10, 3, 4) == 0);

        Guid revision = Guid.NewGuid();
        var command = new ActionCommand { Id = Guid.NewGuid(), Session = shop.Session, Revision = revision,
            Token = offer.Token, Kind = ActionKind.Buy, Quantity = 1 };
        Require(ActionProtocol.Authorize(command, shop.Session, revision, offer));
        Require(!ActionProtocol.Authorize(command, shop.Session, Guid.NewGuid(), offer));
        Require(!ActionProtocol.Authorize(command, Guid.Empty, revision, offer));

        // Model the balances and counters that the original executors update, in both orders.
        foreach (bool buyFirst in new[] { true, false })
        {
            int balance = 10, held = 1, sold = 0, bought = 0;
            foreach (bool buying in new[] { buyFirst, !buyFirst })
            {
                int max = buying ? TradeRules.BuyLimit(10, 2, sold) : TradeRules.SellLimit(held, 2, bought);
                int price = buying ? 4 : 2;
                Require(TradeRules.QuoteAllows(true, price, price, 1, max, balance, buying));
                if (buying) { sold++; held++; balance -= price; }
                else { bought++; held--; balance += price; }
            }
            Require(balance == 8 && held == 1 && sold == 1 && bought == 1);
        }
        Console.WriteLine("Comércio: abas, cotação, limites, sequências modeladas e rejeição de repetição passaram.");
    }

    private static void CheckFocusAndClicks()
    {
        var session = Guid.NewGuid();
        long now = Stopwatch.GetTimestamp();
        Require(ActionProtocol.CanGrantFocus(true, session, session, now, now));
        Require(!ActionProtocol.CanGrantFocus(false, session, session, now, now));
        Require(!ActionProtocol.CanGrantFocus(true, session, Guid.NewGuid(), now, now));
        Require(!ActionProtocol.CanGrantFocus(true, session, session, now, now + ActionProtocol.FocusLifetime));
        Require(ActionProtocol.HasFocus(session, session, now + ActionProtocol.FocusLifetime, now));
        Require(!ActionProtocol.HasFocus(session, Guid.Empty, now + ActionProtocol.FocusLifetime, now));

        var snapshot = new Snapshot { Mode = 2 };
        snapshot.Rows.Add(new Entry { Group = 4, Price = 3, Id = "buy" });
        snapshot.Rows.Add(new Entry { Group = 5, Price = 2, Id = "sell" });
        var tabs = new ElectroInventory.TradeTabs();
        var targets = new ElectroInventory.HitTargets();
        var pointer = new ElectroInventory.PointerCapture();
        int selectedCard = 0, confirmed = 0;
        targets.Add(new Rectangle(0, 0, 100, 50), () => tabs.Select(snapshot, 4));
        targets.Add(new Rectangle(100, 0, 100, 50), () => tabs.Select(snapshot, 5));
        targets.Add(new Rectangle(0, 50, 200, 70), () => selectedCard++);
        targets.Add(new Rectangle(200, 50, 100, 70), () => confirmed++);
        foreach (byte mode in new byte[] { 0, 1, 2 })
        {
            bool owns = mode == 0 || ActionProtocol.HasFocus(session, session, now + ActionProtocol.FocusLifetime, now);
            Require(owns);
            Require(!pointer.Click(false, true) && !pointer.Click(owns, true)); // native press crossed the handoff
            Require(!pointer.Click(owns, false) && pointer.Click(owns, true) && !pointer.Click(owns, true));
        }
        Require(targets.Click(new Point(110, 10)) && tabs.Current == 5);
        Require(targets.Click(new Point(10, 10)) && tabs.Current == 4);
        Require(targets.Click(new Point(20, 70)) && selectedCard == 1);
        Require(targets.Click(new Point(240, 60)) && confirmed == 1);
        targets.Clear();
        Require(!targets.Click(new Point(240, 60)) && confirmed == 1);
        Require(!ActionProtocol.HasFocus(session, session, now, now)); // pool can resume
        Console.WriteLine("Foco: concessão, cliques, perda da sessão e devolução passaram.");
    }

    private static void CheckV3()
    {
        var session = Guid.NewGuid();
        Require(ElectroInventory.OverlaySession.Matches(session, session, 41, 41, true, false));
        Require(!ElectroInventory.OverlaySession.Matches(session, Guid.Empty, 41, 41, true, false));
        Require(!ElectroInventory.OverlaySession.Matches(session, session, 41, 42, true, false));
        Require(!ElectroInventory.OverlaySession.Matches(session, session, 41, 41, false, false));
        Require(ElectroInventory.OverlaySession.CloseRequested(true, false, true, false, false, false, false));
        var requestedTrade = Guid.NewGuid();
        Require(ElectroInventory.OverlaySession.HasNewTradeOpen(true, requestedTrade, Guid.Empty));
        Require(!ElectroInventory.OverlaySession.HasNewTradeOpen(true, requestedTrade, requestedTrade));
        Require(!ElectroInventory.OverlaySession.HasNewTradeOpen(false, requestedTrade, Guid.Empty));
        Require(ElectroInventory.OverlaySession.CanOpenPersonal(true, 0, false, true, false));
        Require(!ElectroInventory.OverlaySession.CanOpenPersonal(true, 1, false, true, false));
        Require(!ElectroInventory.OverlaySession.CanOpenPersonal(true, 2, false, true, false));
        Require(!ElectroInventory.OverlaySession.CanOpenPersonal(true, 0, true, true, false));
        Require(!ElectroInventory.OverlaySession.CanOpenPersonal(true, 0, false, false, false));
        Require(!ElectroInventory.OverlaySession.CanOpenPersonal(true, 0, false, true, true));
        Require(ElectroInventory.OverlaySession.CloseRequested(false, true, false, true, false, false, false));
        Require(!ElectroInventory.OverlaySession.CloseRequested(false, true, false, true, false, true, false));
        Require(!ElectroInventory.OverlaySession.CloseRequested(true, true, false, true, false, false, false));
        Require(!ElectroInventory.OverlaySession.CloseRequested(true, true, true, false, false, true, false));
        Require(!ElectroInventory.OverlaySession.CloseRequested(true, false, false, false, true, false, false));
        Require(ElectroInventory.OverlaySession.CloseRequested(true, true, false, false, true, false, false));
        Require(!ElectroInventory.OverlaySession.CloseRequested(true, true, false, false, true, true, false));

        var design = new Size(1400, 760);
        foreach (var size in new[] { new Size(1280, 720), new Size(1920, 1080), new Size(1920, 1200), new Size(3440, 1440) })
        {
            var bounds = ElectroInventory.OverlayViewport.Fit(size, design);
            Require(bounds.Width > 0 && bounds.Height > 0 && bounds.Left > 0 && bounds.Top > 0);
            Require(Math.Abs(bounds.Width / bounds.Height - 1400f / 760f) < .001);
            Require(ElectroInventory.OverlayPointer.TryMap(size.Width / 2f, size.Height / 2f, bounds, design, out var center));
            Require(Math.Abs(center.X - 700) <= 1 && Math.Abs(center.Y - 380) <= 1);
        }
        Require(ElectroInventory.OverlayViewport.Fit(Size.Empty, design).IsEmpty);

        var offer = new ActionOffer { Token = Guid.NewGuid(), Kinds = ActionKind.Buy, Maximum = 4 };
        var row = new Entry { Group = 4, Price = 13 };
        Require(ElectroInventory.TradeTabs.Total(row, offer, 3) == 39);
        Require(ElectroInventory.TradeTabs.Total(row, offer, 5) == null);
        Require(ElectroInventory.TradeTabs.Total(row, null, 1) == null);
        Require(ActionProtocol.ExactTransfer(5.25f, 2.5f, 3));
        Require(!ActionProtocol.ExactTransfer(2.25f, 2.5f, 3));
        Console.WriteLine("V3: sessões, atalhos/Esc, personagem, diferentes proporções e totais passaram.");
    }

    private sealed class MenuNode
    {
        internal bool Visible;
        internal readonly System.Collections.Generic.List<MenuNode> Children = new System.Collections.Generic.List<MenuNode>();
    }

    private static void CheckV31()
    {
        var nativePool = new object();
        var unrelatedPool = new object();
        Require(ActionProtocol.OwnsPool(true, nativePool, nativePool));
        Require(!ActionProtocol.OwnsPool(true, nativePool, unrelatedPool));
        Require(!ActionProtocol.OwnsPool(false, nativePool, nativePool));
        var root = new MenuNode();
        var child = new MenuNode();
        var detail = new MenuNode();
        root.Children.Add(child);
        child.Children.Add(detail);
        root.Visible = true;
        Require(ActionProtocol.VisibleMenuTree(root, menu => menu.Visible, menu => menu.Children));
        root.Visible = false;
        detail.Visible = true;
        Require(ActionProtocol.VisibleMenuTree(root, menu => menu.Visible, menu => menu.Children));
        detail.Visible = false;
        Require(!ActionProtocol.VisibleMenuTree(root, menu => menu.Visible, menu => menu.Children));

        var session = Guid.NewGuid();
        var snapshot = new Snapshot { Mode = 2, Character = 27, Session = session };
        var buy = new Entry { Group = 4, Price = 12 };
        var sell = new Entry { Group = 5, Price = 8 };
        snapshot.Rows.Add(buy);
        snapshot.Rows.Add(sell);
        var tabs = new ElectroInventory.TradeTabs();
        tabs.Open(snapshot);
        Require(tabs.Toggle(snapshot) && tabs.Current == 5);
        Require(!ElectroInventory.TradeTabs.CanAct(buy, null) && !ElectroInventory.TradeTabs.CanAct(sell, null));
        snapshot.Rows.Remove(buy);
        tabs.Open(snapshot);
        Require(tabs.Current == 5 && !tabs.Toggle(snapshot));

        var revision = Guid.NewGuid();
        foreach (var direction in new[] { ActionKind.Deposit, ActionKind.Take })
        {
            var offer = new ActionOffer { Kinds = direction, Maximum = 2, Token = Guid.NewGuid() };
            var command = new ActionCommand { Id = Guid.NewGuid(), Kind = direction, Quantity = 1,
                Session = session, Revision = revision, Token = offer.Token };
            Require(ActionProtocol.Authorize(command, session, revision, offer));
            Require(ActionProtocol.ExactTransfer(2.5f, 1, command.Quantity));
            Require(!ActionProtocol.ExactTransfer(.5f, 1, command.Quantity));
            Require(!ActionProtocol.Authorize(command, session, revision, null)); // item blocked
            Require(!ActionProtocol.Authorize(command, Guid.Empty, revision, offer)); // vehicle left context
            Require(!ActionProtocol.Authorize(command, session, Guid.NewGuid(), offer)); // replay
        }
        Console.WriteLine("V3.1: árvore nativa, pool, NPC somente leitura, Guardar/Retirar e invalidação passaram.");
    }

    private static void CheckV4()
    {
        Guid session = Guid.NewGuid();
        var gear = new Snapshot { Session = session, Character = 312 };
        gear.Rows.Add(new Entry { Group = 0, Name = "Colete", Quantity = 54 });
        gear.Rows.Add(new Entry { Group = 1, Name = "Arma equipada", Quantity = 17 });
        gear.Rows.Add(new Entry { Group = 2, Name = "Arma carregada", Quantity = 0 });
        var bytes = Protocol.Encode(gear);
        var decoded = ActionProtocol.ReadEquipment(bytes, session, 312);
        Require(decoded != null && decoded.Rows[0].Quantity == 54 && decoded.Rows[1].Group == 1
            && decoded.Rows[1].Quantity == 17 && decoded.Rows[2].Quantity == 0);
        Require(ActionProtocol.ReadEquipment(bytes, session, 313) == null);
        Require(ActionProtocol.ReadEquipment(bytes, Guid.NewGuid(), 312) == null);
        Require(ActionProtocol.ReadEquipment(new byte[] { 1, 2 }, session, 312) == null);
        Require(ActionProtocol.ReadEquipment(new byte[ActionProtocol.EquipmentCapacity + 1], session, 312) == null);
        gear.Rows.Add(new Entry { Group = 3, Quantity = 500 });
        Require(ActionProtocol.ReadEquipment(Protocol.Encode(gear), session, 312) == null);
        Require(Protocol.Decode(bytes).Rows.Count == 3); // base payload stays readable
        Require(ActionProtocol.EquipmentPayload + ActionProtocol.EquipmentCapacity == ActionProtocol.Capacity);

        var selection = new ElectroInventory.ActionSelection();
        Guid revision = Guid.NewGuid();
        var offer = new ActionOffer { Token = Guid.NewGuid(), Kinds = ActionKind.Use | ActionKind.Discard, Maximum = 3 };
        selection.Bind(revision, offer);
        Require(selection.AuthorizedKinds().Length == 2 && !selection.Choose(ActionKind.Take));
        Require(selection.Choose(ActionKind.Discard));
        selection.Confirming = true;
        selection.Bind(Guid.NewGuid(), offer);
        Require(!selection.Confirming);
        selection.Bind(revision, new ActionOffer { Token = Guid.Empty, Kinds = ActionKind.Deposit, Maximum = 10 });
        Require(!selection.Choose(ActionKind.Deposit));
        selection.Bind(revision, null);
        Require(!selection.Choose(ActionKind.Take)); // cash and weapons without a bridge executor
        Require(ActionProtocol.ExactTransfer(2.5f, .25f, 1));
        Require(!ActionProtocol.ExactTransfer(.5f, .25f, 1));
        Console.WriteLine("V4: proteção, armas, munição, extensão inválida, contexto e ações restritas passaram.");
    }

    private static void CheckNativeCommerce()
    {
        Require(TradeRules.NpcCanSettle(true, 12, 3, 2, 2, true));
        Require(!TradeRules.NpcCanSettle(true, 12, 3, 2, 1, true));
        Require(TradeRules.NpcCanSettle(false, 12, 3, 4, 0, false));
        Require(!TradeRules.NpcCanSettle(false, 12, 3, 5, 0, false));
        Require(!TradeRules.QuoteAllows(true, 8, 8, 1, 1, 0, true));
        Require(!ActionProtocol.ExactTransfer(1, float.MaxValue, 1)); // no safe inventory credit
        Guid session = Guid.NewGuid(), revision = Guid.NewGuid();
        var offer = new ActionOffer { Token = Guid.NewGuid(), Kinds = ActionKind.Buy, Maximum = 2 };
        var command = new ActionCommand { Session = session, Revision = revision, Token = offer.Token,
            Id = Guid.NewGuid(), Kind = ActionKind.Buy, Quantity = 1 };
        Require(ActionProtocol.Authorize(command, session, revision, offer));
        Require(!ActionProtocol.Authorize(command, Guid.Empty, revision, offer)); // distance/closed conversation
        Require(!ActionProtocol.Authorize(command, session, Guid.NewGuid(), offer)); // accepted already
        var meal = new Entry { Group = 4, Price = 8 };
        Require(!ElectroInventory.TradeTabs.CanAct(meal, null)); // ConsumeOnPurchase is native-only
        int stock = 2;
        for (int spent = 0; spent < 2; spent++)
        {
            Require(TradeRules.BuyLimit(4, 2, spent) == stock);
            stock--;
        }
        Require(TradeRules.BuyLimit(4, 2, 2) == 0);
        Console.WriteLine("Comércio nativo: refeições sem token, fundos do NPC, estoque, sessão e repetição passaram.");
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
