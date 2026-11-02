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
        /// <summary>临时值：揭面之后每天出现的概率（百分比）。</summary>
        public const int AppearChance = 25;

        /// <summary>临时值：两次到访之间至少隔几天。</summary>
        public const int CooldownDays = 3;

        /// <summary>150 声望之前顶替小偷的概率（百分比）。</summary>
        public const int ThiefReplaceChance = 10;

        /// <summary>第一次见面之后，隔几天来揭面。</summary>
        public const int RevealDelayDays = 3;

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

                // ---- 第一段：来买食物 ----
                if (!State.FirstSeen)
                {
                    int day = State.FirstAppearDay;
                    if (day < 0 || State.Today >= day)
                    {
                        InsertFront(manager, LonaClient.Create(LonaVisit.FirstPurchase, LonaStory.FirstMeet));
                        State.MarkFirstMet();
                        Gift.Reset();
                        ModEntry.Log("初次见面（第一段）：第 " + State.Today + " 天");
                    }
                    return;
                }

                // ---- 第二段：三天后揭面，期间她不再出现 ----
                if (!State.Revealed)
                {
                    int revealDay = State.FirstMetDay + RevealDelayDays;
                    if (State.Today >= revealDay)
                    {
                        State.PauseBaseNotesToday();
                        InsertFront(manager, LonaClient.Create(LonaVisit.Reveal, LonaStory.FirstMeet));
                        State.MarkRevealed();
                        Gift.Reset();
                        ModEntry.Log("初次见面（第二段·揭面）：第 " + State.Today + " 天");
                    }
                    return;
                }

                // ---- 待播的特殊剧情 ----
                string pending = State.PendingSpecial;
                if (!string.IsNullOrEmpty(pending))
                {
                    State.PendingSpecial = "";
                    LonaStory story;
                    if (!TryParseStory(pending, out story))
                    {
                        ModEntry.Log("丢弃无法识别的特殊剧情：" + pending);
                        return;
                    }
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

        /// <summary>只接受「能当剧情播」的枚举值；旧存档里已删掉的（例如 Below70）一律丢弃。</summary>
        private static bool TryParseStory(string raw, out LonaStory story)
        {
            story = LonaStory.Auto;
            try
            {
                LonaStory parsed = (LonaStory)Enum.Parse(typeof(LonaStory), raw);
                if (!Enum.IsDefined(typeof(LonaStory), parsed)) return false;
                if (parsed == LonaStory.Auto || parsed == LonaStory.Daily || parsed == LonaStory.ThiefForm
                    || parsed == LonaStory.FirstMeet || parsed == LonaStory.Gift || parsed == LonaStory.GiftRefused)
                {
                    return false;
                }
                story = parsed;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
