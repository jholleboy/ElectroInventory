using System;
using System.Drawing;
using System.Collections.Generic;

namespace ElectroInventory
{
    internal static class OverlaySession
    {
        public static bool Matches(Guid openSession, Guid snapshotSession, int snapshotCharacter,
            int currentCharacter, bool available, bool demo) =>
            available && openSession != Guid.Empty && openSession == snapshotSession &&
            (demo || (currentCharacter != 0 && snapshotCharacter == currentCharacter));

        public static bool CloseRequested(bool contextual, bool ownsInput, bool inspectPressed,
            bool togglePressed, bool escapePressed, bool searching, bool closeButton) =>
            (contextual && inspectPressed) || (ownsInput && (closeButton || (escapePressed && !searching))) ||
            (!contextual && togglePressed && !searching);
    }

    internal sealed class PauseGuard
    {
        private bool captured;
        // Also suppress the release frame, which some pause bindings use.
        public bool Update(bool ownsInput, bool escapeDown)
        {
            bool suppress = ownsInput || captured;
            captured = escapeDown && suppress;
            return suppress;
        }
    }

    internal sealed class PointerCapture
    {
        private bool armed;
        public bool Click(bool ownsInput, bool down)
        {
            if (!ownsInput) { armed = false; return false; }
            if (!down) { armed = true; return false; }
            if (!armed) return false;
            armed = false;
            return true;
        }
    }

    internal sealed class HitTargets
    {
        private readonly List<Tuple<Rectangle, Action>> targets = new List<Tuple<Rectangle, Action>>();
        public void Clear() => targets.Clear();
        public void Add(Rectangle region, Action action) => targets.Add(Tuple.Create(region, action));
        public bool Click(Point point)
        {
            var hit = targets.FindLast(target => target.Item1.Contains(point));
            if (hit == null) return false;
            hit.Item2();
            return true;
        }
    }

    internal static class OverlayViewport
    {
        public static RectangleF Fit(Size viewport, Size design)
        {
            if (viewport.Width <= 0 || viewport.Height <= 0 || design.Width <= 0 || design.Height <= 0)
                return RectangleF.Empty;
            float scale = Math.Min(viewport.Width * .90f / design.Width, viewport.Height * .86f / design.Height);
            float width = design.Width * scale, height = design.Height * scale;
            return new RectangleF((viewport.Width - width) / 2, (viewport.Height - height) / 2, width, height);
        }
    }

    internal static class OverlayPointer
    {
        public static bool TryMap(float x, float y, RectangleF panel, Size logicalSize, out Point point)
        {
            point = Point.Empty;
            if (float.IsNaN(x) || float.IsNaN(y) || panel.Width <= 0 || panel.Height <= 0 || !panel.Contains(x, y))
                return false;
            point = new Point((int)((x - panel.Left) * logicalSize.Width / panel.Width),
                (int)((y - panel.Top) * logicalSize.Height / panel.Height));
            return true;
        }
    }
}
