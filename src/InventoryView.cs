using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ElectroInventory.Wire;

namespace ElectroInventory
{
    internal sealed class Navigation
    {
        public string Query = "", Category = "";
        public bool Search;
        public int Selected, Offset, CategoryOffset;
        public bool Right;
        public readonly TradeTabs Trade = new TradeTabs();
        public void Reset() { Query = Category = ""; Search = Right = false; Selected = Offset = CategoryOffset = 0; }
        public void Filter(string category) { Category = category; Selected = Offset = 0; }
    }

    internal sealed class InventoryView
    {
        public const int Width = 1400, Height = 760, Page = 12;
        private static readonly Color Cyan = Color.FromArgb(87, 229, 243), Pink = Color.FromArgb(247, 137, 213);
        private static readonly Color Ink = Color.FromArgb(231, 245, 250), Muted = Color.FromArgb(157, 177, 194);
        private static readonly Color CardSelected = Color.FromArgb(23, 54, 68);
        private static readonly Color Card = Color.FromArgb(19, 36, 53), Edge = Color.FromArgb(49, 76, 94);
        public readonly List<Tuple<Rectangle, Action>> Targets = new List<Tuple<Rectangle, Action>>();

        public static List<Entry> Items(Snapshot snapshot, Navigation nav, bool right)
        {
            var compare = System.Globalization.CultureInfo.GetCultureInfo("pt-BR").CompareInfo;
            return snapshot.Rows.Where(row => (snapshot.Mode != 1 || (row.Group != 0) == right)
                && (snapshot.Mode != 2 || row.Group == nav.Trade.Current)
                && (nav.Category.Length == 0 || row.Category == nav.Category)
                && compare.IndexOf(row.Name ?? "", nav.Query, System.Globalization.CompareOptions.IgnoreCase |
                    System.Globalization.CompareOptions.IgnoreNonSpace) >= 0).ToList();
        }

        public Bitmap Make(Snapshot snapshot, Navigation nav, bool demo, bool passive, ActionSelection action, ActionResult result, bool busy)
        {
            var image = new Bitmap(Width, Height);
            using (var g = Graphics.FromImage(image))
            {
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                Targets.Clear();
                Box(g, new Rectangle(0, 0, Width, Height), Color.FromArgb(9, 19, 31), Cyan);
                Line(g, 25, 72, Width - 25, 72, Edge);
                Text(g, "◇  ELECTROINVENTORY", 29, 20, 480, 33, 21, Cyan, true);
                Text(g, demo ? "DADOS DEMONSTRATIVOS" : "RC2  /  " + (action.Offer == null ? "LEITURA" : "AÇÕES AUTORIZADAS"), 810, 25, 300, 32, 16, Pink, true);
                if (!passive)
                    Button(g, new Rectangle(1250, 20, 120, 38), "Esc  Fechar", () => CloseRequested = true);
                Text(g, snapshot.Mode == 1 ? "ARMAZENAMENTO" : snapshot.Mode == 2 ? "COMÉRCIO" : "INVENTÁRIO",
                    28, 90, 750, 50, 40, Ink, true);
                Text(g, snapshot.Title, 30, 143, 750, 35, 22, Muted, false);
                if (!passive)
                    Button(g, new Rectangle(914, 99, 456, 46),
                        (nav.Search ? "⌕  › " : "⌕  ") + (nav.Query.Length == 0 ? "Pesquisar por nome" : nav.Query),
                        () => nav.Search = true, nav.Search);
                else if (!double.IsNaN(snapshot.Balance))
                    Text(g, "Saldo aplicável  $ " + Quantity(snapshot.Balance), 1004, 107, 355, 30, 19, Cyan, true);

                if (snapshot.Mode == 2) DrawTradeTabs(g, snapshot, nav);
                var categories = snapshot.Rows.Select(x => x.Category).Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct().ToList();
                categories.Insert(0, "");
                var active = Items(snapshot, nav, nav.Right);
                int count = active.Count;
                nav.Selected = Math.Max(0, Math.Min(nav.Selected, count - 1));
                nav.Offset = (nav.Selected / Page) * Page;
                if (!passive)
                {
                    int x = 28;
                    foreach (string category in categories.Skip(nav.CategoryOffset).Take(8))
                    {
                        string key = category;
                        Button(g, new Rectangle(x, 183, 145, 35), key.Length == 0 ? "Todos" : key,
                            () => nav.Filter(key), nav.Category == key);
                        x += 153;
                    }
                    if (categories.Count > 8)
                        Button(g, new Rectangle(1270, 183, 100, 35), "Mais ›", () =>
                            nav.CategoryOffset = nav.CategoryOffset + 8 >= categories.Count ? 0 : nav.CategoryOffset + 8);
                }

                if (snapshot.Mode == 1)
                {
                    Grid(g, Items(snapshot, nav, false), nav, false, new Rectangle(26, 230, 450, 458), "Inventário do jogador", 3, passive);
                    Grid(g, Items(snapshot, nav, true), nav, true, new Rectangle(488, 230, 450, 458), "Armazenamento", 3, passive);
                }
                else Grid(g, active, nav, false, new Rectangle(26, 230, 912, 458),
                    snapshot.Mode == 2 ? (nav.Trade.Current == TradeTabs.Buy ? "Ofertas da sessão" : "Itens elegíveis do inventário") : "Itens pessoais", 3, passive);
                DrawDetail(g, count == 0 ? null : active[nav.Selected]);
                DrawActions(g, action, result, busy, passive);
                Line(g, 25, 704, Width - 25, 704, Edge);
                Text(g, snapshot.Mode == 2
                    ? "F10 fecha  ·  Ctrl+F2 Comprar/Vender  ·  Ctrl+setas navega/ajusta  ·  Ctrl+F3 confirma  ·  Ctrl+F5 cancela"
                    : passive ? "F10 fecha  ·  Ctrl+F6 troca lado  ·  Ctrl+setas navega/ajusta  ·  Ctrl+F4 ação  ·  Ctrl+F3 confirma  ·  Ctrl+F5 cancela" :
                    "Setas/mouse  Navegar  ·  PgUp/PgDn/roda  Página  ·  /  Pesquisar  ·  Ctrl+F4  Ação  ·  Ctrl+F3  Confirmar  ·  I/Esc  Fechar",
                    28, 718, 1150, 27, 16, Muted, false);
                if (snapshot.Incomplete) Text(g, "Lista parcial", 1230, 718, 145, 27, 16, Pink, true);
            }
            return image;
        }

