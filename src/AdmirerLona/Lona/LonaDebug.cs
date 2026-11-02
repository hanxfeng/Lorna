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
            builder.SetSize(580f, 640f).SetDraggable(true).SetCloseOnEscape(true);

            builder.AddHeader("出现（下一位顾客，开门时生效）");
            builder.BeginGrid(2, 270f, 40f);
            builder.AddButton("洛娜 · 来买", delegate { Force(LonaVisit.Buy, LonaStory.Daily, -1); });
            builder.AddButton("洛娜 · 来卖", delegate { Force(LonaVisit.Sell, LonaStory.Daily, -1); });
            builder.AddButton("小偷形态（150 前）", ForceThief);
            builder.AddButton("初见面·第一段（买食物）", delegate { Force(LonaVisit.FirstPurchase, LonaStory.FirstMeet, -1); });
            builder.AddButton("初见面·第二段（揭面）", delegate { Force(LonaVisit.Reveal, LonaStory.FirstMeet, -1); });
            builder.End();

            builder.AddHeader("日常剧情");
            builder.BeginGrid(4, 130f, 40f);
            for (int i = 0; i < 4; i++)
            {
                int tier = i;
                builder.AddButton("档 " + (i + 1) + "（" + TierLabel(i) + "）",
                    delegate { Force(LonaVisit.Buy, LonaStory.Daily, tier); });
            }
            builder.End();

            builder.AddHeader("特殊剧情");
            builder.BeginGrid(2, 270f, 40f);
            builder.AddButton("好感 > 80", delegate { Force(LonaVisit.Story, LonaStory.Above80, -1); });
            builder.AddButton("好感 > 90", delegate { Force(LonaVisit.Story, LonaStory.Above90, -1); });
            builder.AddButton("好感 100", delegate { Force(LonaVisit.Story, LonaStory.Reach100, -1); });
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

            builder.AddHeader("状态");
            builder.BeginRow(6f);
            builder.AddButton("打印状态到 F8", PrintState);
            builder.AddButton("打印立绘尺寸", Portrait.LogSpriteInfo);
            builder.AddButton("重置洛娜状态", ResetState);
            builder.AddButton("关闭", delegate { CustomUIManager.Instance.CloseWindow(WindowId); });
            builder.End();

            builder.Show();
        }

        private static string TierLabel(int tier)
        {
            switch (tier)
            {
                case 0: return "70-79";
                case 1: return "80-89";
                case 2: return "90-99";
                default: return "100";
            }
        }

        private static void Force(LonaVisit visit, LonaStory story, int tier)
        {
            DebugForce.Pending = true;
            DebugForce.Thief = false;
            DebugForce.Visit = visit;
            DebugForce.Story = story;
            DebugForce.DailyTier = tier;
            ModEntry.Log("已排队：下一位顾客是洛娜（" + story + " / " + visit + (tier >= 0 ? " / 档 " + tier : "") + "），开门时生效");
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
    }
}
