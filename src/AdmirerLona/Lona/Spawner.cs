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
        public static int DailyStory;

        public static void Clear()
        {
            Pending = false;
            Thief = false;
            DailyStory = 0;
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

        /// <summary>激活之前顶替小偷的概率（百分比）。</summary>
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

                if (State.PendingTierDay >= 0 || State.PendingDeliveryDay >= 0)
                {
                    ModEntry.Log("待处理：today=" + State.Today
                        + " tier=" + State.PendingTierStory + "@" + State.PendingTierDay
                        + " delivery=" + State.PendingDeliveryCategory + "@" + State.PendingDeliveryDay);
                }

                // ---- 上层区声望档位提升：第二天队首，纯剧情 ----
                bool tierStoryQueued = false;
                if (State.PendingTierDay >= 0 && State.Today >= State.PendingTierDay)
                {
                    LonaStory tierStory = (LonaStory)State.PendingTierStory;
                    State.ClearTierStory();
                    if (tierStory == LonaStory.Upper70 || tierStory == LonaStory.Upper140)
                    {
                        InsertFront(manager, LonaClient.Create(LonaVisit.Story, tierStory));
                        State.MarkVisited();
                        Gift.Reset();
                        tierStoryQueued = true;
                        ModEntry.Log("档位剧情：" + tierStory + "（队首）");
                    }
                }

                // ---- 电话预约送货：第二天队尾，正常交易（100 好感免费） ----
                if (State.PendingDeliveryDay >= 0 && State.Today >= State.PendingDeliveryDay)
                {
                    string category = State.PendingDeliveryCategory;
                    int count = State.PendingDeliveryCount;
                    bool free = State.PendingDeliveryFree;
                    int tier = State.PendingDeliveryTier;
                    State.ClearDelivery();

                    if (string.IsNullOrEmpty(category) || count <= 0)
                    {
                        ModEntry.Log("电话送货数据不完整，已跳过");
                        return;
                    }

                    List<GameItem> items = Delivery.Build(category, tier, count, State.DeliveryTotalValueValue);
                    StoreClient delivery = LonaClient.CreateDelivery(items, free);
                    manager.AddClient(delivery);
                    State.MarkVisited();
                    Gift.Reset();
                    ModEntry.Log("电话送货到店：" + category + " x" + items.Count + (free ? "（免费）" : ""));
                    return;
                }

                if (tierStoryQueued) return;

                // ---- 激活之前：小偷/扒手/食物窃贼有 10% 概率换成她的立绘 ----
                if (!State.Activated)
                {
                    TryReplaceThiefSprite(manager);
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

                // ---- 揭面后第二天：队首正式自我介绍 ----
                if (State.PostRevealDay >= 0 && State.Today >= State.PostRevealDay)
                {
                    State.ClearPostReveal();
                    InsertFront(manager, LonaClient.Create(LonaVisit.Story, LonaStory.PostReveal));
                    State.MarkVisited();
                    Gift.Reset();
                    ModEntry.Log("揭面后介绍（队首）");
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
                    StoreClient special = LonaClient.Create(LonaVisit.Story, story);
                    if (story == LonaStory.Reach100) InsertFront(manager, special);
                    else manager.AddClient(special);
                    State.MarkVisited();
                    Gift.Reset();
                    ModEntry.Log("特殊剧情：" + story + (story == LonaStory.Reach100 ? "（队首）" : "（队尾）"));
                    return;
                }

                // ---- 日常到访：50% 剧情，50% 普通买卖 ----
                if (HasLona(manager.clientStack)) return;
                if (State.Today - State.LastVisitDay < CooldownDays) return;
                if (!RNG.Roll(AppearChance)) return;

                if (RNG.Roll(50))
                {
                    int storyId = PickDailyStory();
                    if (storyId > 0)
                    {
                        bool trade = LonaText.DailyStoryTrades(storyId);
                        LonaVisit storyVisit = trade ? LonaVisit.StoryBuy : LonaVisit.Story;
                        StoreClient storyClient = LonaClient.Create(storyVisit, LonaStory.Daily, storyId);
                        if (LonaText.DailyStoryQueueHead(storyId)) InsertFront(manager, storyClient);
                        else if (!ReplaceFirstThief(manager, storyClient)) manager.AddClient(storyClient);
                        State.MarkVisited();
                        Gift.Reset();
                        ModEntry.Log("日常剧情：" + storyId + (trade ? "（购买）" : "") + (LonaText.DailyStoryQueueHead(storyId) ? "（队首）" : "（队尾）"));
                        return;
                    }
                }

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

        private static int PickDailyStory()
        {
            bool replay = State.ReplayDailyStoriesValue;
            List<int> candidates = new List<int>();
            if (State.DailyPoolAll && replay)
            {
                for (int id = 1; id <= 35; id++)
                {
                    if (id != 30) candidates.Add(id);
                }
            }
            else
            {
                int from;
                int to;
                int affection = State.AffectionInt;
                if (affection < 80) { from = 1; to = 10; }
                else if (affection < 90) { from = 11; to = 20; }
                else if (affection < 100) { from = 21; to = 29; }
                else { from = 31; to = 35; }

                for (int id = from; id <= to; id++)
                {
                    if (!State.DailyStorySeen(id)) candidates.Add(id);
                }

                if (candidates.Count == 0 && affection >= 100 && replay)
                {
                    State.EnableDailyPoolAll();
                    for (int id = 1; id <= 35; id++)
                    {
                        if (id != 30) candidates.Add(id);
                    }
                }
            }

            if (candidates.Count == 0) return 0;
            int picked = candidates[RNG.GetRandomInt(0, candidates.Count - 1)];
            State.MarkDailyStorySeen(picked);
            if (State.All100DailySeen() && replay) State.EnableDailyPoolAll();
            return picked;
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
                client = LonaClient.Create(DebugForce.Visit, DebugForce.Story, DebugForce.DailyStory);
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

        /// <summary>
        /// 激活之前：把队列里第一个小偷/扒手/食物窃贼的立绘换成洛娜。
        /// 只改 spriteName —— identifier、displayName、对话、批发折扣和上货表全部保持原样，
        /// 所以行为和原来那一种小偷完全一致，也不会写任何状态或触发剧情。
        /// </summary>
        private static void TryReplaceThiefSprite(StoreClientManager manager)
        {
            if (manager == null || manager.clientStack == null) return;

            StoreClient thief = FindFirstThief(manager.clientStack);
            if (thief == null) return;

            if (!RNG.Roll(ThiefReplaceChance)) return;

            if (!Portrait.Has(Portrait.KeyThief))
            {
                ModEntry.Log("窃贼洛娜：lona_thief 贴图未加载，保持原版立绘");
                return;
            }

            thief.spriteName = Portrait.KeyThief;
            ModEntry.Log("顶替小偷：" + thief.identifier + "（只换立绘，名字/行为保持原版）");
        }

        private static StoreClient FindFirstThief(List<StoreClient> stack)
        {
            if (stack == null) return null;
            for (int i = 0; i < stack.Count; i++)
            {
                StoreClient client = stack[i];
                if (client != null && client.identifier != null && IsThief(client.identifier))
                {
                    return client;
                }
            }
            return null;
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
