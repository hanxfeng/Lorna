using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdmirerLona
{
    /// <summary>洛娜的电话：203169。联系人、拨号对话、预约送货和冷却。</summary>
    internal static class Phone
    {
        public const long Number = 203169L;
        public const string DialogId = "Lona";
        private const string PlayerSpeaker = "PLAYER";

        public static bool HasContact
        {
            get
            {
                return PlayerStore.Instance != null
                       && PlayerStore.Instance.PhoneClientDict != null
                       && PlayerStore.Instance.PhoneClientDict.ContainsKey(Number);
            }
        }

        public static void EnsureRegistered()
        {
            if (PhoneClientList.DialogFuncs == null) return;
            if (!PhoneClientList.DialogFuncs.ContainsKey(DialogId))
            {
                PhoneClientList.DialogFuncs[DialogId] = BuildDialog;
            }
        }

        public static void Unregister()
        {
            if (PhoneClientList.DialogFuncs != null)
            {
                PhoneClientList.DialogFuncs.Remove(DialogId);
            }
        }

        /// <summary>揭面后把 203169 加进电话簿；读档/开门时也会补。</summary>
        public static void EnsureContact(bool force = false)
        {
            if (PlayerStore.Instance == null || PlayerStore.Instance.PhoneClientDict == null) return;
            if (!force && State.Left)
            {
                RemoveContact();
                return;
            }
            if (!force && !State.Revealed) return;
            EnsureRegistered();

            Dictionary<long, StorePhoneClient> dict = PlayerStore.Instance.PhoneClientDict;
            StorePhoneClient client;
            if (dict.TryGetValue(Number, out client) && client != null)
            {
                client.dialogFuncId = DialogId;
                client.dialedBefore = true;
                if (client.phoneState == StorePhoneClient.PhoneState.None
                    || client.phoneState == StorePhoneClient.PhoneState.NotResponding)
                {
                    client.phoneState = StorePhoneClient.PhoneState.Regular;
                }
                if (client.alias == null) client.alias = new List<string>();
                if (!client.alias.Contains(LonaClient.LonaId)) client.alias.Add(LonaClient.LonaId);
                if (client.cooldownDuration <= 0) client.cooldownDuration = State.NormalDeliveryDaysValue;
                if (client.phoneState == StorePhoneClient.PhoneState.Cooldown && client.currentCooldown <= 0)
                {
                    client.phoneState = StorePhoneClient.PhoneState.Regular;
                }
                return;
            }

            dict[Number] = new StorePhoneClient
            {
                displayName = LonaText.DisplayName,
                locID = "",
                dialogFuncId = DialogId,
                phoneState = StorePhoneClient.PhoneState.Regular,
                phoneClientType = StorePhoneClient.PhoneClientType.None,
                dialedBefore = true,
                cooldownDuration = State.NormalDeliveryDaysValue,
                alias = new List<string> { LonaClient.LonaId }
            };
            ModEntry.Log("电话联系人已添加：203169");
        }

        public static void RemoveContact()
        {
            if (PlayerStore.Instance == null || PlayerStore.Instance.PhoneClientDict == null) return;
            if (PlayerStore.Instance.PhoneClientDict.Remove(Number))
            {
                ModEntry.Log("电话联系人已移除：203169");
            }
        }

        // ---------------------------------------------------------------
        // 拨号对话
        // ---------------------------------------------------------------
        private static Dialogue BuildDialog(StorePhoneClient client)
        {
            Dialogue hello = new Dialogue();
            hello.SetText(LonaText.DisplayName, LonaText.PhoneHello());

            Dialogue what = hello.NextDialogue();
            what.SetText(LonaText.DisplayName, LonaText.PhoneWhat());
            AddChoice(what, LonaText.PhoneDeliver(), null, BuildDeliveryMenu(client));
            AddChoice(what, LonaText.PhoneStatus(), null, BuildStatusDialog());
            return hello;
        }

        /// <summary>选「委托洛娜带一些东西」后：需要什么？右侧列出品类。</summary>
        private static Dialogue BuildDeliveryMenu(StorePhoneClient client)
        {
            if (client != null && client.phoneState == StorePhoneClient.PhoneState.Cooldown)
            {
                Dialogue busy = new Dialogue();
                busy.SetText(LonaText.DisplayName, LonaText.PhoneBusyFirst());

                Dialogue wait = busy.NextDialogue();
                wait.SetText(LonaText.DisplayName,
                    LonaText.PhoneBusyDays(Mathf.Max(0, client.currentCooldown)));

                Dialogue ok = wait.NextDialogue();
                ok.SetText(PlayerSpeaker, LonaText.PhoneBusyOk());
                ok.SetEndAction(EndCall);
                return busy;
            }

            List<LonaDeliveryCategory> specials = Delivery.UnlockedSpecialCategories();
            Dialogue page3 = BuildThirdPage(client, specials);
            Dialogue page2 = BuildSecondPage(client, page3);

            List<LonaDeliveryCategory> baseCategories = Delivery.BaseCategories();
            Dialogue ask = new Dialogue();
            ask.SetText(LonaText.DisplayName, LonaText.PhoneAsk());
            for (int i = 0; i < baseCategories.Count && i < 2; i++)
            {
                LonaDeliveryCategory category = baseCategories[i];
                AddChoice(ask, category.Name, delegate { Choose(client, category); });
            }
            AddChoice(ask, LonaText.PhoneMore(), null, page2);
            return ask;
        }

        /// <summary>选「问问近况」后：最近……还算不错。然后显示当前好感度，结束通话。</summary>
        private static Dialogue BuildStatusDialog()
        {
            Dialogue status = new Dialogue();
            status.SetText(LonaText.DisplayName, LonaText.PhoneStatusLine());

            Dialogue affection = status.NextDialogue();
            affection.SetText(LonaText.DisplayName, LonaText.PhoneAffection(State.Affection));
            affection.SetEndAction(EndCall);
            return status;
        }

        private static Dialogue BuildSecondPage(StorePhoneClient client, Dialogue page3)
        {
            Dialogue page = new Dialogue();
            page.SetText(LonaText.DisplayName, LonaText.PhoneAsk());

            List<LonaDeliveryCategory> baseCategories = Delivery.BaseCategories();
            for (int i = 2; i < baseCategories.Count && i < 4; i++)
            {
                LonaDeliveryCategory category = baseCategories[i];
                AddChoice(page, category.Name, delegate { Choose(client, category); });
            }
            AddChoice(page, LonaText.PhoneMore(), null, page3);
            return page;
        }

        private static Dialogue BuildThirdPage(StorePhoneClient client, List<LonaDeliveryCategory> specials)
        {
            Dialogue page = new Dialogue();
            page.SetText(LonaText.DisplayName, LonaText.PhoneAsk());

            List<LonaDeliveryCategory> baseCategories = Delivery.BaseCategories();
            if (baseCategories.Count > 4)
            {
                LonaDeliveryCategory category = baseCategories[4];
                AddChoice(page, category.Name, delegate { Choose(client, category); });
            }

            if (specials.Count > 0)
            {
                AddChoice(page, LonaText.PhoneSpecial(), null, BuildSpecialPage(client, specials, 0, page));
            }
            AddChoice(page, LonaText.PhoneCancel(), null, CancelDialog());
            return page;
        }

        private static Dialogue BuildSpecialPage(StorePhoneClient client,
            List<LonaDeliveryCategory> specials, int offset, Dialogue backTo)
        {
            Dialogue page = new Dialogue();
            page.SetText(LonaText.DisplayName, LonaText.PhoneAsk());

            int shown = 0;
            for (int i = offset; i < specials.Count && shown < 2; i++, shown++)
            {
                LonaDeliveryCategory category = specials[i];
                AddChoice(page, category.Name, delegate { Choose(client, category); });
            }

            if (offset + 2 < specials.Count)
            {
                AddChoice(page, LonaText.PhoneMore(), null,
                    BuildSpecialPage(client, specials, offset + 2, backTo));
            }
            else
            {
                AddChoice(page, LonaText.PhoneBack(), null, backTo);
            }
            return page;
        }

        private static void AddChoice(Dialogue page, string label, Action action, Dialogue next = null)
        {
            // 电话/对话 UI 的选项文字取的是「下一个 Dialogue 的正文」，所以先用一行
            // 玩家气泡承载选项文字，再接到真正的下一页/确认页。
            Dialogue line = new Dialogue();
            line.SetText(PlayerSpeaker, label);
            line.nextDialogue = next ?? ConfirmDialog();
            page.AddChoice(DirectoryMaster.Item("$"), "", "", line, action);
        }

        private static Dialogue ConfirmDialog()
        {
            Dialogue confirm = new Dialogue();
            confirm.SetText(LonaText.DisplayName, LonaText.PhoneConfirm());
            confirm.SetEndAction(EndCall);
            return confirm;
        }

        private static Dialogue CancelDialog()
        {
            Dialogue cancel = new Dialogue();
            cancel.SetText(LonaText.DisplayName, LonaText.PhoneCancel());
            cancel.SetEndAction(EndCall);
            return cancel;
        }

        private static void Choose(StorePhoneClient client, LonaDeliveryCategory category)
        {
            if (client == null || category == null) return;
            try
            {
                int tier = Delivery.CurrentTier();
                int count = Delivery.CountForAffection();
                bool free = Delivery.FreeForAffection();
                State.QueueDelivery(category.Id, count, free, tier, State.Today + 1);

                client.cooldownDuration = category.CooldownDays;
                client.UseService();
                ModEntry.Log("电话预约：" + category.Id + " x" + count +
                             (free ? "（免费）" : "") + "，冷却 " + client.cooldownDuration + " 天");
            }
            catch (Exception e)
            {
                ModEntry.Log("电话预约失败: " + e.Message);
            }
        }

        private static void EndCall()
        {
            try
            {
                if (PhoneUIManager.Instance != null)
                {
                    PhoneUIManager.Instance.OnPhoneDialogEnd();
                }
            }
            catch (Exception e)
            {
                ModEntry.Log("结束电话对话失败: " + e.Message);
            }
        }

        // ---------------------------------------------------------------
        // 调试
        // ---------------------------------------------------------------
        public static void DebugCall()
        {
            EnsureContact(true);
            if (PhoneUIManager.Instance == null) return;
            PhoneUIManager.Instance.OpenUI();
            PhoneUIManager.Instance.AutoCall(Number);
        }

        public static void DebugAddContact()
        {
            EnsureContact(true);
            ModEntry.Log("调试：电话联系人已补上（203169）");
        }

        public static void DebugQueue(string categoryId)
        {
            LonaDeliveryCategory category = Delivery.Find(categoryId);
            if (category == null) return;
            State.QueueDelivery(category.Id, Delivery.CountForAffection(),
                Delivery.FreeForAffection(), Delivery.CurrentTier(), State.Today);
            ModEntry.Log("调试：已预约送货 " + category.Id + "（下次开门）");
        }

        public static void DebugTierStory(LonaStory story)
        {
            State.QueueTierStory((int)story, State.Today);
            ModEntry.Log("调试：已排队档位剧情 " + story + "（下次开门）");
        }

        public static void DebugClearCooldown()
        {
            StorePhoneClient client = GetContact();
            if (client == null) return;
            client.phoneState = StorePhoneClient.PhoneState.Regular;
            client.currentCooldown = 0;
            ModEntry.Log("调试：电话冷却已清除");
        }

        public static void DebugGiveCard()
        {
            Delivery.GiveToPlayer(DirectoryMaster.Item(LonaCard.ItemId));
            ModEntry.Log("调试：洛娜的名片已放入背包");
        }

        public static void DebugLogState()
        {
            StorePhoneClient client = GetContact();
            ModEntry.Log("电话状态：contact=" + (client != null)
                + " state=" + (client != null ? client.phoneState.ToString() : "-")
                + " cooldown=" + (client != null ? client.currentCooldown : -1)
                + " pendingDelivery=" + State.PendingDeliveryDay + "/" + State.PendingDeliveryCategory
                + " pendingTier=" + State.PendingTierStory + "@" + State.PendingTierDay
                + " UL=" + StoreReputation.GetULReputation()
                + " affection=" + State.AffectionInt);
        }

        private static StorePhoneClient GetContact()
        {
            if (PlayerStore.Instance == null || PlayerStore.Instance.PhoneClientDict == null) return null;
            StorePhoneClient client;
            return PlayerStore.Instance.PhoneClientDict.TryGetValue(Number, out client) ? client : null;
        }
    }
}
