using HarmonyLib;

namespace AdmirerLona
{
    /// <summary>
    /// 游戏的电话簿/来电界面用 locID 走本地化表取名字，模组没有往游戏表里加词条，
    /// 这里只对洛娜这条电话记录改用代码里的显示名。
    /// </summary>
    [HarmonyPatch(typeof(StorePhoneClient), nameof(StorePhoneClient.GetLocalizedDisplayName))]
    internal static class PhoneNamePatch
    {
        private static bool Prefix(StorePhoneClient __instance, ref string __result)
        {
            if (__instance != null && __instance.dialogFuncId == Phone.DialogId)
            {
                __result = LonaText.DisplayName;
                return false;
            }
            return true;
        }
    }
}
