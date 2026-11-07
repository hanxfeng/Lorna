using System;
using System.Globalization;
using UnityEngine;

namespace AdmirerLona
{
    /// <summary>
    /// 存档状态与配置。所有与本局游戏绑定的数据都走 ModData（随存档保存）。
    /// 好感度内部用 double 存、界面按 int 显示，和声望一致。
    /// </summary>
    internal static class State
    {
        public const string ModId = "admirer_lona";
        public const string BaseModId = "Enhanced_Secret_Admirers_And_More_News";

        /// <summary>首次出现所需的「下层区声望」阈值，玩家可在模组配置里改。</summary>
        public static ConfigEntry<int> UnlockRep;

        /// <summary>普通物品（材料/工具/日用品/食物/医疗品）的送货冷却天数。</summary>
        public static ConfigEntry<int> DeliveryDaysNormal;

        /// <summary>特殊类（水培农场/水产养殖/养殖/酿酒/水商）的送货冷却天数。</summary>
        public static ConfigEntry<int> DeliveryDaysSpecial;

        /// <summary>电话预约送货的目标总价值。</summary>
        public static ConfigEntry<int> DeliveryTotalValue;

        /// <summary>播完全部日常剧情后，是否把所有日常剧情重新加入池子轮播。</summary>
        public static ConfigEntry<bool> ReplayDailyStories;

        /// <summary>立绘固定缩放：1 = 与原版顾客同尺寸（世界尺寸 2.88 × 2.11）。</summary>
        public const float FixedPortraitScale = 1f;

        public static float PortraitScaleValue
        {
            get { return FixedPortraitScale; }
        }

        /// <summary>首次出现声望阈值（0–200）。</summary>
        public static int UnlockRepValue
        {
            get { return UnlockRep != null ? Mathf.Clamp(UnlockRep.Value, 0, 200) : 70; }
        }

        public static int NormalDeliveryDaysValue
        {
            get { return DeliveryDaysNormal != null ? Mathf.Clamp(DeliveryDaysNormal.Value, 1, 60) : 3; }
        }

        public static int SpecialDeliveryDaysValue
        {
            get { return DeliveryDaysSpecial != null ? Mathf.Clamp(DeliveryDaysSpecial.Value, 1, 60) : 5; }
        }

        public static int DeliveryTotalValueValue
        {
            get { return DeliveryTotalValue != null ? Mathf.Max(1000, DeliveryTotalValue.Value) : 1500; }
        }

        public static bool ReplayDailyStoriesValue
        {
            get { return ReplayDailyStories == null || ReplayDailyStories.Value; }
        }

        private const string KActivated = "activated";
        private const string KActivationRep = "activation_rep";
        private const string KAffection = "affection";
        private const string KLeft = "left";
        private const string KFirstSeen = "first_seen";
        private const string KFirstAppearDay = "first_appear_day";
        private const string KPendingSpecial = "pending_special";
        private const string KAbove80 = "special_above80";
        private const string KAbove90 = "special_above90";
        private const string KReach100 = "special_100";
        private const string KDrugCount = "drug_count";
        private const string KLastDrugDay = "last_drug_day";
        private const string KPendingNoteDay = "pending_note_day";
        private const string KPendingNoteIndex = "pending_note_index";
        private const string KLastVisitDay = "last_visit_day";
        private const string KFirstMetDay = "first_met_day";
        private const string KRevealed = "revealed";
        private const string KPostRevealDay = "post_reveal_day";
        private const string KSeenDailyLow = "seen_daily_low";
        private const string KSeenDailyHigh = "seen_daily_high";
        private const string KDailyPoolAll = "daily_pool_all";
        private const string KBaseNotesStopped = "base_notes_stopped";

