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

    /// <summary>
    /// 占位文案。正式文本确定后，只需要替换这个文件里的字符串（或整体搬到 Localization/zh.csv）。
    /// </summary>
    internal static class LonaText
    {
        public const string DisplayName = "洛娜";

        // ---------------- 日常剧情：4 档 ----------------
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

        // ---------------- 特殊剧情 ----------------
        public static readonly string[] FirstMeet =
        {
            "【首次登场占位】门口的风铃响得比平时早。",
            "【首次登场占位】进来的女人摘下兜帽，露出一张你在下层区从没见过的脸。",
            "【首次登场占位】“我写信写了很久，”她说，“一直没敢署名。”",
            "【首次登场占位】“我叫洛娜。”"
        };

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

        // ---------------- 卖药警告纸条 ----------------
        public static readonly string[] DrugNotes =
        {
            "【警告纸条 1 占位】\n\n我知道你在卖什么。\n\n我不喜欢那种东西，芬太片也好，梦尘也好。它们会把人变成另一种样子。\n\n——洛娜",
            "【警告纸条 2 占位】\n\n我又看见了。\n\n你要是缺钱，我可以想办法。别用这个办法。\n\n——洛娜",
            "【警告纸条 3 占位】\n\n这是最后一次了。\n\n下一次，我就不来了。\n\n——洛娜"
        };

        // ---------------- 送礼 ----------------
        public const string GiftOffer = "【送礼选项占位】（把东西放到柜台上）";
        public const string GiftHint = "【送礼提示占位】把想送的东西放上柜台吧。";
        public const string GiftThanks = "【收到礼物占位】“……谢谢。”";
        public const string GiftRefusedTooExpensive = "【拒收占位】太贵了，我不能收。";
        public const string GiftRefusedNarcotic = "【拒收占位】这个我不要。";

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
                case LonaStory.FirstMeet: return FirstMeet;
                case LonaStory.Below70: return Below70;
                case LonaStory.Above80: return Above80;
                case LonaStory.Above90: return Above90;
                case LonaStory.Reach100: return Reach100;
                default: return ColdGreeting;
            }
        }

        public static string DrugNote(int index)
        {
            if (index < 1) index = 1;
            if (index > DrugNotes.Length) index = DrugNotes.Length;
            return DrugNotes[index - 1];
        }
    }
}
