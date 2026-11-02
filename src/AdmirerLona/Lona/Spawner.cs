using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdmirerLona
{
    /// <summary>调试面板排队的强制出场。</summary>
    internal static class DebugForce
    {
        public static bool Pending;
        public static bool Thief;
        public static LonaVisit Visit = LonaVisit.Buy;
        public static LonaStory Story = LonaStory.Daily;
        public static int DailyTier = -1;

        public static void Clear()
        {
            Pending = false;
            Thief = false;
            DailyTier = -1;
            Visit = LonaVisit.Buy;
            Story = LonaStory.Daily;
        }
    }

    /// <summary>顾客生成阶段的出场逻辑。</summary>
    internal static class Spawner
    {
        /// <summary>临时值：150 声望之后每天出现的概率（百分比）。</summary>
        public const int AppearChance = 25;

        /// <summary>临时值：两次到访之间至少隔几天。</summary>
        public const int CooldownDays = 3;

        /// <summary>150 声望之前顶替小偷的概率（百分比）。</summary>
        public const int ThiefReplaceChance = 10;

        private static readonly string[] ThiefIds = { "thief", "pettyThief", "foodThief" };

        public static void OnGenerate(StoreClientManager manager)
        {
            try
            {
                if (manager == null || manager.clientStack == null) return;
                if (!State.Ready) return;

                Portrait.EnsureInjected();

                if (DebugForce.Pending)
                {
                    ApplyDebug(manager);
                    return;
                }

                if (!StartingPerk.IsSecretAdmirer()) return;
                if (State.Left) return;
                if (!BaseModSchema.Ok) return;

                State.CheckActivation();

                // ---- 150 声望之前：10% 顶替小偷，只换立绘 ----
                if (!State.Activated)
                {
                    if (RNG.Roll(ThiefReplaceChance))
                    {
                        ReplaceFirstThief(manager, LonaClient.CreateThiefForm());
                    }
                    return;
                }

                // ---- 首次登场：阈值后第二天 ----
                if (!State.FirstSeen)
                {
                    int day = State.FirstAppearDay;
                    if (day < 0 || State.Today >= day)
                    {
                        InsertFront(manager, LonaClient.Create(LonaVisit.Story, LonaStory.FirstMeet));
                        State.MarkFirstSeen();
                        State.MarkVisited();
                        Gift.Reset();
                        ModEntry.Log("首次登场：第 " + State.Today + " 天");
                        return;
                    }
                }

                // ---- 待播的特殊剧情 ----
                string pending = State.PendingSpecial;
                if (!string.IsNullOrEmpty(pending))
                {
                    LonaStory story = ParseStory(pending);
                    State.PendingSpecial = "";
                    InsertFront(manager, LonaClient.Create(LonaVisit.Story, story));
                    State.MarkVisited();
                    Gift.Reset();
                    ModEntry.Log("特殊剧情：" + story);
                    return;
                }

                // ---- 日常到访 ----
                if (HasLona(manager.clientStack)) return;
                if (State.Today - State.LastVisitDay < CooldownDays) return;
                if (!RNG.Roll(AppearChance)) return;

                LonaVisit visit = RNG.Roll(50) ? LonaVisit.Buy : LonaVisit.Sell;
                StoreClient client = LonaClient.Create(visit, LonaStory.Daily);
                Gift.Reset();
                if (!ReplaceFirstThief(manager, client))
                {
                    manager.AddClient(client);
                }
                State.MarkVisited();
                ModEntry.Log("日常到访：" + visit + "（好感 " + State.AffectionInt + "）");
            }
            catch (Exception e)
            {
                ModEntry.Log("生成洛娜失败: " + e);
            }
        }

        private static void ApplyDebug(StoreClientManager manager)
        {
            StoreClient client;
            if (DebugForce.Thief)
            {
                client = LonaClient.CreateThiefForm();
                ModEntry.Log("调试：小偷形态已排队");
            }
            else
            {
                client = LonaClient.Create(DebugForce.Visit, DebugForce.Story, DebugForce.DailyTier);
                ModEntry.Log("调试：剧情 " + DebugForce.Story + " / 到访 " + DebugForce.Visit + " 已排队");
            }
            InsertFront(manager, client);
            DebugForce.Clear();
        }

        private static void InsertFront(StoreClientManager manager, StoreClient client)
        {
            if (client == null) return;
            manager.AddNextClient(client);
        }

        private static bool ReplaceFirstThief(StoreClientManager manager, StoreClient client)
        {
            if (client == null) return false;
            List<StoreClient> stack = manager.clientStack;
            for (int i = 0; i < stack.Count; i++)
            {
                StoreClient existing = stack[i];
                if (existing == null || existing.identifier == null) continue;
                if (!IsThief(existing.identifier)) continue;

                stack[i] = client;
                manager.TrackClient(client);
                ModEntry.Log("顶替小偷：" + existing.identifier + " → " + client.identifier);
                return true;
            }
            return false;
        }

        private static bool IsThief(string identifier)
        {
            for (int i = 0; i < ThiefIds.Length; i++)
            {
                if (identifier == ThiefIds[i]) return true;
            }
            return false;
        }

        private static bool HasLona(List<StoreClient> stack)
        {
            for (int i = 0; i < stack.Count; i++)
            {
                StoreClient client = stack[i];
                if (client != null && client.identifier == LonaClient.LonaId) return true;
            }
            return false;
        }

        private static LonaStory ParseStory(string raw)
        {
            try
            {
                return (LonaStory)Enum.Parse(typeof(LonaStory), raw);
            }
            catch (Exception)
            {
                return LonaStory.Below70;
            }
        }
    }
}