        // 电话预约送货 / 上层区声望档位剧情
        private const string KPendingDeliveryDay = "pending_delivery_day";
        private const string KPendingDeliveryCategory = "pending_delivery_category";
        private const string KPendingDeliveryCount = "pending_delivery_count";
        private const string KPendingDeliveryFree = "pending_delivery_free";
        private const string KPendingDeliveryTier = "pending_delivery_tier";
        private const string KLastULRep = "last_ul_rep";
        private const string KUL70Story = "ul70_story";
        private const string KUL140Story = "ul140_story";
        private const string KPendingTierStory = "pending_tier_story";
        private const string KPendingTierDay = "pending_tier_day";

        // 下面两个键写在「前置模组」的 mod data 命名空间里，由前置模组读取。
        public const string KNotesPauseUntilRep = "notes_pause_until_rep";
        public const string KNotesPauseDay = "notes_pause_day";

        // ---- 运行时（不存档） ----
        public static bool GiftMode;

        public static bool Ready
        {
            get { return PlayerStore.Instance != null && StoreStation.Instance != null; }
        }

        public static int Today
        {
            get { return StoreStation.Instance != null ? StoreStation.Instance.dayCounter : 0; }
        }

        public static int Reputation
        {
            get { return StoreReputation.GetLLReputation(); }
        }

        // ---------------------------------------------------------------
        // 配置
        // ---------------------------------------------------------------
        public static void DefineConfig(string modId)
        {
            ModConfig config = ModHelper.GetConfig(modId);
            // 不带 min/max：配置面板里是数字输入框，不是滑条。
            UnlockRep = config.Define(
                "unlock_rep", 70, "Lorna",
                () => "首次出现声望",
                tooltip: () => "下层区声望达到该值的第二天，洛娜第一次出现（默认 70，0–200）。初始好感为当时声望的一半。");

            DeliveryDaysNormal = config.Define(
                "delivery_days_normal", 3, "Lorna",
                () => "普通物品送货间隔",
                tooltip: () => "材料/工具/日用品/食物/医疗品的预约送货冷却天数（默认 3，1–60）。");

            DeliveryDaysSpecial = config.Define(
                "delivery_days_special", 5, "Lorna",
                () => "特殊物品送货间隔",
                tooltip: () => "水培农场/水产养殖/养殖/酿酒/水商的预约送货冷却天数（默认 5，1–60）。");

            DeliveryTotalValue = config.Define(
                "delivery_total_value", 1500, "Lorna",
                () => "洛娜送货总价值上限",
                tooltip: () => "先抽好感对应的件数；若总价值超过该上限，则保留价值最高且不超过上限的组合（默认 1500，最低 1000，不设上限）。");

            ReplayDailyStories = config.Define(
                "replay_daily_stories", true, "Lorna",
                () => "轮播日常剧情",
                tooltip: () => "勾选：所有日常剧情播完后，把所有日常剧情重新加入池子继续随机抽取。不勾选：播完后不再出现日常剧情，洛娜之后只进行常规买卖。");
        }

        // ---------------------------------------------------------------
        // 基础状态
        // ---------------------------------------------------------------
        public static bool Activated
        {
            get { return GetBool(KActivated); }
        }

        public static bool Left
        {
            get { return GetBool(KLeft); }
        }

        public static bool FirstSeen
        {
            get { return GetBool(KFirstSeen); }
        }

        public static int FirstAppearDay
        {
            get { return GetInt(KFirstAppearDay, -1); }
        }

        public static int LastVisitDay
        {
            get { return GetInt(KLastVisitDay, -1000); }
        }

        public static double Affection
        {
            get { return GetDouble(KAffection, 0); }
            set { SetDouble(KAffection, Math.Max(0, value)); }
        }

        public static int AffectionInt
        {
            get { return (int)Affection; }
        }

        public static bool BaseNotesStopped
        {
            get { return GetBool(KBaseNotesStopped); }
        }

