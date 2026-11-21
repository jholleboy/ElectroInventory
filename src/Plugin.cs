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
        public string Mode { get; set; } = "Live";
        public string ToggleKey { get; set; } = "I";
        public string InspectKey { get; set; } = "F10";
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
        private static readonly int[] OverlayControls = { 1, 2, 21, 22, 23, 24, 25, 30, 31, 37, 44, 75, 140, 141, 142, 199, 200, 237 };
        private static Texture texture;
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
                    Run(source, unavailable, config, demo, log);
            }
            catch (ThreadAbortException) { throw; }
            catch (Exception e) { Game.LogTrivial("[ElectroInventory] Falha: " + e.GetType().Name + " " + e.Message); }
            finally { Finally(); }
        }

        private static void Run(IDataSource source, IDataSource unavailable, Settings config, bool demo, Action<string> log)
        {
            var layout = new InventoryView();
            var nav = new Navigation();
            var edges = new HashSet<Keys>();
            Keys toggle = Parse(config.ToggleKey, Keys.I);
            Keys inspect = Parse(config.InspectKey, Keys.F10);
            bool open = false, passive = false, mousePressed = false, conflictLogged = false;
            Snapshot sample = null;
            Guid session = Guid.Empty;
            long nextRead = 0;
            int revision = 0;
            Size lastSize = Size.Empty;
            Game.RawFrameRender += Draw;
            while (!stop)
            {
                long now = Stopwatch.GetTimestamp();
                bool refresh = now >= nextRead;
                if (refresh)
                {
                    nextRead = now + Stopwatch.Frequency / 10;
                    Snapshot next = source.Read() ?? unavailable.Read();
                    if (!ReferenceEquals(next, sample))
                    {
                        if (open && (next == null || next.Session != session))
                        { open = false; ReleaseTexture(); log("Sessão encerrada ou alterada."); }
                        sample = next;
                    }
                    else refresh = false;
                }
                bool togglePressed = Pressed(toggle, edges);
                bool inspectPressed = inspect != toggle && Pressed(inspect, edges);
                bool escape = Pressed(Keys.Escape, edges);
                bool blocked = NativeFunction.Natives.IS_PAUSE_MENU_ACTIVE<bool>() || NativeFunction.Natives.IS_CUTSCENE_ACTIVE<bool>()
                    || NativeFunction.Natives.IS_SCREEN_FADED_OUT<bool>()
                    || NativeFunction.Natives.IS_SCREEN_FADING_OUT<bool>();
                var ped = Game.LocalPlayer.Character;
                bool available = !blocked && ped != null && ped.Exists() && ped.IsVisible && (demo || sample?.Character == unchecked((int)(uint)ped.Handle));
                if (open && (!available || sample == null || (!passive && sample.BlocksInput)))
                {
                    open = false;
                    ReleaseTexture();
                }
                if (open && ((passive && inspectPressed) || (!passive && (layout.CloseRequested ||
                    (escape && !nav.Search) || (togglePressed && !nav.Search)))))
                {
                    open = false;
                    ReleaseTexture();
                }
                else if (!open && available && sample != null &&
                    ((togglePressed && sample.Mode == 0 && !sample.BlocksInput) || (inspectPressed && sample.Mode != 0)))
                {
                    open = true;
                    passive = sample.Mode != 0;
                    session = sample.Session;
                    nav.Reset();
                    layout.CloseRequested = false;
                    refresh = true;
                }
                else if (togglePressed && !open && !conflictLogged)
                {
                    log("Abertura recusada: contexto indisponível ou entrada reservada por outra interface.");
                    conflictLogged = true;
                }
                else if (open) conflictLogged = false;

                if (open)
                {
                    Size size = Game.Resolution;
                    RectangleF bounds = Fit(size);
                    if (!passive)
                    {
                        refresh |= Navigate(nav, edges, toggle, togglePressed, escape);
                        // Frame-local controls expire automatically when the overlay closes.
                        foreach (int control in OverlayControls)
                            NativeFunction.Natives.DISABLE_CONTROL_ACTION(0, control, true);
                        NativeFunction.Natives.SET_MOUSE_CURSOR_THIS_FRAME();
                        bool click = NativeFunction.Natives.IS_DISABLED_CONTROL_PRESSED<bool>(0, 237);
                        if (click && !mousePressed && bounds.Width > 0)
                        {
                            float x = NativeFunction.Natives.GET_CONTROL_NORMAL<float>(0, 239) * size.Width;
                            float y = NativeFunction.Natives.GET_CONTROL_NORMAL<float>(0, 240) * size.Height;
                            layout.Click(new Point((int)((x - bounds.Left) * InventoryView.Width / bounds.Width),
                                (int)((y - bounds.Top) * InventoryView.Height / bounds.Height)));
                            refresh = true;
                        }
                        mousePressed = click;
                    }
                    else if (Game.IsKeyDownRightNow(Keys.LControlKey))
                    {
                        if (Pressed(Keys.F7, edges)) { nav.Selected = Math.Max(0, nav.Selected - InventoryView.Page); refresh = true; }
                        if (Pressed(Keys.F8, edges)) { nav.Selected += InventoryView.Page; refresh = true; }
                        if (Pressed(Keys.F6, edges) && sample.Mode == 1) { nav.Right = !nav.Right; nav.Selected = 0; refresh = true; }
                    }
                    if (size != lastSize || refresh)
                    {
                        string path = Path.Combine(temp, "page-" + revision++ + ".png");
                        using (var bitmap = layout.Make(sample, nav, demo, passive)) bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
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
                else mousePressed = false;
                GameFiber.Yield();
            }
        }

        private static Keys Parse(string value, Keys fallback) =>
            Enum.TryParse(value, true, out Keys key) && Enum.IsDefined(typeof(Keys), key) && key != Keys.None ? key : fallback;

        private static bool Pressed(Keys key, HashSet<Keys> edges)
        {
            if (Game.IsKeyDownRightNow(key)) return edges.Add(key);
            edges.Remove(key);
            return false;
        }

        private static bool Navigate(Navigation nav, HashSet<Keys> edges, Keys toggle, bool togglePressed, bool escape)
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
            int delta = (Pressed(Keys.Right, edges) ? 1 : 0) - (Pressed(Keys.Left, edges) ? 1 : 0)
                + (Pressed(Keys.Down, edges) ? 3 : 0) - (Pressed(Keys.Up, edges) ? 3 : 0)
                + (Pressed(Keys.PageDown, edges) ? InventoryView.Page : 0) - (Pressed(Keys.PageUp, edges) ? InventoryView.Page : 0);
            if (NativeFunction.Natives.IS_CONTROL_JUST_PRESSED<bool>(0, 241)) delta -= InventoryView.Page;
            if (NativeFunction.Natives.IS_CONTROL_JUST_PRESSED<bool>(0, 242)) delta += InventoryView.Page;
            if (delta != 0) { nav.Selected = Math.Max(0, nav.Selected + delta); return true; }
            return false;
        }

        private static RectangleF Fit(Size size)
        {
            float scale = Math.Min(size.Width * .90f / InventoryView.Width, size.Height * .86f / InventoryView.Height);
            float w = InventoryView.Width * scale, h = InventoryView.Height * scale;
            return new RectangleF((size.Width - w) / 2, (size.Height - h) / 2, w, h);
        }

        private static void Draw(object sender, Rage.GraphicsEventArgs args)
        {
            try { lock (RenderLock) if (texture != null) args.Graphics.DrawTexture(texture, destination); }
            catch (Exception e) { stop = true; Game.LogTrivial("[ElectroInventory] Falha de renderização: " + e.Message); }
        }

        private static void ReleaseTexture()
        {
            lock (RenderLock)
            {
                if ((object)texture is IDisposable disposable) disposable.Dispose();
                texture = null;
            }
        }

        public static void Finally()
        {
            stop = true;
            Game.RawFrameRender -= Draw;
            ReleaseTexture();
            if (temp == null) return;
            try { Directory.Delete(temp, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            temp = null;
        }
    }
}
