using System.Collections.Generic;

namespace AdmirerLona
{
    /// <summary>一次到访要播的剧情类型。</summary>
    internal enum LonaStory
    {
        Auto = 0,
        ThiefForm,
        FirstMeet,
        Daily,
        Above80,
        Above90,
        Reach100,
        Upper70,
        Upper140,
        PostReveal,
        Gift,
        GiftRefused
    }

    /// <summary>对话里的一行：Player = true 时用玩家气泡（不显示名字）。</summary>
    internal struct LonaLine
    {
        public bool Player;
        public string Text;

        public LonaLine(bool player, string text)
        {
            Player = player;
            Text = text;
        }
    }

    /// <summary>
    /// 文案。日常/特殊/通用/送礼都在 content/lona_story_zh.md，
    /// 初次见面、揭面、纸条、电话在各自的 md 里；
    /// 运行 content/build_localization.py 生成 zh.csv，运行时只读 CSV。
    /// </summary>
    internal static class LonaText
    {
        public const string DisplayName = "洛娜";

        /// <summary>从本地化文件读一条；读不到用 fallback。</summary>
        public static string Get(string key, string fallback)
        {
            string value = ModHelper.GetLocalized(State.ModId, key);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        /// <summary>
        /// 读取一段多行剧情。md 里 [P] 开头的行是玩家气泡，显示时去掉 [P]。
        /// 文本里的字面 \n 和真实换行都按行拆分。
        /// </summary>
        public static LonaLine[] ReadStory(string key, string fallback = "")
        {
            string text = Get(key, fallback);
            if (string.IsNullOrEmpty(text)) return new LonaLine[0];

            text = text.Replace("\\n", "\n");
            string[] raw = text.Split('\n');
            List<LonaLine> lines = new List<LonaLine>(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                string line = raw[i].TrimEnd('\r');
                if (line.Length == 0) continue;
                if (line.StartsWith("[P]"))
                {
                    lines.Add(new LonaLine(true, line.Substring(3)));
                }
                else
                {
                    lines.Add(new LonaLine(false, line));
                }
            }
            return lines.ToArray();
        }

        public static string[] ReadStoryText(string key, string fallback = "")
        {
            LonaLine[] lines = ReadStory(key, fallback);
            string[] result = new string[lines.Length];
            for (int i = 0; i < lines.Length; i++) result[i] = lines[i].Text;
            return result;
        }

        // ---------------- 日常剧情 1–35 ----------------
        public static LonaLine[] DailyStory(int id)
        {
            if (id < 1 || id > 35 || id == 30) return new LonaLine[0];
            return ReadStory("lona_daily_" + id.ToString("00"));
        }

        public static bool DailyStoryTrades(int id)
        {
            return id == 1 || id == 12;
        }

        public static int DailyStoryBudget(int id)
        {
            if (id == 1) return 30;
            if (id == 12) return 150;
            return 0;
        }

        public static bool DailyStoryQueueHead(int id)
        {
            return id == 5 || id == 33;
        }

        public static string DailyStoryBuyAck()
        {
            return Get("lona_daily_buy_ack", "");
        }

        public static string DailyStoryClose(int id)
        {
            return Get("lona_daily_" + id.ToString("00") + "_close", "");
        }

        // ---------------- 特殊剧情 ----------------
        public static LonaLine[] StoryLines(LonaStory story)
        {
            switch (story)
            {
                case LonaStory.Above80: return ReadStory("lona_above80");
                case LonaStory.Above90: return ReadStory("lona_above90");
                case LonaStory.PostReveal: return ReadStory("lona_postreveal");
                case LonaStory.Upper70: return LonaOnly(Upper70Lines());
                case LonaStory.Upper140: return LonaOnly(Upper140Lines());
                default: return ReadStory("lona_story_fallback");
            }
        }

        private static LonaLine[] LonaOnly(string[] lines)
        {
            LonaLine[] result = new LonaLine[lines.Length];
            for (int i = 0; i < lines.Length; i++) result[i] = new LonaLine(false, lines[i]);
            return result;
        }

        public static LonaLine[] Reach100Intro()
        {
            return ReadStory("lona_100_intro");
        }

        public static LonaLine[] ConfessionAgree()
        {
            return ReadStory("lona_100_agree");
        }

        public static LonaLine[] ConfessionRefuse()
        {
            return ReadStory("lona_100_refuse");
        }

        public static string ConfessionChoiceLabel(string kind)
        {
            if (kind == "agree") return CleanChoice(Get("lona_100_choice_agree", "同意"));
            if (kind == "dots") return CleanChoice(Get("lona_100_choice_dots", "......"));
            return CleanChoice(Get("lona_100_choice_refuse", "拒绝"));
        }

        private static string CleanChoice(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = text.Replace("\\n", "\n").Trim();
            int lineBreak = text.IndexOf('\n');
            if (lineBreak >= 0) text = text.Substring(0, lineBreak).Trim();
            if (text.StartsWith("[P]")) text = text.Substring(3).Trim();
            return text;
        }

        // ---------------- 通用买卖 ----------------
        public static string GenericBuyIntro() { return Get("lona_generic_buy_intro", ""); }
        public static string GenericBuyClose() { return Get("lona_generic_buy_close", ""); }
        public static string GenericSellIntro() { return Get("lona_generic_sell_intro", ""); }
        public static string GenericSellClose() { return Get("lona_generic_sell_close", ""); }

        // ---------------- 送礼 ----------------
        public static string GiftSkip { get { return Get("lona_gift_skip", "不用了，直接交易。"); } }
        public static string GiftHint { get { return Get("lona_gift_hint", "点击后将礼物放上柜台以送礼"); } }
        public static string GiftRefusedTooExpensive { get { return Get("lona_gift_refused_expensive", "太贵了，我不能收。"); } }
        public static string GiftRefusedForbidden { get { return Get("lona_gift_refused_forbidden", "抱歉，我讨厌这种东西。"); } }

        public static string[] GiftThanks
        {
            get { return ReadStoryText("lona_gift_thanks"); }
        }

        // ---------------- 卖药警告纸条 ----------------
        private static readonly string[] NoteKeys = { "lona_note_1", "lona_note_2", "lona_note_3" };

        public static string DrugNote(int index)
        {
            if (index < 1) index = 1;
            if (index > NoteKeys.Length) index = NoteKeys.Length;
            return Get(NoteKeys[index - 1], "");
        }

        // ---------------- 初次见面 · 第一段 ----------------
        public static List<LonaLine> FirstPurchaseLines()
        {
            return new List<LonaLine>
            {
                new LonaLine(false, Get("lona_first1_01", "......")),
                new LonaLine(true,  Get("lona_first1_02", "......")),
                new LonaLine(false, Get("lona_first1_03", "......")),
                new LonaLine(true,  Get("lona_first1_04", "要买点什么？")),
                new LonaLine(false, Get("lona_first1_05", "有食物吗？"))
            };
        }

        public static string FirstPurchaseFarewell()
        {
            return Get("lona_first1_06", "下次见");
        }

        public static string FirstPurchaseChatter()
        {
            return Get("lona_first1_07", "……");
        }

        // ---------------- 初次见面 · 第二段（揭面） ----------------
        public static List<LonaLine> RevealLines()
        {
            return new List<LonaLine>
            {
                new LonaLine(false, Get("lona_first2_01", "我又来了")),
                new LonaLine(false, Get("lona_first2_02", "抱歉，我不太擅长，也有点恐惧与人交流")),
                new LonaLine(false, Get("lona_first2_03", "写了那么多纸条，直到今天，才鼓起勇气走了进来")),
                new LonaLine(false, Get("lona_first2_04", "我叫洛娜，电话是203169")),
                new LonaLine(false, Get("lona_first2_05", "我可以根据你的需要带来一些物品，不过不能确保具体是什么")),
                new LonaLine(false, Get("lona_first2_06", "总之，今后我会常来，再见。"))
            };
        }

        // ---------------- 电话与预约送货 ----------------
        public static string PhoneHello() { return Get("lona_phone_hello", "我是洛娜"); }
        public static string PhoneWhat() { return Get("lona_phone_what", "有什么事吗？"); }
        public static string PhoneDeliver() { return Get("lona_phone_deliver", "委托洛娜带一些东西。"); }
        public static string PhoneStatus() { return Get("lona_phone_status", "问问近况。"); }
        public static string PhoneStatusLine() { return Get("lona_phone_status_line", "最近……还算不错。"); }

        public static string PhoneAffection(double value)
        {
            return string.Format(Get("lona_phone_affection", "好感度：{0} / 100"), value.ToString("0.#"));
        }

        public static string PhoneBusyFirst() { return Get("lona_phone_busy_1", "抱歉，最近不能给你带东西"); }

        public static string PhoneBusyDays(int days)
        {
            return string.Format(Get("lona_phone_busy_2", "再等{0}天就可以了"), days);
        }

        public static string PhoneBusyOk() { return Get("lona_phone_busy_ok", "我明白了，多谢"); }
        public static string PhoneAsk() { return Get("lona_phone_ask", "需要什么？"); }
        public static string PhoneConfirm() { return Get("lona_phone_confirm", "明天就到。"); }
        public static string PhoneCancel() { return Get("lona_phone_cancel", "那就这样。"); }
        public static string PhoneMore() { return Get("lona_phone_more", "下一页…"); }
        public static string PhoneBack() { return Get("lona_phone_back", "返回"); }
        public static string PhoneSpecial() { return Get("lona_phone_special", "特殊物品…"); }
        public static string CategoryName(string key, string fallback) { return Get(key, fallback); }
        public static string DeliveryGreeting() { return Get("lona_delivery_hello", "东西带来了，你看看。"); }
        public static string DeliveryFreeLine() { return Get("lona_delivery_free", "这些你拿着，不用给钱。"); }

        public static string[] Upper70Lines()
        {
            return new[]
            {
                Get("lona_ul70_01", "我简直不敢相信"),
                Get("lona_ul70_02", "我升职了"),
                Get("lona_ul70_03", "但我不知道是为什么"),
                Get("lona_ul70_04", "不过以后我就可以给你更好的东西"),
                Get("lona_ul70_05", "我要先走了，再见。")
            };
        }

        public static string[] Upper140Lines()
        {
            return new[]
            {
                Get("lona_ul140_01", "我居然成为货运站的负责人"),
                Get("lona_ul140_02", "我听他们说都是你的功劳"),
                Get("lona_ul140_03", "谢谢你"),
                Get("lona_ul140_04", "抱歉，我要先走了，要来不及了。")
            };
        }
    }
}
