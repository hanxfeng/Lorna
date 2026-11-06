using System;
using System.Collections.Generic;
using HarmonyLib;

namespace AdmirerLona
{
    /// <summary>
    /// 洛娜的议价/指控反应。文本在 content/lona_story_zh.md 的 lona_bargain_* 键里。
    /// </summary>
    internal static class LonaBargain
    {
        private static StoreClient tracked;
        private static bool markupResolved;
        private static bool discountResolved;
        private static bool buyingMarkupResolved;
        private static readonly HashSet<Dialogue> customDialogues = new HashSet<Dialogue>();

        public static bool Prepare()
        {
            StoreClient client = null;
            if (PlayerStore.Instance != null && PlayerStore.Instance.currentClientInstance != null)
            {
                client = PlayerStore.Instance.currentClientInstance.GetClientBlueprint();
            }
            if (client != tracked)
            {
                tracked = client;
                markupResolved = false;
                discountResolved = false;
                buyingMarkupResolved = false;
            }
            return client != null && client.identifier == LonaClient.LonaId;
        }

        public static Dialogue Build(string key, bool dismiss)
        {
            LonaLine[] lines = LonaText.ReadStory(key);
            Dialogue head = new Dialogue();
            head.priority = 6;
            customDialogues.Add(head);
            Dialogue current = head;
            for (int i = 0; i < lines.Length; i++)
            {
                current.SetText(lines[i].Player ? "PLAYER" : "CLIENT", lines[i].Text);
                if (i < lines.Length - 1) current = current.NextDialogue();
            }
            if (dismiss) current.SetEndAction(Dismiss);
            return head;
        }

        public static void Start(Dialogue dialogue)
        {
            if (dialogue == null || DialogUIManager.Instance == null) return;
            try
            {
                DialogUIManager.Instance.StartDialogue(dialogue);
            }
            catch (Exception e)
            {
                ModEntry.Log("洛娜议价对话失败: " + e.Message);
            }
        }

        public static bool IsCustomDialogue(Dialogue dialogue)
        {
            return dialogue != null && customDialogues.Contains(dialogue);
        }

        public static bool TryGrateful(BargainUIManager ui, ref Dialogue result)
        {
            if (!Prepare()) return false;
            if (ui.BuyingMarkupOffered && !buyingMarkupResolved)
            {
                buyingMarkupResolved = true;
                State.AddAffection(1.0);
                result = Build("lona_bargain_buying_markup_accept", false);
                return true;
            }
            if (ui.DiscountOffered && !discountResolved)
            {
                discountResolved = true;
                State.AddAffection(1.0);
                result = Build("lona_bargain_discount_accept", false);
                return true;
            }
            return false;
        }

        public static bool TryMarkupAccept(BargainUIManager ui, ref Dialogue result)
        {
            if (!Prepare()) return false;
            if (ui.BlackmailOffered)
            {
                result = Build("lona_bargain_markup_accept", false);
                return true;
            }
            if (markupResolved || !ui.MarkupOffered) return false;
            markupResolved = true;
            State.AddAffection(-1.0);
            result = Build("lona_bargain_markup_accept", false);
            return true;
        }

        public static bool TryMarkupReject(BargainUIManager ui, ref Dialogue result)
        {
            if (!Prepare()) return false;
            if (ui.BlackmailOffered)
            {
                result = Build("lona_bargain_markup_refuse", true);
                return true;
            }
            if (markupResolved || !ui.MarkupOffered) return false;
            markupResolved = true;
            State.AddAffection(-5.0);
            result = Build("lona_bargain_markup_refuse", true);
            return true;
        }

        /// <summary>玩家点「举报」：好感 -5，但举报必定失败。</summary>
        public static bool OnReportThreat(StoreClient client)
        {
            if (!Prepare()) return false;

            State.AddAffection(-5.0);
            if (client != null)
            {
                // 游戏在调用前会把字段清空；重新挂上，保证之后再举报也失败。
                client.tryInterceptReport = delegate { return OnReportThreat(client); };
            }
            Start(Build("lona_bargain_report", false));
            return true;
        }

        /// <summary>玩家指控洛娜是改造体：好感 -10，她直接离开。</summary>
        public static bool OnAccuseAug()
        {
            if (!Prepare()) return false;
            State.AddAffection(-10.0);
            Start(Build("lona_bargain_accuse_aug", true));
            return true;
        }

        private static void Dismiss()
        {
            try
            {
                if (PlayerStore.Instance != null)
                {
                    PlayerStore.Instance.DismissCurrentClient(false);
                }
            }
            catch (Exception e)
            {
                ModEntry.Log("洛娜议价离场失败: " + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(BargainUIManager), "CreateAcceptGratefulDialog")]
    internal static class Patch_LonaBargainGrateful
    {
        private static bool Prefix(BargainUIManager __instance, ref Dialogue __result)
        {
            return !LonaBargain.TryGrateful(__instance, ref __result);
        }
    }

    [HarmonyPatch(typeof(BargainUIManager), "CreateAcceptReluctantDialog")]
    internal static class Patch_LonaBargainMarkupAccept
    {
        private static bool Prefix(BargainUIManager __instance, ref Dialogue __result)
        {
            return !LonaBargain.TryMarkupAccept(__instance, ref __result);
        }
    }

    [HarmonyPatch(typeof(BargainUIManager), "CreateRejectDialog")]
    internal static class Patch_LonaBargainMarkupReject
    {
        private static bool Prefix(BargainUIManager __instance, ref Dialogue __result)
        {
            return !LonaBargain.TryMarkupReject(__instance, ref __result);
        }
    }

    [HarmonyPatch(typeof(BargainUIManager), nameof(BargainUIManager.ThreatenAug))]
    internal static class Patch_LonaAccuseAug
    {
        private static bool Prefix()
        {
            return !LonaBargain.OnAccuseAug();
        }
    }

    [HarmonyPatch(typeof(BargainUIManager), "CreateInnocentBlackmailDialog")]
    internal static class Patch_LonaInnocentBlackmail
    {
        private static bool Prefix(ref Dialogue __result)
        {
            if (!LonaBargain.Prepare()) return true;
            __result = LonaBargain.Build("lona_bargain_report", false);
            return false;
        }
    }

    /// <summary>
    /// 原版 OfferDiscount / OfferMarkup 会在自定义对话后追加预算变化台词。
    /// 洛娜的自定义议价对话不追加这些原版文本。
    /// </summary>
    [HarmonyPatch(typeof(BargainUIManager), "AppendClientLine")]
    internal static class Patch_LonaBargainAppendLine
    {
        private static bool Prefix(Dialogue d, ref Dialogue __result)
        {
            if (!LonaBargain.IsCustomDialogue(d)) return true;
            __result = d;
            return false;
        }
    }
}
