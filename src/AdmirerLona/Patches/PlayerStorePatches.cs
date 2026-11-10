using System.Collections.Generic;
using HarmonyLib;

namespace AdmirerLona
{
    /// <summary>玩家把物品卖出去（给顾客）时触发：卖药检测 + 送礼。</summary>
    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.SellItem))]
    internal static class Patch_SellItem
    {
        private static bool Prefix(GameItem item, out bool __state)
        {
            __state = false;
            if (Gift.ShouldBlock(item))
            {
                // Harmony 即使 Prefix 返回 false 也仍会执行 Postfix；
                // 这里主动退出送礼状态，Postfix 用 __state 跳过后续结算。
                Gift.Disarm();
                return false;
            }
            Gift.Capture(item);
            __state = true;
            return true;
        }

        private static void Postfix(GameItem item, bool __state)
        {
            if (!__state) return;
            Gift.OnSold(item);
            Notes.OnItemSold(item);
        }
    }

    /// <summary>「一键清空柜台」不走 SellItem，单独补一次卖药检测。</summary>
    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.SellAllItemFromTable))]
    internal static class Patch_SellAllItemFromTable
    {
        private static int cashBefore;
        private static int countBefore;
        private static bool hasNarcotic;

        private static void Prefix()
        {
            cashBefore = PlayerStore.Instance != null ? PlayerStore.Instance.playerCash : 0;
            countBefore = 0;
            if (EmporiumEntry.Instance != null && EmporiumEntry.Instance.backInvinvElement != null)
            {
                countBefore = EmporiumEntry.Instance.backInvinvElement.childItems.Count;
            }
            hasNarcotic = Notes.ContainsNarcoticInBackInventory();
        }

        private static void Postfix()
        {
            if (!hasNarcotic) return;

            int countAfter = 0;
            if (EmporiumEntry.Instance != null && EmporiumEntry.Instance.backInvinvElement != null)
            {
                countAfter = EmporiumEntry.Instance.backInvinvElement.childItems.Count;
            }

            // 物品被清掉（即使 0 元成交）或现金增加，都说明这次清空确实成交了。
            bool sold = countAfter < countBefore
                || (PlayerStore.Instance != null && PlayerStore.Instance.playerCash > cashBefore);
            if (sold) State.RegisterDrugSale();
        }
    }

    /// <summary>
    /// 100 好感免费送货：买入价仍由 FreeFeature 压到 0；
    /// 成交后移除 free，使玩家转卖时恢复物品原价。
    /// </summary>
    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.BuyItem))]
    internal static class Patch_FreeDeliveryItem
    {
        private static void Postfix(GameItem item)
        {
            if (item == null) return;
            if (PlayerStore.Instance == null || PlayerStore.Instance.currentClientInstance == null) return;

            StoreClient client = PlayerStore.Instance.currentClientInstance.GetClientBlueprint();
            if (client == null || !client.IsTag(LonaClient.DeliveryTag)) return;

            if (item.FindItemFeatureByID("free") != null)
            {
                item.RemoveItemFeatureByID("free");
            }
        }
    }

    /// <summary>
    /// 以物易物：OnItemsSold 只包含玩家交出去的物品（出售方），
    /// 玩家收到的物品不在这个列表里，所以不会把“买入”当成卖药。
    /// </summary>
    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.OnItemsSold))]
    internal static class Patch_OnItemsSold
    {
        private static void Postfix(List<GameItem> gameItems)
        {
            Notes.OnItemsSold(gameItems);
        }
    }
}
