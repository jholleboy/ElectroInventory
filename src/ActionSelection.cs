using System;
using ElectroInventory.Wire;

namespace ElectroInventory
{
    internal sealed class ActionSelection
    {
        private static readonly ActionKind[] Order = { ActionKind.Use, ActionKind.Discard, ActionKind.Deposit,
            ActionKind.Take, ActionKind.Buy, ActionKind.Sell };
        public Guid Revision { get; private set; }
        public ActionOffer Offer { get; private set; }
        public ActionKind Kind { get; private set; }
        public int Quantity { get; private set; } = 1;
        public bool Confirming { get; set; }

        public void Bind(Guid revision, ActionOffer offer)
        {
            if (Revision == revision && Offer?.Token == offer?.Token) return;
            Revision = revision;
            Offer = offer;
            Quantity = 1;
            Kind = ActionKind.None;
            Confirming = false;
            NextKind();
        }

        public void NextKind()
        {
            if (Offer == null) return;
            int index = Array.IndexOf(Order, Kind);
            for (int i = 1; i <= Order.Length; i++)
            {
                ActionKind candidate = Order[(index + i) % Order.Length];
                if ((Offer.Kinds & candidate) == 0) continue;
                Kind = candidate;
                Quantity = 1;
                Confirming = false;
                return;
            }
        }
        public void Adjust(int direction)
        {
            if (Offer == null || Confirming || Kind == ActionKind.Use) return;
            if (Kind == ActionKind.Discard)
            {
                if (direction > 0) Quantity = Quantity == 1 && Offer.Maximum > 5 ? 5 : Offer.Maximum;
                else Quantity = Quantity == Offer.Maximum && Offer.Maximum > 5 ? 5 : 1;
            }
            else Quantity = (int)Math.Max(1, Math.Min(Offer.Maximum, (long)Quantity + direction));
        }

        public static string Label(ActionKind kind)
        {
            switch (kind)
            {
                case ActionKind.Use: return "Usar / ativar";
                case ActionKind.Discard: return "Descartar";
                case ActionKind.Deposit: return "Guardar";
                case ActionKind.Take: return "Retirar";
                case ActionKind.Buy: return "Comprar";
                case ActionKind.Sell: return "Vender";
                default: return "";
            }
        }
    }
}
