using System;

namespace AdmirerLona
{
    /// <summary>
    /// F9 调试面板里的入口：Special 分类下「Lorna 测试面板」，
    /// 点开是一个独立的窗口，里面按剧情分好按钮。
    /// </summary>
    internal static class LonaDebug
    {
        private const string WindowId = "admirer_lona.debug";
        private const string StoryWindowId = "admirer_lona.story_debug";

        public static void Register()
        {
            ModHelper.AddDebugFunction(State.ModId, ModHelper.DEBUG_CATEGORY_SPECIAL, "Lorna 测试面板", Open);
        }

        private static void Open()
        {
            if (CustomUIManager.Instance == null)
            {
                ModEntry.Log("自定义 UI 还没准备好");
                return;
            }

            CustomUIBuilder builder = CustomUIManager.Window(WindowId, "洛娜测试", CustomUIManager.LAYER_OVERLAY);
            builder.SetSize(580f, 760f).SetDraggable(true).SetCloseOnEscape(true);

            builder.AddHeader("出现（下一位顾客，开门时生效）");
            builder.BeginGrid(2, 270f, 40f);
            builder.AddButton("洛娜 · 来买", delegate { Force(LonaVisit.Buy, LonaStory.Daily); });
            builder.AddButton("洛娜 · 来卖", delegate { Force(LonaVisit.Sell, LonaStory.Daily); });
            builder.AddButton("小偷形态（激活前）", ForceThief);
            builder.AddButton("初见面·第一段（买食物）", delegate { Force(LonaVisit.FirstPurchase, LonaStory.FirstMeet); });
            builder.AddButton("初见面·第二段（揭面）", delegate { Force(LonaVisit.Reveal, LonaStory.FirstMeet); });
            builder.End();

            builder.AddHeader("剧情测试");
            builder.BeginRow(6f);
            builder.AddButton("剧情列表（全部）", OpenStoryList);
            builder.End();

            builder.AddHeader("特殊剧情");
            builder.BeginGrid(2, 270f, 40f);
            builder.AddButton("好感 > 80", delegate { Force(LonaVisit.Story, LonaStory.Above80); });
            builder.AddButton("好感 > 90", delegate { Force(LonaVisit.Story, LonaStory.Above90); });
            builder.AddButton("好感 100", delegate { Force(LonaVisit.Story, LonaStory.Reach100); });
            builder.End();

            builder.AddHeader("警告纸条");
            builder.BeginGrid(3, 180f, 40f);
            for (int i = 1; i <= 3; i++)
            {
                int index = i;
                builder.AddButton("警告纸条 #" + i, delegate { ModEntry.Log(Notes.DebugDeliver(index)); });
            }
            builder.End();

            builder.AddHeader("立绘参考");
            builder.AddLabel(Portrait.DescribeReference());

            builder.AddHeader("电话与送货");
            builder.BeginRow(6f);
            builder.AddButton("电话/送货测试面板", OpenPhoneTest);
            builder.End();

            builder.AddHeader("状态");
            builder.BeginRow(6f);
            builder.AddButton("打印状态到 F8", PrintState);
            builder.AddButton("打印立绘尺寸", Portrait.LogSpriteInfo);
            builder.AddButton("强制重注入立绘", Portrait.ForceReinject);
            builder.AddButton("重置洛娜状态", ResetState);
            builder.AddButton("关闭", delegate { CustomUIManager.Instance.CloseWindow(WindowId); });
            builder.End();

            builder.Show();
        }

        private static void Force(LonaVisit visit, LonaStory story, int dailyStory = 0)
        {
            DebugForce.Pending = true;
            DebugForce.Thief = false;
            DebugForce.Visit = visit;
            DebugForce.Story = story;
            DebugForce.DailyStory = dailyStory;
            ModEntry.Log("已排队：下一位顾客是洛娜（" + story + (dailyStory > 0 ? " / 日常 " + dailyStory : "")
                + " / " + visit + "），开门时生效");
        }

        private static void ForceDaily(int storyId)
        {
            LonaVisit visit = LonaText.DailyStoryTrades(storyId) ? LonaVisit.StoryBuy : LonaVisit.Story;
            Force(visit, LonaStory.Daily, storyId);
        }

