using System;
using HarmonyLib;

namespace AdmirerLona
{
    /// <summary>
    /// 下层区声望变化：声望每 +2，好感 +1（即声望增量的 50%）。
    /// 另外在这里补一次激活检查（玩家可能在交易中跨过阈值）。
    /// </summary>
    [HarmonyPatch(typeof(StoreReputation), "ModReputation", new Type[] { typeof(double) })]
    internal static class Patch_ModReputation
    {
        private static void Prefix(StoreReputation __instance, ref double __state)
        {
            __state = __instance != null ? __instance.GetReputationExact() : 0.0;
        }

        private static void Postfix(StoreReputation __instance, double __state)
        {
            if (__instance == null) return;
            if (!string.Equals(__instance.factionId, StoreReputation.FACTION_LOWER_LEVEL)) return;
            State.OnLowerRepChanged(__state);
        }
    }

    [HarmonyPatch(typeof(StoreReputation), "ModReputationRaw", new Type[] { typeof(double) })]
    internal static class Patch_ModReputationRaw
    {
        private static void Prefix(StoreReputation __instance, ref double __state)
        {
            __state = __instance != null ? __instance.GetReputationExact() : 0.0;
        }

        private static void Postfix(StoreReputation __instance, double __state)
        {
            if (__instance == null) return;
            if (!string.Equals(__instance.factionId, StoreReputation.FACTION_LOWER_LEVEL)) return;
            State.OnLowerRepChanged(__state);
        }
    }
}
