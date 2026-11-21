using System;
using System.Drawing;

namespace ElectroInventory
{
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
