using System;
using HarmonyLib;

namespace AdmirerLona
{
    /// <summary>关店时洛娜在店：固定台词，替代原版 dialog_util_shutter_*。</summary>
    [HarmonyPatch(typeof(StoreClientDialogList), "CreateShutterDownClose")]
    internal static class Patch_LonaShutterClose
    {
        private static bool Prefix(ref Dialogue __result)
        {
            if (!LonaClient.IsCurrentLona()) return true;
            __result = LonaClient.BuildShutterDialogue();
            return false;
        }
    }

    /// <summary>洛娜在店时拔枪：固定两句，替代原版随机反应。</summary>
    [HarmonyPatch(typeof(AugHelper), nameof(AugHelper.GetRandomClientReactionUnholster))]
    internal static class Patch_LonaUnholster
    {
        private static bool Prefix(ref Dialogue __result)
        {
            if (!LonaClient.IsCurrentLona()) return true;
            __result = LonaClient.BuildUnholsterDialogue();
            return false;
        }
    }

    /// <summary>拔枪后击杀洛娜：永久离开，之后不再出现、不再有剧情和纸条。</summary>
    [HarmonyPatch(typeof(StoreUIManager), nameof(StoreUIManager.Shoot))]
    internal static class Patch_LonaShot
    {
        private static void Postfix()
        {
            try
            {
                StoreUIManager ui = StoreUIManager.Instance;
                if (ui == null || ui.shotClientInstance == null) return;
                StoreClient client = ui.shotClientInstance.GetClientBlueprint();
                if (client != null && client.identifier == LonaClient.LonaId && !State.Left)
                {
                    // 人已经不在了：洛娜自己的纸条和前置模组的仰慕者纸条一起停掉。
                    try { State.StopBaseNotes(); }
                    catch (Exception e) { ModEntry.Log("停止仰慕者纸条失败: " + e.Message); }
                    try { State.Leave("shot by player"); }
                    catch (Exception e) { ModEntry.Log("洛娜被击杀后离开失败: " + e.Message); }
                }
            }
            catch (Exception e)
            {
                ModEntry.Log("处理洛娜被击杀失败: " + e.Message);
            }
        }
    }
}
