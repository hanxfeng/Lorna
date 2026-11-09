using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdmirerLona
{
    /// <summary>
    /// 卖药警告纸条，以及与前置模组（Enhanced_Secret_Admirers_And_More_News）纸条的互斥。
    /// 互斥实现：写前置模组的两个私有存档键 notes_pause_until_rep / notes_pause_day，
    /// 让它在暂停期间不发纸条（需要前置模组支持，见「前置模组修改.txt」）。
    /// </summary>
    internal static class Notes
    {
        private static readonly string[] NarcoticItems =
        {
            "fanta_pill", "dream_dust"
        };

        public static bool IsNarcoticSale(GameItem item)
        {
            if (item == null) return false;

            // 钥匙卡物资箱（含开锁后的状态）：箱子里的梦尘不算玩家卖药。
            if (IsKeycardLockedContainer(item)) return false;

            if (IsNarcoticId(item.identifier)) return true;

            // 容器里的药也算；但遇到钥匙卡箱子时不再往下检查它的内容。
            try
            {
                if (item.contentWindow != null && item.contentWindow.child is GameInventory inventory)
                {
                    foreach (GameItem child in inventory.childItems)
                    {
                        if (IsNarcoticSale(child)) return true;
                    }
                }
            }
            catch (Exception e)
            {
                ModEntry.Log("检查容器内药品失败: " + e.Message);
            }
            return false;
        }

        private static bool IsKeycardLockedContainer(GameItem item)
        {
            if (item == null || !item.IsTag("LOCKED_CONTAINER_TAG")) return false;
            try
            {
                string lockId = item.GetTagReadonly("LOCK_ID").GetString();
                return !string.IsNullOrEmpty(lockId)
                    && lockId.StartsWith("LOCK_ID_", StringComparison.Ordinal);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsNarcoticId(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return false;
            for (int i = 0; i < NarcoticItems.Length; i++)
            {
                if (identifier == NarcoticItems[i]) return true;
            }
            return false;
        }

        public static void OnItemSold(GameItem item)
        {
            if (!IsNarcoticSale(item)) return;
            State.RegisterDrugSale();
        }

        /// <summary>以物易物：只检查玩家交出去的物品列表，购买收到的物品不在这里。</summary>
        public static void OnItemsSold(List<GameItem> items)
        {
            if (items == null) return;
            for (int i = 0; i < items.Count; i++)
            {
                if (IsNarcoticSale(items[i]))
                {
                    State.RegisterDrugSale();
                    return;
                }
            }
        }

        /// <summary>「一键清空柜台」不走 SellItem，成交前先看一眼柜台里有没有药。</summary>
        public static bool ContainsNarcoticInBackInventory()
        {
            try
            {
                if (EmporiumEntry.Instance == null || EmporiumEntry.Instance.backInvinvElement == null) return false;
                foreach (GameItem item in EmporiumEntry.Instance.backInvinvElement.childItems)
                {
                    if (IsNarcoticSale(item)) return true;
                }
            }
            catch (Exception e)
            {
                ModEntry.Log("清空柜台检查失败: " + e.Message);
            }
            return false;
        }

        /// <summary>调试用：直接把第 index 张警告纸条放到柜台上（不计数、不触发暂停）。</summary>
        public static string DebugDeliver(int index)
        {
            if (!State.Ready) return "还没有载入存档";
            try
            {
                GameItem note = PreBuiltItemHelper.CreateHandnote(LonaText.DrugNote(index), "", "");
                if (note == null) return "生成失败";
                PlayerStore.Instance.AddDirectSellingItemToTable(note, true);
                return "警告纸条 #" + index + " 已放到柜台";
            }
            catch (Exception e)
            {
                return "生成失败: " + e.Message;
            }
        }

        // ---------------------------------------------------------------
        // 纸条投放：每天开门、第一条顾客之前
        // ---------------------------------------------------------------
        public static void DeliverPending()
        {
            if (!State.Ready || State.Left) return;
            // 取消「麻醉品买卖」后不再发放警告纸条；已有待投放的也一并取消。
            if (!State.NarcoticTradeValue)
            {
                State.ClearPendingNote();
                return;
            }
            // 跳过一天也补发：下一次开门时只要日期已经到/超过待投放日就投放。
            if (State.PendingNoteDay < 0 || State.Today < State.PendingNoteDay) return;

            int index = State.PendingNoteIndex;
            string body = LonaText.DrugNote(index);

            try
            {
                GameItem note = PreBuiltItemHelper.CreateHandnote(body, "", "");
                bool placed = false;
                try
                {
                    PlayerStore.Instance.AddDirectSellingItemToTable(note, true);
                    placed = note.parentInventory != null;
                }
                catch (Exception)
                {
                    placed = false;
                }
                if (!placed)
                {
                    ModHelper.MoveItemToInventory(note);
                }
            }
            catch (Exception e)
            {
                ModEntry.Log("警告纸条生成失败: " + e.Message);
            }

            // 警告纸条出现后：暂停前置模组的纸条，直到下层区声望 +10。
            // 好感 60 后前置纸条已经永久停止，不能再被这里的短暂停覆盖。
            if (!State.BaseNotesStopped)
            {
                State.PauseBaseNotesUntil(State.Reputation + 10);
            }

            ModEntry.Log("警告纸条 #" + index + " 已投放（第 " + State.Today + " 天）");
            State.ClearPendingNote();
        }
    }

    /// <summary>
    /// 前置检查：优先读取前置 DLL 检查私有存档键；
    /// 新版的 ModHelper 文件 API 只保证读取自身模组文件夹，
    /// 读不到时降级为只检查前置已加载，不阻止模组运行。
    /// </summary>
    internal static class BaseModSchema
    {
        private const int StUnknown = 0;
        private const int StOk = 1;
        private const int StBroken = 2;
        private const int StMissing = 3;

        private static int state = StUnknown;
        private static string reason;

        public static string Reason { get { return reason; } }

        public static bool Ok
        {
            get
            {
                if (state == StUnknown) Check();
                return state == StOk;
            }
        }

        public static void Check()
        {
            state = StBroken;
            reason = null;

            LoadedMod mod = ModLoader.Mods.Find(m => m.Manifest != null && m.Manifest.ID == State.BaseModId);
            if (mod == null || !mod.IsEnabled || mod.LoadError != null)
            {
                state = StMissing;
                reason = "前置模组 Enhanced_Secret_Admirers_And_More_News 未安装、未启用或加载失败";
                Debug.LogError("[AdmirerLona] " + reason + "，洛娜功能已停用。");
                return;
            }

            try
            {
                byte[] bytes = ModHelper.ReadModFileBytes(State.BaseModId, "PawnshopMod.dll");
                if (bytes != null && bytes.Length > 0)
                {
                    if (!ContainsUtf16(bytes, "npc_seen_") || !ContainsUtf16(bytes, "npc_done_"))
                    {
                        reason = "前置模组的私有存档键名已变更（找不到 npc_seen_ / npc_done_）";
                        Debug.LogError("[AdmirerLona] " + reason + "。洛娜功能已停用（不弹窗，按 F8 查看）。");
                        return;
                    }

                    state = StOk;
                    return;
                }

                ModEntry.Log("前置 DLL 字节检查不可用（新版文件 API 只保证读取自身模组文件夹），改为只检查前置加载状态。");
            }
            catch (Exception e)
            {
                ModEntry.Log("前置 DLL 字节检查失败，改为只检查前置加载状态: " + e.Message);
            }

            state = StOk;
        }

        public static void Reset()
        {
            state = StUnknown;
            reason = null;
        }

        private static bool ContainsUtf16(byte[] haystack, string needle)
        {
            byte[] pattern = new byte[needle.Length * 2];
            for (int i = 0; i < needle.Length; i++)
            {
                pattern[i * 2] = (byte)needle[i];
                pattern[i * 2 + 1] = 0;
            }
            for (int i = 0; i <= haystack.Length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (haystack[i + j] != pattern[j]) { match = false; break; }
                }
                if (match) return true;
            }
            return false;
        }
    }
}
