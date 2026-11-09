using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace AdmirerLona
{
    /// <summary>
    /// 送礼：复用谈判桌。洛娜「来买」的这次访问里，玩家点选「（把东西放到柜台上）」后
    /// 进入送礼状态——她什么都会收下；玩家放上柜台成交的第一件物品算礼物（不收钱、加好感），
    /// 之后恢复正常买卖。
    /// </summary>
    internal static class Gift
    {
        private static StoreClient armed;
        private static Func<GameItem, bool> previousAccept;
        private static int cashBefore;

        private static string[] pendingLines;
        private static bool messageScheduled;

        // 必须保存 StoreClient 的原始 preference 字段，不能保存 willAcceptItem 的 getter：
        // getter 返回的是一个会再次读取 willAcceptItemPreference 的包装委托，
        // 把它写回 preference 会让委托调用自己，Disarm 之后下一次询问直接无限递归。
        private static readonly FieldInfo AcceptPreferenceField =
            AccessTools.Field(typeof(StoreClient), "willAcceptItemPreference");

        public static void Arm(StoreClient client)
        {
            if (client == null) return;
            Disarm();

            armed = client;
            previousAccept = AcceptPreferenceField != null
                ? AcceptPreferenceField.GetValue(client) as Func<GameItem, bool>
                : null;
            // 不能无条件 true：游戏在部分路径会拿 null 来问，返回 true 会把 null 带进交易逻辑。
            client.willAcceptItem = delegate(GameItem item) { return item != null; };

            State.GiftMode = true;
            ModEntry.Log("送礼状态开启（好感 " + State.AffectionInt + "）");
        }

        public static void Disarm()
        {
            if (armed != null && AcceptPreferenceField != null)
            {
                try { armed.willAcceptItem = previousAccept; }
                catch (Exception) { }
            }
            armed = null;
            previousAccept = null;
            State.GiftMode = false;
        }

        private static StoreClient CurrentClient()
        {
            if (PlayerStore.Instance == null || PlayerStore.Instance.currentClientInstance == null) return null;
            return PlayerStore.Instance.currentClientInstance.GetClientBlueprint();
        }

        private static bool IsGiftClient()
        {
            return armed != null && CurrentClient() == armed;
        }

        /// <summary>当前是否正处在给洛娜送礼的状态。</summary>
        public static bool IsArmedForCurrentClient()
        {
            return State.GiftMode && IsGiftClient();
        }

        public static bool ShouldBlock(GameItem item)
        {
            if (!State.GiftMode || !IsGiftClient() || item == null) return false;

            if (IsNarcotic(item))
            {
                QueueLines(LonaText.GiftRefusedForbidden);
                return true;
            }
            if (ContrabandHelper.IsContraband(item))
            {
                QueueLines(LonaText.GiftRefusedContraband);
                return true;
            }
            if (State.AffectionInt < 80 && ValueOf(item) > 300)
            {
                QueueLines(LonaText.GiftRefusedTooExpensive);
                return true;
            }
            return false;
        }

        public static void Capture(GameItem item)
        {
            if (!State.GiftMode || !IsGiftClient()) return;
            cashBefore = PlayerStore.Instance != null ? PlayerStore.Instance.playerCash : 0;
        }

        public static void OnSold(GameItem item)
        {
            if (!State.GiftMode || !IsGiftClient() || item == null) return;

            // 把这次成交的钱退回去，礼物就是礼物。
            int paid = (PlayerStore.Instance != null ? PlayerStore.Instance.playerCash : 0) - cashBefore;
            if (paid > 0)
            {
                PlayerStore.Instance.playerCash -= paid;
                StoreClient client = CurrentClient();
                if (client != null) client.ModBudget(paid);
            }

            int value = ValueOf(item);
            double gain = Math.Min(5.0, value / 100.0);
            State.AddAffection(gain);

            ModEntry.Log("收到礼物：" + item.identifier + " 价值 " + value + "，好感 +" + gain.ToString("0.##"));

            // 先退出送礼状态，再排队播感谢；不能在 SellItem 的调用栈里直接 StartDialogue。
            Disarm();
            QueueLines(LonaText.GiftThanks);
        }

        public static void Reset()
        {
            Disarm();
            messageScheduled = false;
            pendingLines = null;
        }

        private static bool IsNarcotic(GameItem item)
        {
            return item.IsTag("NARCOTIC");
        }

        private static int ValueOf(GameItem item)
        {
            long value = item.unitValue;
            if (value > 0) return (int)value;
            try
            {
                return (int)item.GetNegociatedValue();
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// 不在 SellItem 的 Prefix/Postfix 里直接开对话：SellMultiple 会在一帧内连续卖多件，
        /// 同步 StartDialogue 会嵌套执行当前对话的 endAction，导致递归/卡死。
        /// 这里只记下一条消息，等这一帧的卖货调用栈结束再显示。
        /// </summary>
        private static void QueueLines(params string[] lines)
        {
            pendingLines = lines;
            if (messageScheduled) return;
            if (DialogUIManager.Instance == null) return;

            messageScheduled = true;
            try
            {
                if (LonaCoroutineHost.Run(ShowMessageNextFrame()) == null)
                {
                    messageScheduled = false;
                }
            }
            catch (Exception e)
            {
                messageScheduled = false;
                ModEntry.Log("送礼对话排队失败: " + e.Message);
            }
        }

        private static IEnumerator ShowMessageNextFrame()
        {
            yield return null;

            // 等当前对话链播完再插话，避免在卖货/成交对话的 endAction 里嵌套开新对话。
            if (DialogUIManager.Instance != null)
            {
                for (int i = 0; i < 600 && DialogUIManager.Instance.currentDialog != null; i++)
                {
                    yield return null;
                }
            }

            messageScheduled = false;
            string[] lines = pendingLines;
            pendingLines = null;
            if (lines == null || lines.Length == 0 || DialogUIManager.Instance == null) yield break;

            try
            {
                Dialogue head = new Dialogue();
                Dialogue current = head;
                for (int i = 0; i < lines.Length; i++)
                {
                    current.SetText(LonaText.DisplayName, lines[i]);
                    if (i < lines.Length - 1) current = current.NextDialogue();
                }
                DialogUIManager.Instance.StartDialogue(head);
            }
            catch (Exception e)
            {
                ModEntry.Log("送礼对话失败: " + e.Message);
            }
        }
    }
}
