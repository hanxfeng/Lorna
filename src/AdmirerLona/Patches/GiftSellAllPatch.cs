using System.Collections.Generic;
using HarmonyLib;

namespace AdmirerLona
{
    /// <summary>
    /// 送礼时点「一键卖出」，游戏会把柜台上所有可卖物品塞进 SellMultiple 连续结算。
    /// 第一件是礼物，后面的物品会按普通交易继续卖；有些物品（纸条等）本来不会被洛娜收，
    /// 因为送礼时 willAcceptItem 被临时放开，它们也会被塞进列表，容易在后续结算里卡死。
    /// 这里在送礼状态下把列表截成一件：只结算礼物，其余物品留在柜台上。
    /// </summary>
    [HarmonyPatch(typeof(NegociationUIManager), nameof(NegociationUIManager.SellMultiple))]
    internal static class Patch_GiftSellMultiple
    {
        private static void Prefix(ref List<GameItem> items)
        {
            if (items == null || items.Count <= 1) return;
            if (!Gift.IsArmedForCurrentClient()) return;

            // SellMultiple 从列表末尾往前卖，保留最后一件正好对应原来第一件成交的物品。
            items = new List<GameItem> { items[items.Count - 1] };
            ModEntry.Log("送礼时一键卖出只结算一件礼物，其余物品留在柜台");
        }
    }
}
