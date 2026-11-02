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

        /// <summary>立绘缩放：1 = 与画布同尺寸，越小人物越矮。</summary>
        public static ConfigEntry<float> PortraitScale;

        public static float PortraitScaleValue
        {
            get { return PortraitScale != null ? PortraitScale.Value : 0.75f; }
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
            UnlockRep = config.Define(
                "unlock_rep", 150, "Lorna",
                () => "首次出现声望",
                min: 0, max: 200,
                tooltip: () => "下层区声望达到该值的第二天，洛娜第一次出现。");

            PortraitScale = config.Define(
                "portrait_scale", 0.75f, "Lorna",
                () => "立绘缩放",
                min: 0.4f, max: 1.2f,
                tooltip: () => "1 = 和画布同尺寸；越小人物越矮。改完下次开门生效。");
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

        public static string PendingSpecial
        {
            get { return ModHelper.GetModData(ModId, KPendingSpecial) ?? ""; }
            set
            {
                if (string.IsNullOrEmpty(value)) ModHelper.RemoveModData(ModId, KPendingSpecial);
                else ModHelper.SetModData(ModId, KPendingSpecial, value);
            }
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

        public static void MarkFirstMet()
        {
            SetBool(KFirstSeen, true);
            SetInt(KFirstMetDay, Today);
            Log("First met on day " + Today);
        }

        public static void MarkRevealed()
        {
            SetBool(KRevealed, true);
            Log("Revealed on day " + Today);
        }

        // ---------------- 前置模组的纸条暂停 ----------------

        /// <summary>暂停前置模组发放纸条，直到下层区声望达到 untilRep。</summary>
        public static void PauseBaseNotesUntil(int untilRep)
        {
            ModHelper.SetModDataInt(BaseModId, KNotesPauseUntilRep, untilRep);
            Log("Paused base notes until rep " + untilRep + " (now " + Reputation + ")");
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

            // 老存档迁移：已经见过面但没有记录见面日，就从今天算起。
            if (Activated && FirstSeen && FirstMetDay < 0)
            {
                SetInt(KFirstMetDay, Today);
            }
        }

        /// <summary>检查是否满足激活条件；一旦激活就永久保留。</summary>
        public static void CheckActivation()
        {
            if (!Ready || Activated) return;
            if (!StartingPerk.IsSecretAdmirer()) return;

            int rep = Reputation;
            int need = UnlockRep != null ? UnlockRep.Value : 150;
            if (rep < need) return;

            SetBool(KActivated, true);
            SetInt(KActivationRep, rep);
            Affection = Math.Floor(Math.Max(0, (rep - 10) / 2.0));
            SetInt(KFirstAppearDay, Today + 1);

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
            Affection = value;
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

            // 永远离开之后：不再有纸条、不再有剧情排队、送礼状态作废。
            ModHelper.RemoveModData(ModId, KPendingNoteDay);
            ModHelper.RemoveModData(ModId, KPendingNoteIndex);
            PendingSpecial = "";
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
                KFirstMetDay, KRevealed
            };
            foreach (string key in keys) ModHelper.RemoveModData(ModId, key);
            ModHelper.RemoveModData(BaseModId, KNotesPauseUntilRep);
            ModHelper.RemoveModData(BaseModId, KNotesPauseDay);
            GiftMode = false;
        }
    }
}
