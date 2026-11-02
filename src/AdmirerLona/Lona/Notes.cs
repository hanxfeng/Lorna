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
            if (item == null || item.identifier == null) return false;
            for (int i = 0; i < NarcoticItems.Length; i++)
            {
                if (item.identifier == NarcoticItems[i]) return true;
            }
            return false;
        }

        public static void OnItemSold(GameItem item)
        {
            if (!IsNarcoticSale(item)) return;
            State.RegisterDrugSale();
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

        // ---------------------------------------------------------------
        // 纸条投放：每天开门、第一条顾客之前
        // ---------------------------------------------------------------
        public static void DeliverPending()
        {
            if (!State.Ready || State.Left) return;
            if (State.PendingNoteDay < 0 || State.PendingNoteDay != State.Today) return;

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
            State.PauseBaseNotesUntil(State.Reputation + 10);

            ModEntry.Log("警告纸条 #" + index + " 已投放（第 " + State.Today + " 天）");
            State.ClearPendingNote();
        }
    }

    /// <summary>
    /// 检测前置模组的私有存档键是否还在。
    /// 做法：读它的 DLL，在字符串堆里找 UTF-16 的 "npc_seen_" / "npc_done_"。
    /// 找不到就把洛娜整体停用，并在 F8 控制台报错（不给普通玩家弹窗）。
    /// </summary>
    internal static class BaseModSchema
    {
        private const int StUnknown = 0;
        private const int StOk = 1;
        private const int StBroken = 2;
        private const int StMissing = 3;

        private static int state = StUnknown;
        private static string reason;

        public static bool Checked { get { return state != StUnknown; } }
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
            if (mod == null)
            {
                state = StMissing;
                reason = "前置模组 Enhanced_Secret_Admirers_And_More_News 未安装或未启用";
                Debug.LogError("[AdmirerLona] " + reason + "，洛娜功能已停用。");
                return;
            }

            try
            {
                byte[] bytes = ModHelper.ReadModFileBytes(State.BaseModId, "PawnshopMod.dll");
                if (bytes == null || bytes.Length == 0)
                {
                    reason = "读不到前置模组的 PawnshopMod.dll";
                }
                else if (!ContainsUtf16(bytes, "npc_seen_") || !ContainsUtf16(bytes, "npc_done_"))
                {
                    reason = "前置模组的私有存档键名已变更（找不到 npc_seen_ / npc_done_）";
                }
                else
                {
                    state = StOk;
                    return;
                }
            }
            catch (Exception e)
            {
                reason = "检查前置模组失败: " + e.Message;
            }

            state = StBroken;
            Debug.LogError("[AdmirerLona] " + reason + "。洛娜功能已停用（不弹窗，按 F8 查看）。");
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