        public static string PendingSpecial
        {
            get { return ModHelper.GetModData(ModId, KPendingSpecial) ?? ""; }
            set
            {
                if (string.IsNullOrEmpty(value)) ModHelper.RemoveModData(ModId, KPendingSpecial);
                else ModHelper.SetModData(ModId, KPendingSpecial, value);
            }
        }

        // ---------------- 电话预约送货 ----------------
        public static int PendingDeliveryDay
        {
            get { return GetInt(KPendingDeliveryDay, -1); }
        }

        public static string PendingDeliveryCategory
        {
            get { return ModHelper.GetModData(ModId, KPendingDeliveryCategory) ?? ""; }
        }

        public static int PendingDeliveryCount
        {
            get { return GetInt(KPendingDeliveryCount, 0); }
        }

        public static bool PendingDeliveryFree
        {
            get { return GetBool(KPendingDeliveryFree); }
        }

        public static int PendingDeliveryTier
        {
            get { return GetInt(KPendingDeliveryTier, 0); }
        }

        public static void QueueDelivery(string category, int count, bool free, int tier, int day)
        {
            if (PendingDeliveryDay >= 0)
            {
                Log("覆盖已有送货预约：" + PendingDeliveryCategory + " @" + PendingDeliveryDay);
            }
            ModHelper.SetModData(ModId, KPendingDeliveryCategory, category);
            SetInt(KPendingDeliveryCount, count);
            SetBool(KPendingDeliveryFree, free);
            SetInt(KPendingDeliveryTier, tier);
            SetInt(KPendingDeliveryDay, day);
            Log($"Delivery queued: {category} x{count} free={free} tier={tier} day={day}");
        }

        public static void ClearDelivery()
        {
            ModHelper.RemoveModData(ModId, KPendingDeliveryCategory);
            ModHelper.RemoveModData(ModId, KPendingDeliveryCount);
            ModHelper.RemoveModData(ModId, KPendingDeliveryFree);
            ModHelper.RemoveModData(ModId, KPendingDeliveryTier);
            ModHelper.RemoveModData(ModId, KPendingDeliveryDay);
        }

        // ---------------- 上层区声望档位剧情 ----------------
        public static int PendingTierStory
        {
            get { return GetInt(KPendingTierStory, 0); }
        }

        public static int PendingTierDay
        {
            get { return GetInt(KPendingTierDay, -1); }
        }

        public static void QueueTierStory(int story, int day)
        {
            SetInt(KPendingTierStory, story);
            SetInt(KPendingTierDay, day);
            Log($"Upper-tier story queued: {story} day={day}");
        }

        public static void ClearTierStory()
        {
            ModHelper.RemoveModData(ModId, KPendingTierStory);
            ModHelper.RemoveModData(ModId, KPendingTierDay);
        }

        /// <summary>
        /// 开门时检查上层区声望是否跨过 70 / 140。只在本局内第一次跨过时排队，
        /// 剧情当天直接插到队首；老存档里已经超过的值不会补触发。
        /// </summary>
        public static void TrackUpperTier()
        {
            if (!Ready || Left) return;
            int current = StoreReputation.GetULReputation();
            int last = GetInt(KLastULRep, int.MinValue);
            if (last == int.MinValue)
            {
                SetInt(KLastULRep, current);
                return;
            }
            if (!Revealed)
            {
                SetInt(KLastULRep, current);
                return;
            }

            if (current > last)
            {
                bool done70 = GetBool(KUL70Story);
                bool done140 = GetBool(KUL140Story);

                if (current >= 140 && !done140)
                {
                    SetBool(KUL140Story, true);
                    SetBool(KUL70Story, true);
                    QueueTierStory((int)LonaStory.Upper140, Today);
                }
                else if (current >= 70 && !done70)
                {
                    SetBool(KUL70Story, true);
                    QueueTierStory((int)LonaStory.Upper70, Today);
                }
            }

            SetInt(KLastULRep, current);
        }

