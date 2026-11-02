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
        Below70,
        Above80,
        Above90,
        Reach100,
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
    /// 文案。日常/特殊剧情仍是占位（还没写），警告纸条和初次见面走 Localization/zh.csv。
    /// 键在 content/*.md 里改，运行 content/build_localization.py 重新生成。
    /// </summary>
    internal static class LonaText
    {
        public const string DisplayName = "洛娜";

        // ---------------- 日常剧情：4 档（占位） ----------------
        // tier 0: 70-79 / 1: 80-89 / 2: 90-99 / 3: 100
        private static readonly string[][] DailyLines =
        {
            new[]
            {
                "【日常 70-79 占位】她又把帽子压低了点，像是还没想好要不要认你。",
                "【日常 70-79 占位】“今天货不多，别嫌弃。”",
                "【日常 70-79 占位】她提到下层区的雨，说这几个月下得比往年久。"
            },
            new[]
            {
                "【日常 80-89 占位】“你最近生意不错。”她说这话时没看你。",
                "【日常 80-89 占位】她把带来的东西往柜台上一放，动作比上次随意。",
                "【日常 80-89 占位】她记得你上次抱怨过的那件小事。"
            },
            new[]
            {
                "【日常 90-99 占位】“我给你留了点东西。”她顿了顿，“不是白给的。”",
                "【日常 90-99 占位】她开始会主动说起自己的事，虽然每次都只说一半。",
                "【日常 90-99 占位】她把伞靠在门边，说反正外面也不下了。"
            },
            new[]
            {
                "【日常 100 占位】“今天的账，我自己记。”",
                "【日常 100 占位】她不再把脸藏在阴影里。",
                "【日常 100 占位】“你要是哪天想关店，”她说，“记得先跟我说。”"
            }
        };

        // ---------------- 特殊剧情（占位） ----------------
        public static readonly string[] Below70 =
        {
            "【特殊：好感跌破 70】她把东西放下，没有像往常那样先开口。",
            "【特殊：好感跌破 70】“那些东西，”她说，“我闻得出来。”",
            "【特殊：好感跌破 70】她没等你解释，转身走了。"
        };

        public static readonly string[] Above80 =
        {
            "【特殊：好感初次 >80】她今天多带了一份东西，说是“顺手”。",
            "【特殊：好感初次 >80】“别多想，”她补了一句，“下层区的人都这样。”"
        };

        public static readonly string[] Above90 =
        {
            "【特殊：好感初次 >90】她把一直戴着的护腕取下来，放在柜台上给你看。",
            "【特殊：好感初次 >90】“这是我哥留下的，”她说，“现在归你保管。”"
        };

        public static readonly string[] Reach100 =
        {
            "【特殊：好感满 100】她来得很早，店里还没开灯。",
            "【特殊：好感满 100】“我本来不打算留这么久。”",
            "【特殊：好感满 100】“但我想了想，还是留下吧。”"
        };

        // 好感 < 70 时的简短招呼（不播日常剧情）
        public static readonly string[] ColdGreeting =
        {
            "【冷淡占位】她点了点头，没有多说。"
        };

        // ---------------- 卖药警告纸条（zh.csv） ----------------
        private static readonly string[] NoteKeys = { "lona_note_1", "lona_note_2", "lona_note_3" };

        private static readonly string[] NoteFallback =
        {
            "下层区已经够糟了，那些麻醉品会让它更糟，可以的话，不要再卖了好吗？",
            "我听说你又在卖那些麻醉品了，我见过许多原本幸福的家庭因它破碎，收手好吗？",
            "我不愿意与毒贩为伍，好自为之。"
        };

        // ---------------- 送礼（占位） ----------------
        public const string GiftOffer = "【送礼选项占位】（把东西放到柜台上）";
        public const string GiftHint = "【送礼提示占位】把想送的东西放上柜台吧。";
        public const string GiftThanks = "【收到礼物占位】“……谢谢。”";
        public const string GiftRefusedTooExpensive = "【拒收占位】太贵了，我不能收。";
        public const string GiftRefusedNarcotic = "【拒收占位】这个我不要。";

        /// <summary>从本地化文件读一条；读不到用 fallback。</summary>
        public static string Get(string key, string fallback)
        {
            string value = ModHelper.GetLocalized(State.ModId, key);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        public static string DrugNote(int index)
        {
            if (index < 1) index = 1;
            if (index > NoteKeys.Length) index = NoteKeys.Length;
            return Get(NoteKeys[index - 1], NoteFallback[index - 1]);
        }

        // ---------------- 初次见面 · 第一段 ----------------
        public static List<LonaLine> FirstPurchaseLines()
        {
            return new List<LonaLine>
            {
                new LonaLine(false, Get("lona_first1_01", "……")),
                new LonaLine(true,  Get("lona_first1_02", "……")),
                new LonaLine(false, Get("lona_first1_03", "……")),
                new LonaLine(true,  Get("lona_first1_04", "要买点什么？")),
                new LonaLine(false, Get("lona_first1_05", "有食物吗？"))
            };
        }

        public static string FirstPurchaseFarewell()
        {
            return Get("lona_first1_06", "下次见");
        }

        /// <summary>第一段里每成交一件时她说的话（替代原版「还有什么事吗？」）。</summary>
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
                new LonaLine(false, Get("lona_first2_02", "是的，纸条是我写的")),
                new LonaLine(false, Get("lona_first2_03", "我犹豫了好久，最后还是光明正大的进来了")),
                new LonaLine(false, Get("lona_first2_04", "我不太擅长，也有点恐惧与人交流")),
                new LonaLine(false, Get("lona_first2_05", "总之，今后我会常来，再见。"))
            };
        }

        // ---------------- 占位辅助 ----------------
        public static string DailyLine(int tier, int index)
        {
            if (tier < 0 || tier >= DailyLines.Length) return "";
            string[] pool = DailyLines[tier];
            index %= pool.Length;
            if (index < 0) index += pool.Length;
            return pool[index];
        }

        public static int DailyLineCount(int tier)
        {
            return tier >= 0 && tier < DailyLines.Length ? DailyLines[tier].Length : 0;
        }

        public static string[] StoryLines(LonaStory story)
        {
            switch (story)
            {
                case LonaStory.Below70: return Below70;
                case LonaStory.Above80: return Above80;
                case LonaStory.Above90: return Above90;
                case LonaStory.Reach100: return Reach100;
                default: return ColdGreeting;
            }
        }
    }
}
