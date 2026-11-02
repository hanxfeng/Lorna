using HarmonyLib;

namespace AdmirerLona
{
    /// <summary>玩家把物品卖出去（给顾客）时触发：卖药检测 + 送礼。</summary>
    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.SellItem))]
    internal static class Patch_SellItem
    {
        private static bool Prefix(GameItem item)
        {
            if (Gift.ShouldBlock(item)) return false;
            Gift.Capture(item);
            return true;
        }

        private static void Postfix(GameItem item)
        {
            Gift.OnSold(item);
            Notes.OnItemSold(item);
        }
    }

    /// <summary>「一键清空柜台」不走 SellItem，单独补一次卖药检测。</summary>
    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.SellAllItemFromTable))]
    internal static class Patch_SellAllItemFromTable
    {
        private static int cashBefore;
        private static bool hasNarcotic;

        private static void Prefix()
        {
            cashBefore = PlayerStore.Instance != null ? PlayerStore.Instance.playerCash : 0;
            hasNarcotic = Notes.ContainsNarcoticInBackInventory();
        }

        private static void Postfix()
        {
            if (!hasNarcotic) return;
            if (PlayerStore.Instance == null || PlayerStore.Instance.playerCash <= cashBefore) return;
            State.RegisterDrugSale();
        }
    }
}
