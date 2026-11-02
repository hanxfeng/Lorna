using System;
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

        public static void Arm(StoreClient client)
        {
            if (client == null) return;
            Disarm();

            armed = client;
            previousAccept = client.willAcceptItem;
            client.willAcceptItem = delegate { return true; };

            State.GiftMode = true;
            ModEntry.Log("送礼状态开启（好感 " + State.AffectionInt + "）");
        }

        public static void Disarm()
        {
            if (armed != null && previousAccept != null)
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

        public static bool ShouldBlock(GameItem item)
        {
            if (!State.GiftMode || !IsGiftClient() || item == null) return false;

            if (IsNarcotic(item))
            {
                Say(LonaText.GiftRefusedNarcotic);
                return true;
            }
            if (State.AffectionInt < 80 && ValueOf(item) > 300)
            {
                Say(LonaText.GiftRefusedTooExpensive);
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
            Say(LonaText.GiftThanks);
            Disarm();
        }

        public static void Reset()
        {
            Disarm();
        }

        private static bool IsNarcotic(GameItem item)
        {
            if (!item.IsTag("NARCOTIC")) return false;
            return item.identifier != "pink_injector";
        }

        private static int ValueOf(GameItem item)
        {
            long value = item.unitValue;
            if (value > 0) return (int)value;
            return (int)item.GetNegociatedValue();
        }

        private static void Say(string text)
        {
            try
            {
                Dialogue dialogue = new Dialogue();
                dialogue.SetText(LonaText.DisplayName, text);
                if (DialogUIManager.Instance != null) DialogUIManager.Instance.StartDialogue(dialogue);
            }
            catch (Exception e)
            {
                ModEntry.Log("送礼对话失败: " + e.Message);
            }
        }
    }
}
