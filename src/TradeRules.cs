using System;

namespace ElectroInventory.Wire
{
    internal static class TradeRules
    {
        public static int BuyLimit(int maximum, int stock, int sold) =>
            Math.Max(0, stock < 0 ? maximum : (int)Math.Max(0, Math.Min(maximum, (long)stock - sold)));

        public static int SellLimit(float quantity, int demand, int bought)
        {
            if (float.IsNaN(quantity) || quantity <= 0 || quantity >= int.MaxValue) return 0;
            int owned = (int)Math.Floor(quantity);
            return demand < 0 ? owned : (int)Math.Max(0, Math.Min(owned, (long)demand - bought));
        }

        public static bool NpcCanSettle(bool buy, int npcCash, int price, int amount, float listedStock, bool finite)
        {
            if (npcCash < 0 || amount <= 0 || price < 0) return false;
            long total = (long)price * amount;
            if (!buy) return total <= npcCash;
            return total <= int.MaxValue - (long)npcCash && (!finite || listedStock >= amount);
        }

        public static bool QuoteAllows(bool permitted, int quotedPrice, int currentPrice,
            int quantity, int maximum, int balance, bool buy) =>
            permitted && quotedPrice == currentPrice && quantity <= maximum &&
            ActionProtocol.ValidPrice(currentPrice, quantity, balance, buy);
    }
}
