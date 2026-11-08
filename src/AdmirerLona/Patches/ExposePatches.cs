using System;
using System.Collections.Generic;
using HarmonyLib;

namespace AdmirerLona
{
    /// <summary>
    /// 洛娜鉴定可鉴定物品：90% 概率看出真实情况；看出标签不符时好感 -1，
    /// 并把原版鉴定台词替换成洛娜专用文本。
    /// </summary>
    internal static class LonaExpose
    {
        public static bool Active;
        public static bool Detected;

        public static void Begin()
        {
            Active = true;
            Detected = false;
        }

        public static void EndAndApply()
        {
            bool hit = Active && Detected;
            Active = false;
            Detected = false;
            if (hit) State.AddAffection(-1.0);
        }

        public static void TryReplace(ItemFeature feature, ref Dialogue result)
        {
            if (!Active || feature == null) return;
            Dialogue custom = Build(feature);
            if (custom == null) return;
            Detected = true;
            result = custom;
        }

        private static Dialogue Line(string text)
        {
            Dialogue dialogue = new Dialogue();
            dialogue.priority = 6;
            dialogue.SetText(LonaText.DisplayName, text);
            return dialogue;
        }

        private static Dialogue Build(ItemFeature feature)
        {
            string category = feature.category ?? "";

            if (category == "CATEGORY_WATER_PURITY")
            {
                string display = feature.GetBaseRealDisplay() ?? "";
                if (display == WaterFeatureHelper.GUTTERFLOW_WATER_DISPLAY)
                    return Line(LonaText.ExposeWaterGutterflow());
                if (display == WaterFeatureHelper.RUST_WATER_DISPLAY)
                    return Line(LonaText.ExposeWaterRustwater());
                if (display == WaterFeatureHelper.GHOSTWATER_DISPLAY)
                    return Line(LonaText.ExposeWaterGhostwater());
                if (display == WaterFeatureHelper.BASE_WATER_DISPLAY)
                    return Line(LonaText.ExposeWaterBasewater());
                if (display == WaterFeatureHelper.HIGH_QUALITY_WATER_DISPLAY)
                    return Line(LonaText.ExposeWaterHighquality());
                return Line(LonaText.ExposeWaterOther());
            }

            if (category == "CATEGORY_GENUINE_INJECTOR")
            {
                string id = feature.realCondition != null ? feature.realCondition.identifier : "";
                if (id == "unusableInjector")
                    return Line(LonaText.ExposeInjectorUnusable());
                if (id == "expiredInjector")
                    return Line(LonaText.ExposeInjectorExpired());
                return Line(LonaText.ExposeInjectorLabelWrong());
            }

            if (category == "CATEGORY_CHEMICAL_PURITY")
            {
                string id = feature.realCondition != null ? feature.realCondition.identifier : "";
                if (id == "pharma_grade")
                    return Line(LonaText.ExposeChemicalPharmaGrade());
                if (id == "near_pharma")
                    return Line(LonaText.ExposeChemicalNearPharma());
                if (id == "street_standard")
                    return Line(LonaText.ExposeChemicalStreetStandard());
                if (id == "cut_product")
                    return Line(LonaText.ExposeChemicalCutProduct());
                if (id == "junk_grade")
                    return Line(LonaText.ExposeChemicalJunkGrade());
                if (id == "starch_grade")
                    return Line(LonaText.ExposeChemicalStarchGrade());
                return Line(LonaText.ExposeGeneric());
            }

            if (category == "CATEGORY_GENUINE_CIGARETTE")
            {
                Dialogue cigarette = Line(LonaText.ExposeCigNotGenuine());
                List<string> issues = new List<string>();
                try
                {
                    GameItem item = EmporiumEntry.Instance != null
                        ? EmporiumEntry.Instance.FindItemByUniqueID(feature.parentItemUniqueId)
                        : null;
                    if (item != null)
                    {
                        if (item.IsTag("CIGARETTE_LETTERING_ISSUE")) issues.Add(LocHelper.GetLocalizedMechanic("ins_cig_issue_lettering"));
                        if (item.IsTag("CIGARETTE_COLOR_ISSUE")) issues.Add(LocHelper.GetLocalizedMechanic("ins_cig_issue_color"));
                        if (item.IsTag("CIGARETTE_SEAL_ISSUE")) issues.Add(LocHelper.GetLocalizedMechanic("ins_cig_issue_seal"));
                        if (item.IsTag("CIGARETTE_LOGO_ISSUE")) issues.Add(LocHelper.GetLocalizedMechanic("ins_cig_issue_logo"));
                        if (item.IsTag("CIGARETTE_BOX_ISSUE")) issues.Add(LocHelper.GetLocalizedMechanic("ins_cig_issue_box"));
                    }
                }
                catch (Exception e)
                {
                    ModEntry.Log("读取假烟问题失败: " + e.Message);
                }
                if (issues.Count > 0)
                {
                    string second = string.Format(LonaText.ExposeCigIssuesWith(), string.Join(", ", issues));
                    cigarette.NextDialogue().SetText(LonaText.DisplayName, second);
                }
                return cigarette;
            }

            // 开箱/生锈类别原版没有具体台词，保持原样。
            if (category == "CATEGORY_OPEN_BOX" || category == "CATEGORY_RUSTED")
                return null;

            return Line(LonaText.ExposeGeneric());
        }
    }

    [HarmonyPatch(typeof(StoreClient), nameof(StoreClient.ClientExposeFeature))]
    internal static class Patch_LonaExposeFeature
    {
        private static bool Prefix(StoreClient __instance)
        {
            if (!LonaClient.IsLona(__instance))
            {
                LonaExpose.Active = false;
                LonaExpose.Detected = false;
                return true;
            }
            if (!RNG.Roll(90))
            {
                LonaExpose.Active = false;
                LonaExpose.Detected = false;
                return false;
            }
            LonaExpose.Begin();
            return true;
        }

        private static void Postfix(StoreClient __instance)
        {
            if (!LonaClient.IsLona(__instance)) return;
            LonaExpose.EndAndApply();
        }
    }

    [HarmonyPatch(typeof(ItemFeature), nameof(ItemFeature.GetClientExposeDialog))]
    internal static class Patch_LonaExposeDialog
    {
        private static void Postfix(ItemFeature __instance, ref Dialogue __result)
        {
            LonaExpose.TryReplace(__instance, ref __result);
        }
    }
}
