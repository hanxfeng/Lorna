using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AdmirerLona
{
    /// <summary>洛娜的名片：自定义物品，双击/使用后走原版 PaperUIManager 的名片面板。</summary>
    internal static class LonaCard
    {
        public const string ItemId = "lona_card";
        private const string IconKey = "lona_card";
        private const string FrontKey = "lona_card_front";
        private const string NativeCardId = "card_rev";

        public static void Register(ModItemDirectory directory)
        {
            if (directory == null) return;
            directory.Add(ItemId, CreateItem);
        }

        private static GameItem CreateItem()
        {
            GameItem item = ItemDirectory.CreateEmptyItem(ItemId);
            item.name = LonaText.Get("lona_card_name", "洛娜的名片");
            item.shortDescription = LonaText.Get("lona_card_short", "洛娜的名片，按使用键查看。");
            item.flavorText = LonaText.Get("lona_card_flavor",
                "深色卡片上印着她的名字、职位和电话 203169。");
            item.unitValue = 0L;
            item.SetSpriteAndShapeFromMod(State.ModId, IconKey);
            item.SetGameItemType("DOCUMENT");
            item.EnableTag("IMPORTANT_TAG");
            item.canActivateSlotItemFunc = (GameItem _, GameInventory _, SlotMarker _) => true;
            item.mayActivateSlotItemFunc = (GameItem _, GameInventory _, SlotMarker _) => true;
            item.onActivateSlotItemFunc = delegate { Open(); };
            AudioHelper.InitCartonCard(item);
            return item;
        }

        public static void Open()
        {
            if (PaperUIManager.Instance == null)
            {
                ModEntry.Log("PaperUIManager 还没准备好，无法打开洛娜的名片");
                return;
            }

            Sprite front = FrontSprite();
            if (front == null)
            {
                ModEntry.Log("找不到洛娜名片正面贴图：" + FrontKey);
                return;
            }

            // 完全复用原版名片（_RevCard）的打开方式、音效和关闭按钮，
            // 只把面板上的卡面贴图和旧的文字换掉。
            PaperUIManager.Instance.OpenCard(NativeCardId);
            GameObject panel = PaperUIManager.Instance.cardRev;
            if (panel == null) return;

            LonaCardView view = panel.GetComponent<LonaCardView>();
            if (view == null) view = panel.AddComponent<LonaCardView>();
            view.Apply(front);
        }

        private static Sprite FrontSprite()
        {
            LoadedMod mod = ModLoader.Mods.Find(m => m.Manifest != null && m.Manifest.ID == State.ModId);
            if (mod == null || mod.PaperSprites == null) return null;
            Sprite sprite;
            return mod.PaperSprites.TryGetValue(FrontKey, out sprite) ? sprite : null;
        }
    }

    /// <summary>
    /// 挂在原版 _RevCard 面板上：打开我们的名片时换图/藏掉原版文字，
    /// 面板被 SetActive(false) 关闭时（OnDisable）自动还原，原版名片不受影响。
    /// </summary>
    internal sealed class LonaCardView : MonoBehaviour
    {
        private Sprite originalSprite;
        private bool originalPreserveAspect = true;
        private Image.Type originalType = Image.Type.Simple;
        private TextMeshProUGUI text;
        private bool textWasActive;
        private bool applied;

        public void Apply(Sprite sprite)
        {
            if (sprite == null) return;
            Image image = GetComponent<Image>();
            if (image == null) return;

            if (!applied)
            {
                originalSprite = image.sprite;
                originalPreserveAspect = image.preserveAspect;
                originalType = image.type;

                Transform textTransform = transform.Find("Text (TMP)");
                if (textTransform != null) text = textTransform.GetComponent<TextMeshProUGUI>();
                if (text == null) text = GetComponentInChildren<TextMeshProUGUI>(true);
                if (text != null) textWasActive = text.gameObject.activeSelf;
                applied = true;
            }

            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            if (text != null) text.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (!applied) return;

            Image image = GetComponent<Image>();
            if (image != null)
            {
                image.sprite = originalSprite;
                image.preserveAspect = originalPreserveAspect;
                image.type = originalType;
            }
            if (text != null) text.gameObject.SetActive(textWasActive);
            applied = false;
        }
    }
}
