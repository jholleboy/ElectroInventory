using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;
using ElectroInventory.Wire;
using Rage;
using Rage.Native;

[assembly: Rage.Attributes.Plugin("ElectroInventory", Description = "Inventário somente leitura para GTA V Legacy", Author = "ElectroInventory", PrefersSingleInstance = true)]
namespace ElectroInventory
{
    [XmlRoot("ElectroInventory")]
    public sealed class Settings
    {
        public bool Enabled { get; set; } = true;
        public bool Diagnostics { get; set; } = true;
        public bool ItemImages { get; set; } = true;
        public string Mode { get; set; } = "Live";
        public string ToggleKey { get; set; } = "I";
        public string InspectKey { get; set; } = "F7";
        internal static Settings Read()
        {
            string path = Path.Combine("Plugins", "ElectroInventory", "Settings.xml");
            if (!File.Exists(path)) return new Settings();
            using (var file = File.OpenRead(path))
                return (Settings)new XmlSerializer(typeof(Settings)).Deserialize(file);
        }
    }

    public static class Plugin
    {
        private static readonly object RenderLock = new object();
        private static readonly int[] OverlayControls = { 1, 2, 21, 22, 23, 24, 25, 30, 31, 37, 44, 75, 140, 141, 142, 237 };
        // GTA frontend navigation, accept/back and cursor buttons, for both input groups.
        private static readonly int[] ContextControls = { 172, 173, 174, 175, 176, 177,
            187, 188, 189, 190, 191, 192, 201, 202, 203, 204, 205, 237, 238 };
        private static Texture texture, cursor;
        private static PointF? cursorPosition;
        private static RectangleF destination;
        private static volatile bool stop;
        private static string temp;