        private static void OpenStoryList()
        {
            if (CustomUIManager.Instance == null)
            {
                ModEntry.Log("自定义 UI 还没准备好");
                return;
            }

            CustomUIBuilder builder = CustomUIManager.Window(StoryWindowId, "洛娜剧情列表", CustomUIManager.LAYER_OVERLAY);
            builder.SetSize(900f, 860f).SetDraggable(true).SetCloseOnEscape(true);

            builder.AddHeader("日常剧情 1–35（下一位顾客，开门时生效）");
            builder.BeginGrid(5, 168f, 38f);
            for (int id = 1; id <= 35; id++)
            {
                if (id == 30) continue;
                int captured = id;
                builder.AddButton("日常 " + id.ToString("00"), delegate { ForceDaily(captured); });
            }
            builder.End();

            builder.AddHeader("特殊剧情");
            builder.BeginGrid(3, 280f, 38f);
            builder.AddButton("初见面·第一段", delegate { Force(LonaVisit.FirstPurchase, LonaStory.FirstMeet); });
            builder.AddButton("初见面·第二段（揭面）", delegate { Force(LonaVisit.Reveal, LonaStory.FirstMeet); });
            builder.AddButton("揭面后介绍（队首）", delegate { Force(LonaVisit.Story, LonaStory.PostReveal); });
            builder.AddButton("好感 >80", delegate { Force(LonaVisit.Story, LonaStory.Above80); });
            builder.AddButton("好感 >90", delegate { Force(LonaVisit.Story, LonaStory.Above90); });
            builder.AddButton("好感 100 告白", delegate { Force(LonaVisit.Story, LonaStory.Reach100); });
            builder.AddButton("上层区 70", delegate { Force(LonaVisit.Story, LonaStory.Upper70); });
            builder.AddButton("上层区 140", delegate { Force(LonaVisit.Story, LonaStory.Upper140); });
            builder.AddButton("关闭", delegate { CustomUIManager.Instance.CloseWindow(StoryWindowId); });
            builder.End();

            builder.Show();
        }

        private static void ForceThief()
        {
            DebugForce.Pending = true;
            DebugForce.Thief = true;
            ModEntry.Log("已排队：下一位顾客是小偷形态，开门时生效");
        }

        private static void PrintState()
        {
            ModEntry.Log(string.Format(
                "state: activated={0} rep={1} need={2} affection={3:0.##} tier={4} left={5} firstSeen={6} " +
                "firstAppearDay={7} drugCount={8} pendingNoteDay={9} pendingSpecial={10} schema={11}",
                State.Activated, State.Reputation, State.UnlockRepValue,
                State.Affection, State.DailyTier, State.Left, State.FirstSeen,
                State.FirstAppearDay, State.DrugCount, State.PendingNoteDay,
                State.PendingSpecial, BaseModSchema.Ok ? "ok" : BaseModSchema.Reason));
        }

        private static void ResetState()
        {
            State.ResetAll();
            DebugForce.Clear();
            ModEntry.Log("洛娜状态已重置（声望/天赋不变）");
        }

        private const string PhoneWindowId = "admirer_lona.phone_debug";

        private static void OpenPhoneTest()
        {
            if (CustomUIManager.Instance == null)
            {
                ModEntry.Log("自定义 UI 还没准备好");
                return;
            }

            CustomUIBuilder builder = CustomUIManager.Window(PhoneWindowId, "洛娜电话/送货", CustomUIManager.LAYER_OVERLAY);
            builder.SetSize(620f, 720f).SetDraggable(true).SetCloseOnEscape(true);

            builder.AddHeader("电话（203169）");
            builder.BeginGrid(3, 190f, 40f);
            builder.AddButton("拨打电话", Phone.DebugCall);
            builder.AddButton("清除冷却", Phone.DebugClearCooldown);
            builder.AddButton("补联系人", Phone.DebugAddContact);
            builder.AddButton("给洛娜名片", Phone.DebugGiveCard);
            builder.AddButton("移除联系人", Phone.RemoveContact);
            builder.AddButton("打印电话状态", Phone.DebugLogState);
            builder.End();

            builder.AddHeader("基础送货（下次开门，队尾）");
            builder.BeginGrid(3, 190f, 40f);
            foreach (LonaDeliveryCategory category in Delivery.BaseCategories())
            {
                LonaDeliveryCategory captured = category;
                builder.AddButton(captured.Name, delegate { Phone.DebugQueue(captured.Id); });
            }
            builder.End();

            builder.AddHeader("特殊送货（下次开门，队尾）");
            builder.BeginGrid(3, 190f, 40f);
            foreach (LonaDeliveryCategory category in Delivery.All)
            {
                if (!category.Special) continue;
                LonaDeliveryCategory captured = category;
                string label = captured.Name + (captured.Unlocked ? "" : "（未解锁）");
                builder.AddButton(label, delegate { Phone.DebugQueue(captured.Id); });
            }
            builder.End();

            builder.AddHeader("档位剧情（下次开门，队首）");
            builder.BeginGrid(3, 190f, 40f);
            builder.AddButton("上层区 70", delegate { Phone.DebugTierStory(LonaStory.Upper70); });
            builder.AddButton("上层区 140", delegate { Phone.DebugTierStory(LonaStory.Upper140); });
            builder.AddButton("关闭", delegate { CustomUIManager.Instance.CloseWindow(PhoneWindowId); });
            builder.End();

            builder.Show();
        }
    }
}
