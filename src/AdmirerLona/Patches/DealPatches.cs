using HarmonyLib;

namespace AdmirerLona
{
    /// <summary>
    /// 洛娜的成交后固定台词。原版 OnDealAccepted 会接上
    /// dialog_util_after_sell / after_buy / dialog_deal_done 等原版句子，
    /// 这里整体替换掉。
    /// </summary>
    [HarmonyPatch(typeof(StoreClient), nameof(StoreClient.OnDealAccepted))]
    internal static class Patch_LonaDealAccepted
    {
        private static bool Prefix(StoreClient __instance, Dialogue hostLine)
        {
            return !LonaClient.HandleDealAccepted(__instance, hostLine);
        }
    }
}
