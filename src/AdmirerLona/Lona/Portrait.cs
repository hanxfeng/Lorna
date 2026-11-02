using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdmirerLona
{
    /// <summary>
    /// 立绘：模组 Textures/lona.png、lona_thief.png 载入成 Texture2D 后，
    /// 在运行时造一个 Sprite 塞进 SpriteDict 的字典里，顾客就能用 spriteName 引用。
    /// 没有图时返回 false，调用方回退到原版立绘。
    /// </summary>
    internal static class Portrait
    {
        public const string KeyLona = "lona";
        public const string KeyThief = "lona_thief";

        private static bool injected;
        private static bool loggedInfo;
        private static float injectedScale = -1f;

        public static void EnsureInjected()
        {
            if (SpriteDict.Instance == null || SpriteDict.Instance.spriteDictionary == null) return;

            if (!loggedInfo)
            {
                loggedInfo = true;
                LogSpriteInfo();
            }

            float scale = State.PortraitScaleValue;
            if (injected && Mathf.Abs(scale - injectedScale) < 0.001f) return;

            Inject(KeyLona, scale);
            Inject(KeyThief, scale);
            injected = true;
            injectedScale = scale;
        }

        /// <summary>给调试面板用的一行摘要（不依赖控制台）。</summary>
        public static string DescribeReference()
        {
            if (SpriteDict.Instance == null || SpriteDict.Instance.spriteDictionary == null)
            {
                return "SpriteDict 还没准备好（进游戏后再打开）";
            }

            Dictionary<string, Sprite> dict = SpriteDict.Instance.spriteDictionary;
            List<string> keys = new List<string>(dict.Keys);
            keys.Sort(StringComparer.Ordinal);

            foreach (string key in keys)
            {
                if (!IsClientKey(key)) continue;
                Sprite sprite = dict[key];
                if (sprite == null) continue;
                Rect rect = sprite.rect;
                return key + ": " + rect.width + "x" + rect.height +
                       "  ppu=" + sprite.pixelsPerUnit.ToString("0.##") +
                       "  pivot=(" + sprite.pivot.x.ToString("0.#") + "," + sprite.pivot.y.ToString("0.#") + ")" +
                       "  世界=" + (rect.width / sprite.pixelsPerUnit).ToString("0.##") + "x" +
                       (rect.height / sprite.pixelsPerUnit).ToString("0.##") +
                       "  （共 " + dict.Count + " 张贴图）";
            }
            return "没找到顾客类贴图（共 " + dict.Count + " 张）";
        }

        /// <summary>把原版顾客立绘的尺寸/像素比打给画师看（只打一次，或从调试面板手动再打）。</summary>
        public static void LogSpriteInfo()
        {
            if (SpriteDict.Instance == null || SpriteDict.Instance.spriteDictionary == null)
            {
                ModEntry.Log("立绘信息：SpriteDict 还没准备好，进游戏后再试");
                return;
            }

            Dictionary<string, Sprite> dict = SpriteDict.Instance.spriteDictionary;
            ModEntry.Log("=== 立绘参考：原版顾客贴图（共 " + dict.Count + " 张） ===");

            List<string> keys = new List<string>(dict.Keys);
            keys.Sort(StringComparer.Ordinal);

            int printed = 0;
            foreach (string key in keys)
            {
                if (printed >= 6) break;
                if (!IsClientKey(key)) continue;
                LogOne(key, dict[key]);
                printed++;
            }
            if (printed == 0)
            {
                foreach (string key in keys)
                {
                    LogOne(key, dict[key]);
                    if (++printed >= 5) break;
                }
            }

            foreach (string key in new[] { KeyLona, KeyThief })
            {
                Texture2D texture = GetModTexture(key);
                if (texture == null)
                {
                    ModEntry.Log("立绘：" + key + ".png 未提供（当前用原版立绘占位）");
                }
                else
                {
                    ModEntry.Log("立绘：" + key + ".png " + texture.width + "x" + texture.height);
                }
            }
        }

        private static bool IsClientKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return key.StartsWith("lowerMale") || key.StartsWith("maleLower")
                   || key.StartsWith("shady") || key.StartsWith("wanted")
                   || key.StartsWith("scav") || key.StartsWith("maleScav");
        }

        private static void LogOne(string key, Sprite sprite)
        {
            if (sprite == null)
            {
                ModEntry.Log("  sprite " + key + ": null");
                return;
            }
            Rect rect = sprite.rect;
            string filter = sprite.texture != null ? sprite.texture.filterMode.ToString() : "?";
            ModEntry.Log("  sprite " + key + ": " + rect.width + "x" + rect.height +
                         " ppu=" + sprite.pixelsPerUnit.ToString("0.##") +
                         " pivot=(" + sprite.pivot.x.ToString("0.#") + "," + sprite.pivot.y.ToString("0.#") + ")" +
                         " filter=" + filter +
                         " 世界尺寸=" + (rect.width / sprite.pixelsPerUnit).ToString("0.##") + "x" +
                         (rect.height / sprite.pixelsPerUnit).ToString("0.##"));
        }

        public static bool Has(string key)
        {
            return SpriteDict.Instance != null
                   && SpriteDict.Instance.spriteDictionary != null
                   && SpriteDict.Instance.spriteDictionary.ContainsKey(key);
        }

        private static void Inject(string key, float scale)
        {
            Texture2D texture = GetModTexture(key);
            if (texture == null)
            {
                ModEntry.Log("立绘占位：" + key + ".png 不存在，使用原版立绘");
                return;
            }

            Sprite reference = FindReferenceSprite();
            Sprite sprite = CreateSprite(texture, reference, scale);
            if (sprite == null) return;

            SpriteDict.Instance.spriteDictionary[key] = sprite;
            ModEntry.Log("立绘已注入：" + key + " (" + texture.width + "x" + texture.height +
                         " ppu=" + sprite.pixelsPerUnit.ToString("0.##") +
                         " scale=" + scale.ToString("0.##") +
                         " 世界=" + (texture.width / sprite.pixelsPerUnit).ToString("0.##") + "x" +
                         (texture.height / sprite.pixelsPerUnit).ToString("0.##") + ")");
        }

        private static Texture2D GetModTexture(string key)
        {
            LoadedMod mod = ModLoader.Mods.Find(m => m.Manifest != null && m.Manifest.ID == State.ModId);
            if (mod == null || mod.Textures == null) return null;
            Texture2D texture;
            return mod.Textures.TryGetValue(key, out texture) ? texture : null;
        }

        /// <summary>找一个原版顾客立绘当尺寸参考，复制它的像素比和轴心。</summary>
        private static Sprite FindReferenceSprite()
        {
            Sprite fallback = null;
            foreach (var entry in SpriteDict.Instance.spriteDictionary)
            {
                if (entry.Value == null) continue;
                if (fallback == null) fallback = entry.Value;
                string key = entry.Key ?? "";
                if (key.StartsWith("lowerMale") || key.StartsWith("maleLower") || key.StartsWith("shady"))
                {
                    return entry.Value;
                }
            }
            return fallback;
        }

        private static Sprite CreateSprite(Texture2D texture, Sprite reference, float scale)
        {
            if (texture == null) return null;

            // 原版顾客贴图用的是点采样（filter=Point），这里保持一致，否则会糊。
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            if (scale <= 0.05f) scale = 0.05f;

            float pixelsPerUnit = 100f / scale;
            Vector2 pivot = new Vector2(0.5f, Mathf.Min(1f, 0.5f / scale));

            if (reference != null && reference.rect.width > 0f && reference.rect.height > 0f)
            {
                // 世界宽度对齐参考图，再按 scale 整体缩放（scale 越小人物越矮）
                pixelsPerUnit = reference.pixelsPerUnit * (texture.width / reference.rect.width) / scale;

                // 轴心往下压，保证缩小时脚底/底边不会浮起来
                float pivotY = reference.pivot.y / reference.rect.height;
                pivot = new Vector2(reference.pivot.x / reference.rect.width,
                                    Mathf.Clamp(pivotY / scale, 0f, 0.98f));
            }

            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), pivot, pixelsPerUnit);
        }
    }
}