        /// <summary>日常剧情档位：-1 = 好感低于 70（不播日常剧情）。</summary>
        public static int DailyTier
        {
            get
            {
                int a = AffectionInt;
                if (a < 70) return -1;
                if (a < 80) return 0;
                if (a < 90) return 1;
                if (a < 100) return 2;
                return 3;
            }
        }

        public static int DrugCount
        {
            get { return GetInt(KDrugCount, 0); }
        }

        public static int PendingNoteDay
        {
            get { return GetInt(KPendingNoteDay, -1); }
        }

        /// <summary>第一次见面（来买食物那次）当天。</summary>
        public static int FirstMetDay
        {
            get { return GetInt(KFirstMetDay, -1); }
        }

        /// <summary>是否已经在第二段剧情里揭面。</summary>
        public static bool Revealed
        {
            get { return GetBool(KRevealed); }
        }

        /// <summary>上层区 70 的升职剧情是否已经触发（触发后她买货预算升到 300）。</summary>
        public static bool Upper70StoryDone
        {
            get { return GetBool(KUL70Story); }
        }

        public static void MarkFirstMet()
        {
            SetBool(KFirstSeen, true);
            SetInt(KFirstMetDay, Today);
            Log("First met on day " + Today);
        }

        public static void MarkRevealed()
        {
            SetBool(KRevealed, true);
            SetInt(KPostRevealDay, Today + 1);
            Log("Revealed on day " + Today);
        }

        /// <summary>揭面后第二天队首的正式自我介绍；-1 表示没有。</summary>
        public static int PostRevealDay
        {
            get { return GetInt(KPostRevealDay, -1); }
        }

        public static void ClearPostReveal()
        {
            ModHelper.RemoveModData(ModId, KPostRevealDay);
        }

        public static bool DailyStorySeen(int id)
        {
            if (id <= 0) return false;
            if (id <= 30)
            {
                return (GetInt(KSeenDailyLow, 0) & (1 << (id - 1))) != 0;
            }
            if (id <= 35)
            {
                return (GetInt(KSeenDailyHigh, 0) & (1 << (id - 31))) != 0;
            }
            return false;
        }

        public static void MarkDailyStorySeen(int id)
        {
            if (id <= 0) return;
            if (id <= 30)
            {
                SetInt(KSeenDailyLow, GetInt(KSeenDailyLow, 0) | (1 << (id - 1)));
            }
            else if (id <= 35)
            {
                SetInt(KSeenDailyHigh, GetInt(KSeenDailyHigh, 0) | (1 << (id - 31)));
            }
        }

        /// <summary>100 档的 31–35 是否全部看过。</summary>
        public static bool All100DailySeen()
        {
            for (int id = 31; id <= 35; id++)
            {
                if (!DailyStorySeen(id)) return false;
            }
            return true;
        }

        public static bool DailyPoolAll
        {
            get { return GetBool(KDailyPoolAll); }
        }

        public static void EnableDailyPoolAll()
        {
            SetBool(KDailyPoolAll, true);
        }

        // ---------------- 前置模组的纸条暂停 ----------------

        /// <summary>暂停前置模组发放纸条，直到下层区声望达到 untilRep。</summary>
        public static void PauseBaseNotesUntil(int untilRep)
        {
            ModHelper.SetModDataInt(BaseModId, KNotesPauseUntilRep, untilRep);
            Log("Paused base notes until rep " + untilRep + " (now " + Reputation + ")");
        }

        /// <summary>好感达到 60 后永久停止前置纸条；洛娜自己的卖药警告纸条不受影响。</summary>
        public static void StopBaseNotes()
        {
            if (BaseNotesStopped) return;
            SetBool(KBaseNotesStopped, true);
            PauseBaseNotesUntil(int.MaxValue);
            Log("Base notes stopped at affection " + AffectionInt);
        }

        /// <summary>只暂停今天一天（洛娜揭面那天，压掉门口的纸条+礼物）。</summary>
        public static void PauseBaseNotesToday()
        {
            ModHelper.SetModDataInt(BaseModId, KNotesPauseDay, Today);
        }