        public static void Main()
        {
            stop = false;
            try
            {
                var config = Settings.Read();
                if (!config.Enabled) return;
                bool demo = string.Equals(config.Mode, "Demo", StringComparison.OrdinalIgnoreCase);
                if (!demo && !string.Equals(config.Mode, "Live", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Mode deve ser Live ou Demo.");
                Action<string> log = message => { if (config.Diagnostics) Game.LogTrivial("[ElectroInventory] " + message); };
                log($"Inicialização. Modo {config.Mode}, protocolo {Protocol.Version}.");
                temp = Path.Combine(Path.GetTempPath(), "ElectroInventory-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(temp);
                int pid;
                using (var process = Process.GetCurrentProcess()) pid = process.Id;
                using (IDataSource source = demo ? (IDataSource)new DemoDataSource() : new LsrReadOnlyDataSource(pid, log))
                using (IDataSource unavailable = new UnavailableDataSource())
                using (var actions = demo ? null : new ActionClient(pid, config.Diagnostics))
                using (var artwork = new ItemArtwork(config.ItemImages ? Path.Combine("Plugins", "ElectroInventory", "items") : null))
                    Run(source, unavailable, actions, artwork, config, demo, log);
            }
            catch (ThreadAbortException) { throw; }
            catch (Exception e) { Game.LogTrivial("[ElectroInventory] Falha: " + e.GetType().Name + " " + e.Message); }
            finally { Finally(); }
        }

        private static void Run(IDataSource source, IDataSource unavailable, ActionClient actions, ItemArtwork artwork, Settings config, bool demo, Action<string> log)
        {
            var layout = new InventoryView(artwork);
            var nav = new Navigation();
            var selection = new ActionSelection();
            ActionResult lastResult = ActionResult.None;
            Guid lastRevision = Guid.Empty;
            var edges = new HashSet<Keys>();
            var pauseGuard = new PauseGuard();
            var pointer = new PointerCapture();
            var secondary = new PointerCapture();
            bool ownsInput = false;
            Guid releaseSession = Guid.Empty;
            LoadCursor();
            Keys toggle = ParseShortcut(config.ToggleKey, Keys.I);
            Keys inspect = ParseShortcut(config.InspectKey, Keys.F7);
            var inspectEdges = new HashSet<Keys>();
            if (inspect == toggle) log("Inspeção desativada: InspectKey e ToggleKey são iguais.");
            bool open = false, passive = false, conflictLogged = false;
            Snapshot sample = null;
            Guid session = Guid.Empty, handledTradeOpen = Guid.Empty;
            long nextRead = 0;
            int revision = 0;
            Size lastSize = Size.Empty;
            Game.RawFrameRender += Draw;
            while (!stop)
            {
                bool escapeDown = Game.IsKeyDownRightNow(Keys.Escape);
                if (pauseGuard.Update(open && ownsInput, escapeDown))
                    BlockPause();
                long now = Stopwatch.GetTimestamp();
                bool refresh = now >= nextRead;
                if (refresh)
                {
                    nextRead = now + Stopwatch.Frequency / (actions?.Busy == true ? 30 : 10);
                    Snapshot next = source.Read() ?? unavailable.Read();
                    if (!ReferenceEquals(next, sample))
                    {
                        if (open && (next == null || next.Session != session))
                        { open = false; ReleaseTexture(); log("Sessão encerrada ou alterada."); }
                        if (open && sample?.Mode == 2 && next?.Mode == 2)
                            nav.Selected = TradeTabs.RestoreSelection(InventoryView.Items(sample, nav, false),
                                nav.Selected, InventoryView.Items(next, nav, false));
                        sample = next;
                    }
                    else refresh = false;
                    var previousGear = actions?.Equipment;
                    actions?.Poll(sample?.PublicationStamp ?? 0, sample?.Rows.Count ?? 0,
                        sample?.Session ?? Guid.Empty, sample?.Character ?? 0);
                    refresh |= !ReferenceEquals(previousGear, actions?.Equipment);
                    var revisionNow = actions?.Revision ?? Guid.Empty;
                    var resultNow = actions?.Result ?? ActionResult.None;
                    refresh |= revisionNow != lastRevision || resultNow != lastResult;
                    nav.Usable.Clear();
                    if (sample?.Mode == 0 && revisionNow != Guid.Empty)
                        for (int index = 0; index < Math.Min(sample.Rows.Count, actions.Offers.Length); index++)
                        {
                            var entry = actions.Offers[index];
                            if (entry != null && entry.Token != Guid.Empty && entry.Maximum > 0 && (entry.Kinds & ActionKind.Use) != 0)
                                nav.Usable.Add(sample.Rows[index]);
                        }
                    lastRevision = revisionNow;
                    if (lastResult != resultNow && resultNow == ActionResult.Success)
                        nextRead = 0;
                    lastResult = resultNow;
                }
                bool modified = Game.IsKeyDownRightNow(Keys.LControlKey) || Game.IsKeyDownRightNow(Keys.RControlKey)
                    || Game.IsKeyDownRightNow(Keys.LMenu) || Game.IsKeyDownRightNow(Keys.RMenu)
                    || Game.IsKeyDownRightNow(Keys.LShiftKey) || Game.IsKeyDownRightNow(Keys.RShiftKey);
                bool togglePressed = Pressed(toggle, edges);
                bool inspectPressed = inspect != toggle && Pressed(inspect, inspectEdges) && !modified;
                bool escape = Pressed(Keys.Escape, edges);
                bool textInput = NativeFunction.Natives.UPDATE_ONSCREEN_KEYBOARD<int>() == 0;
                bool blocked = textInput || NativeFunction.Natives.IS_PAUSE_MENU_ACTIVE<bool>() || NativeFunction.Natives.IS_CUTSCENE_ACTIVE<bool>()
                    || NativeFunction.Natives.IS_SCREEN_FADED_OUT<bool>()
                    || NativeFunction.Natives.IS_SCREEN_FADING_OUT<bool>();
                var ped = Game.LocalPlayer.Character;
                int character = ped != null && ped.Exists() ? unchecked((int)(uint)ped.Handle) : 0;
                bool available = character != 0 && sample != null &&
                    OverlaySession.Matches(sample.Session, sample.Session, sample.Character, character, !blocked, demo);
                if (open && (!available || sample.Session != session || (!passive && (!ped.IsVisible || sample.BlocksInput))))
                {
                    open = false;
                    ReleaseTexture();
                }
                bool nativeTrade = sample?.Mode == 2 && actions?.NativeTradeOnly(sample.Session) == true;
                var requestedTrade = sample?.Mode == 2 ? actions?.TradeOpen ?? Guid.Empty : Guid.Empty;
                bool autoTrade = OverlaySession.HasNewTradeOpen(available, requestedTrade, handledTradeOpen);
                if (autoTrade) handledTradeOpen = requestedTrade;
                if (nativeTrade && (open || (available && inspectPressed) || autoTrade))
                {
                    if (open) { open = false; ReleaseTexture(); }
                    Game.DisplayHelp("Esta negociação usa o menu original do Los Santos RED.");
                    inspectPressed = autoTrade = false;
                }
                if (open && ownsInput && escape && layout.ContextOpen)
                {
                    layout.ContextOpen = false;
                    escape = false;
                    refresh = true;
                }
                if (open && ownsInput && escape && selection.Confirming)
                {
                    selection.Confirming = false;
                    escape = false;
                    refresh = true;
                }
                if (open && OverlaySession.CloseRequested(passive, ownsInput, inspectPressed,
                    togglePressed && !modified, escape, nav.Search, layout.CloseRequested))
                {
                    open = false;
                    ReleaseTexture();
                }
                else if (available && ((!open && OverlaySession.CanOpenPersonal(togglePressed && !modified,
                    sample.Mode, sample.BlocksInput, ped.IsVisible, escapeDown)) ||
                    (sample.Mode != 0 && (autoTrade || (!open && inspectPressed)))))
                {
                    open = true;
                    passive = sample.Mode != 0;
                    session = sample.Session;
                    nav.Reset();
                    if (sample.Mode == 2)
                    {
                        nav.Trade.Open(sample);
                        if (autoTrade) nav.Trade.Select(sample, actions.TradeDirection);
                    }
                    selection.Bind(Guid.Empty, null);
                    layout.CloseRequested = false;
                    layout.ActionRequested = false;
                    layout.ContextOpen = false;
                    refresh = true;
                    if (!passive) BlockPause();
                }
                else if (inspectPressed && !open)
                {
                    string reason = blocked ? "pausa, cena ou tela em transição" :
                        sample == null ? "sem snapshot válido da RC2" :
                        !available ? "personagem diferente ou indisponível" :
                        "nenhuma sessão de comércio ou armazenamento compatível ativa";
                    log("Inspeção indisponível: " + reason + ".");
                }
                else if (togglePressed && !open && !conflictLogged)
                {
                    log("Abertura recusada: contexto indisponível ou entrada reservada por outra interface.");
                    conflictLogged = true;
                }
                else if (open) conflictLogged = false;

                bool previousInput = ownsInput;
                if (!open && previousInput && passive && sample?.Session == session)
                    releaseSession = session;
                if (!open && releaseSession != Guid.Empty && sample?.Session == releaseSession && !blocked)
                {
                    // Keep the original pool paused through the release of an overlay click.
                    BlockMouseClick();
                    if (NativeFunction.Natives.IS_DISABLED_CONTROL_PRESSED<bool>(0, 237) ||
                        NativeFunction.Natives.IS_DISABLED_CONTROL_PRESSED<bool>(0, 238))
                        actions?.UpdateFocus(releaseSession);
                    else releaseSession = Guid.Empty;
                }
                else releaseSession = Guid.Empty;

                ownsInput = open && (!passive || actions?.UpdateFocus(session) == true);
                if ((!open && releaseSession == Guid.Empty) || (open && !passive))
                    actions?.UpdateFocus(Guid.Empty);
                if (!ownsInput)
                {
                    pointer.Click(false, false);
                    secondary.Click(false, false);
                    lock (RenderLock) cursorPosition = null;
                }
                if (ownsInput != previousInput)
                {
                    refresh = true;
                    // Discard hit targets rendered under the previous ownership state.
                    layout.Targets.Clear();
                    layout.ActionRequested = false;
                    selection.Confirming = false;
                    layout.ContextOpen = false;
                }
                if (open)
                {
                    var gear = actions?.Equipment;
                    if (gear?.Session != sample.Session || gear.Character != sample.Character || actions.EquipmentStamp != sample.PublicationStamp)
                        gear = null;
                    if (gear == null) nav.Equipment = false;
                    var displayed = nav.Equipment ? gear : sample;
                    Size size = Game.Resolution;
                    RectangleF bounds = OverlayViewport.Fit(size, new Size(InventoryView.Width, InventoryView.Height));
                    if (ownsInput)
                    {
                        if (!layout.ContextOpen && (nav.Search || !Game.IsKeyDownRightNow(Keys.LControlKey)))
                            refresh |= Navigate(nav, displayed, edges, toggle, togglePressed, escape);
                        // Frame-local controls expire automatically when the overlay closes.
                        foreach (int control in OverlayControls)
                            NativeFunction.Natives.DISABLE_CONTROL_ACTION(0, control, true);
                        BlockMouseClick();
                        if (passive)
                            foreach (int control in ContextControls)
                            {
                                NativeFunction.Natives.DISABLE_CONTROL_ACTION(0, control, true);
                                NativeFunction.Natives.DISABLE_CONTROL_ACTION(2, control, true);
                            }
                        NativeFunction.Natives.SET_MOUSE_CURSOR_THIS_FRAME();
                        bool click = NativeFunction.Natives.IS_DISABLED_CONTROL_PRESSED<bool>(0, 237);
                        bool rightClick = NativeFunction.Natives.IS_DISABLED_CONTROL_PRESSED<bool>(0, 238);
                        float x = NativeFunction.Natives.GET_CONTROL_NORMAL<float>(0, 239) * size.Width;
                        float y = NativeFunction.Natives.GET_CONTROL_NORMAL<float>(0, 240) * size.Height;
                        bool overPanel = OverlayPointer.TryMap(x, y, bounds, new Size(InventoryView.Width, InventoryView.Height), out Point logical);
                        lock (RenderLock) cursorPosition = overPanel ? new PointF(x, y) : (PointF?)null;
                        if (pointer.Click(true, click) && overPanel && size == lastSize)
                        {
                            layout.Click(logical);
                            refresh = true;
                        }
                        if (secondary.Click(true, rightClick) && overPanel && size == lastSize)
                        {
                            layout.RightClick(logical);
                            refresh = true;
                        }

                    }
                    else if (Game.IsKeyDownRightNow(Keys.LControlKey))
                    {
                        if (Pressed(Keys.F7, edges)) { nav.Selected = Math.Max(0, nav.Selected - InventoryView.Page); refresh = true; }
                        if (Pressed(Keys.F8, edges)) { nav.Selected += InventoryView.Page; refresh = true; }
                    }
                    bool switchSide = Pressed(Keys.F6, edges);
                    if (sample.Mode == 1 && switchSide && Game.IsKeyDownRightNow(Keys.LControlKey))
                    { nav.SwitchSide(); refresh = true; }
                    bool switchTab = Pressed(Keys.F2, edges);
                    if (sample.Mode == 2 && switchTab && Game.IsKeyDownRightNow(Keys.LControlKey)
                        && nav.Trade.Toggle(sample))
                    {
                        nav.Filter("");
                        selection.Bind(Guid.Empty, null);
                        layout.ActionRequested = false;
                        refresh = true;
                    }
                    // The selected object is mapped back to its original snapshot row, not its filtered index.
                    if (Pressed(Keys.F1, edges) && ownsInput && !nav.Search && gear != null)
                    {
                        nav.ToggleEquipment();
                        refresh = true;
                    }
                    displayed = nav.Equipment ? gear : sample;
                    var items = InventoryView.Items(displayed, nav, nav.Right);
                    int row = items.Count == 0 ? -1 : sample.Rows.IndexOf(items[Math.Min(items.Count - 1, Math.Max(0, nav.Selected))]);
                    var offer = !nav.Equipment && actions != null && row >= 0 && row < actions.Offers.Length ? actions.Offers[row] : null;
                    if (sample.Mode == 2 && (row < 0 || !TradeTabs.CanAct(sample.Rows[row], offer))) offer = null;
                    var token = selection.Offer?.Token;
                    selection.Bind(lastRevision, offer);
                    if (token != selection.Offer?.Token)
                    {
                        layout.ActionRequested = false;
                        refresh = true;
                    }
                    if (ownsInput && !nav.Search && Pressed(Keys.Apps, edges))
                    {
                        layout.OpenContext();
                        refresh = true;
                    }
                    if (layout.ContextOpen)
                    {
                        if (Pressed(Keys.Up, edges)) { layout.ContextStep(-1); refresh = true; }
                        if (Pressed(Keys.Down, edges)) { layout.ContextStep(1); refresh = true; }
                        if (Pressed(Keys.Enter, edges)) { layout.ActivateContext(selection, actions?.Busy == true); refresh = true; }
                    }
                    if (!layout.ContextOpen && !nav.Search && Game.IsKeyDownRightNow(Keys.LControlKey))
                    {
                        if (Pressed(Keys.Up, edges)) { nav.Selected = Math.Max(0, nav.Selected - 1); refresh = true; }
                        if (Pressed(Keys.Down, edges)) { nav.Selected++; refresh = true; }
                        if (Pressed(Keys.Left, edges)) { selection.Adjust(-1); refresh = true; }
                        if (Pressed(Keys.Right, edges)) { selection.Adjust(1); refresh = true; }
                        if (Pressed(Keys.F5, edges)) { selection.Confirming = false; refresh = true; }
                        if (Pressed(Keys.F4, edges)) { selection.NextKind(); refresh = true; }
                        if (Pressed(Keys.F3, edges) && offer != null && actions?.Busy == false)
                        { layout.ActionRequested = true; refresh = true; }
                        // Never carry a confirmation to a different row.
                        if (refresh && items.Count > 0 && row != sample.Rows.IndexOf(items[Math.Min(items.Count - 1, Math.Max(0, nav.Selected))]))
                        { selection.Bind(Guid.Empty, null); layout.ActionRequested = false; }
                    }
                    if (layout.ActionRequested)
                    {
                        layout.ActionRequested = false;
                        if (selection.Kind != ActionKind.None && offer != null && actions?.Busy == false)
                        {
                            if (selection.Confirming)
                            {
                                actions.Submit(sample.Session, selection.Revision, offer, selection.Kind, selection.Quantity);
                                selection.Confirming = false;
                            }
                            else selection.Confirming = true;
                        }
                        refresh = true;
                    }
                    if (size != lastSize || refresh)
                    {
                        string path = Path.Combine(temp, "page-" + revision++ + ".png");
                        using (var bitmap = layout.Make(displayed, nav, demo, ownsInput, selection, actions?.Result ?? ActionResult.None, actions?.Busy == true, gear != null, actions?.TradeBalance ?? 0, toggle.ToString(), inspect != toggle ? inspect.ToString() : null)) bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                        var next = Game.CreateTextureFromFile(path);
                        if (next == null) throw new IOException("Textura da interface indisponível.");
                        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                        lock (RenderLock)
                        {
                            if ((object)texture is IDisposable disposable) disposable.Dispose();
                            texture = next;
                            destination = bounds;
                        }
                        lastSize = size;
                    }
                }
                GameFiber.Yield();
            }
        }

        private static Keys ParseShortcut(string value, Keys fallback) =>
            Enum.TryParse(value, true, out Keys key) && Enum.IsDefined(typeof(Keys), key) && key != Keys.None && key != Keys.F10 && key != Keys.Escape ? key : fallback;

        private static bool Pressed(Keys key, HashSet<Keys> edges)
        {
            if (Game.IsKeyDownRightNow(key)) return edges.Add(key);
            edges.Remove(key);
            return false;
        }

        private static bool Navigate(Navigation nav, Snapshot snapshot, HashSet<Keys> edges, Keys toggle, bool togglePressed, bool escape)
        {
            bool slash = Pressed(Keys.OemQuestion, edges);
            bool back = Pressed(Keys.Back, edges);
            bool enter = Pressed(Keys.Enter, edges);
            bool changed = false;
            for (int code = (int)Keys.A; code <= (int)Keys.Z; code++)
            {
                var key = (Keys)code;
                bool down = key == toggle ? togglePressed : Pressed(key, edges);
                if (nav.Search && down && nav.Query.Length < 80)
                { nav.Query += (char)code; changed = true; }
            }
            if (nav.Search)
            {
                if (back && nav.Query.Length > 0) { nav.Query = nav.Query.Substring(0, nav.Query.Length - 1); changed = true; }
                if (Pressed(Keys.Space, edges) && nav.Query.Length < 80) { nav.Query += " "; changed = true; }
                if (escape || enter) { nav.Search = false; changed = true; }
                if (changed) nav.Selected = 0;
                return changed;
            }
            if (slash) { nav.Search = true; return true; }
            bool previousCategory = Pressed(Keys.Home, edges);
            bool nextCategory = Pressed(Keys.End, edges);
            if (previousCategory || nextCategory)
            {
                var categories = InventoryView.Categories(snapshot, nav);
                int index = categories.IndexOf(nav.Category);
                index = (index + (nextCategory ? 1 : categories.Count - 1) + categories.Count) % categories.Count;
                nav.Filter(categories[index]);
                nav.CategoryOffset = index / 8 * 8;
                return true;
            }
            int delta = (Pressed(Keys.Right, edges) ? 1 : 0) - (Pressed(Keys.Left, edges) ? 1 : 0)
                + (Pressed(Keys.Down, edges) ? 3 : 0) - (Pressed(Keys.Up, edges) ? 3 : 0)
                + (Pressed(Keys.PageDown, edges) ? InventoryView.Page : 0) - (Pressed(Keys.PageUp, edges) ? InventoryView.Page : 0);
            if (NativeFunction.Natives.IS_CONTROL_JUST_PRESSED<bool>(0, 241)) delta -= InventoryView.Page;
            if (NativeFunction.Natives.IS_CONTROL_JUST_PRESSED<bool>(0, 242)) delta += InventoryView.Page;
            if (delta != 0) { nav.Selected = Math.Max(0, nav.Selected + delta); return true; }
            return false;
        }

        private static void BlockMouseClick()
        {
            NativeFunction.Natives.DISABLE_CONTROL_ACTION(0, 237, true);
            NativeFunction.Natives.DISABLE_CONTROL_ACTION(2, 237, true);
            NativeFunction.Natives.DISABLE_CONTROL_ACTION(0, 238, true);
            NativeFunction.Natives.DISABLE_CONTROL_ACTION(2, 238, true);
        }

        private static void BlockPause()
        {
            NativeFunction.Natives.DISABLE_CONTROL_ACTION(0, 199, true);
            NativeFunction.Natives.DISABLE_CONTROL_ACTION(0, 200, true);
            NativeFunction.Natives.DISABLE_CONTROL_ACTION(2, 199, true);
            NativeFunction.Natives.DISABLE_CONTROL_ACTION(2, 200, true);
        }

        private static void LoadCursor()
        {
            string path = Path.Combine(temp, "cursor.png");
            using (var bitmap = new Bitmap(30, 34))
            using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
            using (var pen = new Pen(Color.FromArgb(7, 17, 28), 2))
            using (var fill = new SolidBrush(Color.FromArgb(223, 253, 255)))
            {
                var points = new[] { new Point(2, 2), new Point(24, 21), new Point(14, 21),
                    new Point(18, 29), new Point(13, 32), new Point(9, 23), new Point(2, 29) };
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.FillPolygon(fill, points);
                graphics.DrawPolygon(pen, points);
                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
            cursor = Game.CreateTextureFromFile(path);
            if (cursor == null) throw new IOException("Textura do cursor indisponível.");
        }

        private static void Draw(object sender, Rage.GraphicsEventArgs args)
        {
            try
            {
                lock (RenderLock)
                {
                    if (texture == null) return;
                    args.Graphics.DrawTexture(texture, destination);
                    if (cursor != null && cursorPosition is PointF point)
                    {
                        float scale = Math.Max(.75f, destination.Width / InventoryView.Width);
                        args.Graphics.DrawTexture(cursor, new RectangleF(point.X - 2 * scale, point.Y - 2 * scale, 30 * scale, 34 * scale));
                    }
                }
            }
            catch (Exception e) { stop = true; Game.LogTrivial("[ElectroInventory] Falha de renderização: " + e.Message); }
        }

        private static void ReleaseTexture()
        {
            lock (RenderLock)
            {
                if ((object)texture is IDisposable disposable) disposable.Dispose();
                texture = null;
                cursorPosition = null;
            }
        }

        public static void Finally()
        {
            stop = true;
            Game.RawFrameRender -= Draw;
            ReleaseTexture();
            lock (RenderLock)
            {
                if ((object)cursor is IDisposable disposable) disposable.Dispose();
                cursor = null;
            }
            if (temp == null) return;
            try { Directory.Delete(temp, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            temp = null;
        }
    }
}