        private static void DrawTradeTabs(Graphics g, Snapshot snapshot, Navigation nav)
        {
            foreach (byte group in new[] { TradeTabs.Buy, TradeTabs.Sell })
            {
                bool present = TradeTabs.HasRows(snapshot, group);
                bool selected = nav.Trade.Current == group;
                int x = group == TradeTabs.Buy ? 28 : 198;
                var rect = new Rectangle(x, 180, 157, 39);
                Box(g, rect, Card, selected ? Cyan : Edge);
                Text(g, group == TradeTabs.Buy ? "COMPRAR" : "VENDER", x + 12, 188, 130, 24, 16,
                    present && selected ? Cyan : Muted, true);
            }
            string reason = !TradeTabs.HasRows(snapshot, TradeTabs.Buy) ? "Compra: sem ofertas deste comerciante." :
                !TradeTabs.HasRows(snapshot, TradeTabs.Sell) ? "Venda: nenhum item elegível ou comerciante não compra." : "";
            Text(g, reason, 376, 186, 740, 34, 16, Muted, false);
        }

        public bool CloseRequested, ActionRequested;
        public void Click(Point point) { Targets.FindLast(hit => hit.Item1.Contains(point))?.Item2(); }

        private void Grid(Graphics g, List<Entry> rows, Navigation nav, bool right, Rectangle bounds, string title, int columns, bool passive)
        {
            Box(g, bounds, Color.FromArgb(13, 26, 39), Edge);
            Text(g, title, bounds.X + 12, bounds.Y + 9, bounds.Width - 22, 26, 19, Ink, true);
            int offset = right == nav.Right ? nav.Offset : 0;
            if (rows.Count == 0) Text(g, "Nenhum item nesta seleção.", bounds.X + 12, bounds.Y + 56, bounds.Width - 24, 32, 18, Muted, false);
            int width = (bounds.Width - 28) / columns;
            for (int i = 0; i < Page && offset + i < rows.Count; i++)
            {
                int index = offset + i;
                Entry row = rows[index];
                var card = new Rectangle(bounds.X + 12 + (i % columns) * width, bounds.Y + 48 + (i / columns) * 92, width - 6, 84);
                bool selected = nav.Right == right && nav.Selected == index;
                Box(g, card, selected ? CardSelected : Card, selected ? Cyan : Edge);
                if (selected) Line(g, card.Left + 2, card.Bottom - 3, card.Right - 3, card.Bottom - 3, Pink);
                Text(g, Glyph(row), card.X + 7, card.Y + 7, 34, 25, 20, selected ? Pink : Cyan, true);
                Text(g, row.Name, card.X + 7, card.Y + 35, card.Width - 14, 27, 16, Ink, true);
                Text(g, Summary(row), card.X + 7, card.Bottom - 22, card.Width - 14, 20, 14, selected ? Ink : Muted, false);
                if (!passive) Targets.Add(Tuple.Create(card, (Action)(() => { nav.Right = right; nav.Selected = index; nav.Search = false; })));
            }
            Text(g, rows.Count == 0 ? "0 entradas" : $"{offset + 1}–{Math.Min(rows.Count, offset + Page)} / {rows.Count}",
                bounds.X + 12, bounds.Bottom - 32, bounds.Width - 24, 23, 15, Muted, false);
        }