        /// <summary>声望已经够了就清掉暂停键（前置模组自己也会判断，这里只是保持干净）。</summary>
        public static void ClearPauseIfReached()
        {
            int until = ModHelper.GetModDataInt(BaseModId, KNotesPauseUntilRep, int.MinValue);
            if (until != int.MinValue && Reputation >= until)
            {
                ModHelper.RemoveModData(BaseModId, KNotesPauseUntilRep);
                Log("Base notes resumed at rep " + Reputation);
            }
        }

        // ---------------------------------------------------------------
        // 生命周期
        // ---------------------------------------------------------------
        /// <summary>读档/开新档后调用：必要时补激活（玩家已经超过阈值）。</summary>
        public static void OnRunLoaded()
        {
            if (!Ready) return;
            CheckActivation();
            if (Activated && AffectionInt >= 60) StopBaseNotes();

            // 老存档迁移：已经见过面但没有记录见面日，就从今天算起。
            if (Activated && FirstSeen && FirstMetDay < 0)
            {
                SetInt(KFirstMetDay, Today);
            }

            // 老存档没有上层区声望基准：记为当前值，避免读档瞬间补播档位剧情。
            if (GetInt(KLastULRep, int.MinValue) == int.MinValue)
            {
                SetInt(KLastULRep, StoreReputation.GetULReputation());
            }
        }

        /// <summary>检查是否满足激活条件；一旦激活就永久保留。</summary>
        public static void CheckActivation()
        {
            if (!Ready || Activated) return;
            if (!StartingPerk.IsSecretAdmirer()) return;

            int rep = Reputation;
            int need = UnlockRepValue;
            if (rep < need) return;

            SetBool(KActivated, true);
            SetInt(KActivationRep, rep);
            Affection = Math.Floor(Math.Max(0, rep) / 2.0);
            SetInt(KFirstAppearDay, Today + 1);

            if (!BaseNotesStopped && AffectionInt >= 60) StopBaseNotes();

            Log($"Activated: rep={rep} need={need} affection={Affection:0.##} firstAppearDay={Today + 1}");
        }

        /// <summary>下层区声望变化后调用（before 为变化前的值）。</summary>
        public static void OnLowerRepChanged(double before)
        {
            if (!Ready) return;

            StoreReputation faction = StoreReputation.GetStoreReputation("FACTION_LOWER_LEVEL");
            double after = faction != null ? faction.GetReputationExact() : Reputation;

            if (Activated && after > before)
            {
                AddAffection((after - before) * 0.5);
            }
            CheckActivation();
        }

        public static void AddAffection(double delta)
        {
            if (delta == 0) return;
            double value = Affection + delta;
            if (value > 100) value = 100;
            if (value < 0) value = 0;
            Affection = value;
            if (value >= 60) StopBaseNotes();
            CheckSpecialThresholds();
        }

        private static void CheckSpecialThresholds()
        {
            if (!Activated) return;

            // 注意：「好感跌破 70」这一段特殊剧情已取消，不在这里处理。
            if (!GetBool(KAbove80) && AffectionInt > 80)
            {
                SetBool(KAbove80, true);
                PendingSpecial = LonaStory.Above80.ToString();
            }
            if (!GetBool(KAbove90) && AffectionInt > 90)
            {
                SetBool(KAbove90, true);
                PendingSpecial = LonaStory.Above90.ToString();
            }
            if (!GetBool(KReach100) && AffectionInt >= 100)
            {
                SetBool(KReach100, true);
                PendingSpecial = LonaStory.Reach100.ToString();
            }
        }

        public static void MarkVisited()
        {
            SetInt(KLastVisitDay, Today);
        }