        private static void DrawDetail(Graphics g, Entry row)
        {
            Box(g, new Rectangle(951, 230, 419, 458), Color.FromArgb(12, 25, 38), Edge);
            Text(g, "Detalhes", 971, 244, 370, 30, 19, Cyan, true);
            if (row == null) return;
            Text(g, Glyph(row), 971, 276, 365, 52, 36, Pink, true);
            Text(g, row.Name, 971, 329, 370, 53, 24, Ink, true);
            Text(g, row.Category + "  ·  " + Summary(row), 971, 381, 370, 35, 17, Cyan, false);
            Text(g, row.Description, 971, 424, 370, 83, 19, Ink, false);
            Text(g, row.Detail, 971, 510, 370, 48, 16, Muted, false);
        }

        private void DrawActions(Graphics g, ActionSelection action, ActionResult result, bool busy, bool passive)
        {
            string status = busy ? "Aguardando o LS RED…" : result == ActionResult.Started ? "Atividade iniciada no LS RED." :
                result == ActionResult.Success ? "Operação concluída." : result == ActionResult.Expired ? "Contexto alterado. Selecione novamente." :
                result == ActionResult.Rejected ? "Operação recusada pelo LS RED." : result == ActionResult.Uncertain ?
                "Resultado desconhecido. Confira o inventário." : result == ActionResult.Cancelled ? "Operação cancelada." : "";
            Text(g, status, 970, 655, 380, 26, 15, Muted, false);
            if (action.Kind == ActionKind.None || busy)
            {
                if (!busy) Text(g, "Ação indisponível para esta seleção na sessão atual.", 970, 603, 380, 42, 16, Muted, false);
                return;
            }
            string label = ActionSelection.Label(action.Kind);
            if (passive)
            {
                Text(g, (action.Confirming ? "Confirmar: " : "") + label + "  × " + action.Quantity,
                    970, 593, 370, 56, 20, action.Confirming ? Pink : Cyan, true);
                return;
            }
            if (action.Confirming)
            {
                Button(g, new Rectangle(970, 595, 225, 41), "Confirmar " + label + " × " + action.Quantity,
                    () => ActionRequested = true, true);
                Button(g, new Rectangle(1204, 595, 140, 41), "Cancelar", () => action.Confirming = false);
                return;
            }
            Button(g, new Rectangle(970, 595, 177, 41), label + " × " + action.Quantity, () => ActionRequested = true, true);
            Button(g, new Rectangle(1154, 595, 84, 41), "Ação ›", action.NextKind);
            Button(g, new Rectangle(1245, 595, 44, 41), "−", () => action.Adjust(-1));
            Button(g, new Rectangle(1296, 595, 44, 41), "+", () => action.Adjust(1));
        }

        private static string Glyph(Entry row)
        {
            if (row.Group == 2) return "◈";
            if (row.Group == 3) return "$";
            switch (row.Category)
            {
                case "Alimentos": case "Food": case "Meals": return "◉";
                case "Bebidas": case "Drinks": return "♧";
                case "Armas": case "Weapons": return "◈";
                case "Equipamentos": case "Equipment": return "⚙";
                default: return "◇";
            }
        }
        private static readonly System.Text.RegularExpressions.Regex GtaTags =
            new System.Text.RegularExpressions.Regex(@"~[a-zA-Z0-9_]+~");
        private static string Quantity(double value) => value.ToString("0.####", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
        private static string Summary(Entry row)
        {
            if (row.Group >= 4)
                return (row.Group == 4 ? "Compra" : "Venda") + "  " + (double.IsNaN(row.Price) ? "—" : "$ " + Quantity(row.Price))
                    + (double.IsNaN(row.Quantity) ? "" : "  × " + Quantity(row.Quantity));
            return double.IsNaN(row.Quantity) ? "" : (row.Group == 3 ? "$ " : "") + Quantity(row.Quantity);
        }
        private void Button(Graphics g, Rectangle rectangle, string label, Action action, bool active = false)
        {
            Box(g, rectangle, Card, active ? Cyan : Edge);
            Text(g, label, rectangle.X + 8, rectangle.Y + 3, rectangle.Width - 12, rectangle.Height - 6, 16, Ink, false);
            Targets.Add(Tuple.Create(rectangle, action));
        }
        private static void Line(Graphics g, int x, int y, int x2, int y2, Color color)
        { using (var pen = new Pen(color)) g.DrawLine(pen, x, y, x2, y2); }
        private static void Box(Graphics g, Rectangle rect, Color fill, Color border)
        {
            using (var brush = new SolidBrush(fill)) g.FillRectangle(brush, rect);
            using (var pen = new Pen(border)) g.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
        }
        private static void Text(Graphics g, string value, int x, int y, int width, int height, int size, Color color, bool bold)
        {
            if (string.IsNullOrEmpty(value)) return;
            using (var font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat { Trimming = StringTrimming.EllipsisWord, FormatFlags = StringFormatFlags.LineLimit })
                g.DrawString(GtaTags.Replace(value.Replace("~n~", "\n"), ""), font, brush, new RectangleF(x, y, width, height), format);
        }
    }
}