        public static void Leave(string reason)
        {
            SetBool(KLeft, true);

            // 永远离开之后：不再有纸条、不再有剧情排队、送礼状态作废、电话联系人删除。
            ModHelper.RemoveModData(ModId, KPendingNoteDay);
            ModHelper.RemoveModData(ModId, KPendingNoteIndex);
            PendingSpecial = "";
            ClearDelivery();
            ClearTierStory();
            Phone.RemoveContact();
            Gift.Reset();

            Log($"Left for good: {reason}");
        }

        // ---------------------------------------------------------------
        // 卖药警告
        // ---------------------------------------------------------------
        /// <summary>玩家卖出芬太片/梦尘时调用。返回 true 表示这次计入了警告。</summary>
        public static bool RegisterDrugSale()
        {
            if (!Ready) return false;
            if (Today < 2) return false;               // 存档第 1 天不生效
            if (GetInt(KLastDrugDay, -1000) == Today) return false; // 一天多次只算一次

            SetInt(KLastDrugDay, Today);

            // 好感惩罚：只要已激活就生效
            if (Activated && Affection > 0)
            {
                AddAffection(-10);
            }

            // 纸条与「离开」只在下层区声望 > 60 时计数
            if (Reputation <= 60) return true;

            int count = DrugCount + 1;
            SetInt(KDrugCount, count);

            if (count >= 4)
            {
                Leave("sold narcotics 4 times");
                return true;
            }

            SetInt(KPendingNoteDay, Today + 1);
            SetInt(KPendingNoteIndex, count);
            Log($"Drug sale #{count} on day {Today}; note queued for day {Today + 1}");
            return true;
        }

        public static int PendingNoteIndex
        {
            get { return GetInt(KPendingNoteIndex, 0); }
        }

        public static void ClearPendingNote()
        {
            ModHelper.RemoveModData(ModId, KPendingNoteDay);
            ModHelper.RemoveModData(ModId, KPendingNoteIndex);
        }

        // ---------------------------------------------------------------
        // ModData 帮助方法
        // ---------------------------------------------------------------
        private static bool GetBool(string key) { return ModHelper.GetModDataBool(ModId, key); }
        private static void SetBool(string key, bool value) { ModHelper.SetModDataBool(ModId, key, value); }
        private static int GetInt(string key, int fallback) { return ModHelper.GetModDataInt(ModId, key, fallback); }
        private static void SetInt(string key, int value) { ModHelper.SetModDataInt(ModId, key, value); }

        private static double GetDouble(string key, double fallback)
        {
            string raw = ModHelper.GetModData(ModId, key);
            if (string.IsNullOrEmpty(raw)) return fallback;
            double value;
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private static void SetDouble(string key, double value)
        {
            ModHelper.SetModData(ModId, key, value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void Log(string message)
        {
            Debug.Log("[AdmirerLona] " + message);
        }

        /// <summary>调试用：把状态清空重来（不影响声望/存档其他内容）。</summary>
        public static void ResetAll()
        {
            string[] keys =
            {
                KActivated, KActivationRep, KAffection, KLeft, KFirstSeen, KFirstAppearDay,
                KPendingSpecial, KAbove80, KAbove90, KReach100,
                KDrugCount, KLastDrugDay, KPendingNoteDay, KPendingNoteIndex, KLastVisitDay,
                KFirstMetDay, KRevealed, KPostRevealDay,
                KSeenDailyLow, KSeenDailyHigh, KDailyPoolAll,
                KBaseNotesStopped,
                KPendingDeliveryDay, KPendingDeliveryCategory, KPendingDeliveryCount,
                KPendingDeliveryFree, KPendingDeliveryTier, KLastULRep,
                KUL70Story, KUL140Story, KPendingTierStory, KPendingTierDay
            };
            foreach (string key in keys) ModHelper.RemoveModData(ModId, key);
            ModHelper.RemoveModData(BaseModId, KNotesPauseUntilRep);
            ModHelper.RemoveModData(BaseModId, KNotesPauseDay);
            Phone.RemoveContact();
            GiftMode = false;
        }
    }
}
