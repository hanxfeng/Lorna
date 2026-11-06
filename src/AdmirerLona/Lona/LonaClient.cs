using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdmirerLona
{
    internal enum LonaVisit
    {
        Story,
        Buy,
        Sell,
        FirstPurchase,
        Reveal,
        StoryBuy
    }

    /// <summary>构造顾客：150 前的「小偷形态」和 150 后的「洛娜形态」。</summary>
    internal static class LonaClient
    {
        public const string LonaId = "lona";

        private static readonly string[] FoodItems =
        {
            "cup_noodle", "processed_milk", "galaxy_blend", "beis_icecream"
        };

        private static readonly string[] WaterItems =
        {
            "bottled_water", "small_bottled_water", "processed_juice"
        };

        private static readonly string[] MedicalItems =
        {
            "bandage_item", "salve", "blood_bag"
        };

        private static readonly string[] HouseholdItems =
        {
            "paper_towel", "toilet_paper", "shampoo", "toothpaste"
        };

        private static readonly string[] MaterialItems =
        {
            "scrap_metal", "wire", "junk"
        };

        private static readonly string[] DefaultItems =
        {
            "paper_towel", "cup_noodle", "bandage_item"
        };

        // ---------------------------------------------------------------
        // 150 声望之前：只是换了立绘的小偷
        // ---------------------------------------------------------------
        public static StoreClient CreateThiefForm()
        {
            Portrait.EnsureInjected();
            StoreClient client = StoreClientList.CreateThief();
            if (Portrait.Has(Portrait.KeyThief))
            {
                client.spriteName = Portrait.KeyThief;
            }
            else
            {
                ModEntry.Log("窃贼洛娜立绘未注入，本次使用原版小偷立绘");
            }
            client.sourceModId = "";
            return client;
        }

        // ---------------------------------------------------------------
        // 电话预约送货：队尾到店，卖货或 100 好感免费送
        // ---------------------------------------------------------------
        public static StoreClient CreateDelivery(List<GameItem> items, bool free)
        {
            Portrait.EnsureInjected();
            StoreClient client = new StoreClient();
            client.identifier = LonaId;
            client.displayName = LonaText.DisplayName;
            client.clientFaction = "FACTION_LOWER_LEVEL";
            StoreClientFactionSetup.InitLowerLevelCitizen(client);
            client.spriteName = Portrait.Has(Portrait.KeyLona)
                ? Portrait.KeyLona
                : SpriteDict.GetRandomLowerLevelSpriteName();
            if (client.spriteName != Portrait.KeyLona)
            {
                ModEntry.Log("送货：洛娜立绘未注入，本次回退原版立绘");
            }
            client.dismissable = true;
            client.arrestable = true;
            client.cooldown = 1;
            client.onReportAction = delegate { State.Leave("reported by the player"); };

            client.clientIntent = StoreClient.ClientIntent.SELL;
            client.mainDialogue.SetText(client.displayName,
                    free ? LonaText.DeliveryFreeLine() : LonaText.DeliveryGreeting())
                .SetEndAction(delegate
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        if (free) items[i].AddItemFeature(ItemFeatureList.FreeFeature());
                        PlayerStore.Instance.AddDirectSellingItemToTable(items[i], false, false, true);
                    }
                });

            client.AddBasicDialog();
            client.CompleteClientCreation();
            client.sourceModId = "";
            ModEntry.Log("送货顾客已构造：sprite=" + client.spriteName + " free=" + free + " items=" + items.Count);
            return client;
        }

        // ---------------------------------------------------------------
        // 150 声望之后
        // ---------------------------------------------------------------
        public static StoreClient Create(LonaVisit visit, LonaStory story, int dailyTier = -1, int dailyStory = 0)
        {
            Portrait.EnsureInjected();
            StoreClient client = new StoreClient();
            client.identifier = LonaId;
            client.displayName = LonaText.DisplayName;
            client.clientFaction = "FACTION_LOWER_LEVEL";
            StoreClientFactionSetup.InitLowerLevelCitizen(client);
            client.spriteName = Portrait.Has(Portrait.KeyLona)
                ? Portrait.KeyLona
                : SpriteDict.GetRandomLowerLevelSpriteName();
            if (client.spriteName != Portrait.KeyLona)
            {
                ModEntry.Log("洛娜立绘未注入，本次回退原版立绘");
            }
            client.dismissable = true;
            client.arrestable = true;
            client.cooldown = 1;
            // 拒绝加价时让她留在店里播完拒绝对话，而不是直接走「离开」分支。
            client.staysAfterFailedBargain = true;
            // 举报洛娜：好感 -5，但举报必定失败。
            client.tryInterceptReport = delegate { return LonaBargain.OnReportThreat(client); };
            // 举报 = 永久离开（与卖四次芬太片/梦尘相同）；正常路径会被 tryInterceptReport 拦下。
            client.onReportAction = delegate { State.Leave("reported by the player"); };

            switch (visit)
            {
                case LonaVisit.Buy:
                    SetupBuyer(client);
                    break;
                case LonaVisit.Sell:
                    SetupSeller(client);
                    break;
                case LonaVisit.FirstPurchase:
                    SetupFirstPurchase(client);
                    break;
                case LonaVisit.Reveal:
                    client.clientIntent = StoreClient.ClientIntent.DIALOGUE;
                    break;
                case LonaVisit.StoryBuy:
                    SetupStoryBuy(client, dailyStory);
                    break;
                default:
                    client.clientIntent = StoreClient.ClientIntent.DIALOGUE;
                    break;
            }

            BuildDialogue(client, visit, story, dailyTier, dailyStory);
            client.AddBasicDialog();
            client.CompleteClientCreation();
            ApplyVisitOverrides(client, visit, dailyStory);
            client.sourceModId = "";
            return client;
        }

        private static void SetupBuyer(StoreClient client)
        {
            client.clientIntent = StoreClient.ClientIntent.BUY;
            client.clientBuyingTagList.Add("HOUSEHOLD_GOOD");
            client.clientBuyingTagList.Add("FOOD");
            client.clientBuyingTagList.Add("MEDICAL");
            // 初始 200；上层区 70 的升职剧情触发后升到 300。
            int budget = State.Upper70StoryDone ? 300 : 200;
            client.SetBudget(budget);
            ModEntry.Log("洛娜买货预算：" + budget + (State.Upper70StoryDone ? "（70 剧情后）" : ""));
            client.willAcceptItem = AcceptsAsBuyer;
        }

        private static void SetupSeller(StoreClient client)
        {
            client.clientIntent = StoreClient.ClientIntent.SELL;
        }

        /// <summary>第一次见面：只买食物，预算 200。</summary>
        private static void SetupFirstPurchase(StoreClient client)
        {
            client.clientIntent = StoreClient.ClientIntent.BUY;
            client.clientBuyingTagList.Add("FOOD");
            client.SetBudget(200);
            client.willAcceptItem = delegate(GameItem item)
            {
                return item != null
                    && item.IsTag("FOOD")
                    && !item.IsTag("NARCOTIC")
                    && !ContrabandHelper.IsContraband(item);
            };
        }

        /// <summary>剧情型购买（日常 1 / 12）：只买食物，带固定预算。</summary>
        private static void SetupStoryBuy(StoreClient client, int storyId)
        {
            client.clientIntent = StoreClient.ClientIntent.BUY;
            client.clientBuyingTagList.Add("FOOD");
            int budget = LonaText.DailyStoryBudget(storyId);
            if (budget <= 0) budget = 30;
            client.SetBudget(budget);
            client.willAcceptItem = delegate(GameItem item)
            {
                return item != null
                    && item.IsTag("FOOD")
                    && !item.IsTag("NARCOTIC")
                    && !ContrabandHelper.IsContraband(item);
            };
            ModEntry.Log("剧情购买：日常 " + storyId + "，预算 " + budget);
        }

        /// <summary>收购：日用品/食物/医疗；麻醉品和违禁品一律不收。</summary>
        private static bool AcceptsAsBuyer(GameItem item)
        {
            if (item == null) return false;
            if (item.IsTag("NARCOTIC")) return false;
            if (ContrabandHelper.IsContraband(item)) return false;
            return item.IsTag("HOUSEHOLD_GOOD") || item.IsTag("FOOD") || item.IsTag("MEDICAL");
        }

        // ---------------------------------------------------------------
        // 对话
        // ---------------------------------------------------------------
        private static void BuildDialogue(StoreClient client, LonaVisit visit, LonaStory story, int dailyTier, int dailyStory)
        {
            if (visit == LonaVisit.FirstPurchase)
            {
                BuildStoryLines(client, LonaText.FirstPurchaseLines());
                return;
            }
            if (visit == LonaVisit.Reveal)
            {
                BuildRevealDialogue(client);
                return;
            }
            if (visit == LonaVisit.Story && story == LonaStory.Reach100)
            {
                BuildConfession(client);
                return;
            }

            List<LonaLine> lines = new List<LonaLine>();
            if (visit == LonaVisit.Story || visit == LonaVisit.StoryBuy)
            {
                if (story == LonaStory.Daily)
                {
                    LonaLine[] daily = LonaText.DailyStory(dailyStory);
                    if (daily != null) lines.AddRange(daily);
                }
                else
                {
                    lines.AddRange(LonaText.StoryLines(story));
                }
            }
            else if (visit == LonaVisit.Buy)
            {
                lines.Add(new LonaLine(false, LonaText.GenericBuyIntro()));
            }
            else
            {
                lines.Add(new LonaLine(false, LonaText.GenericSellIntro()));
            }

            Dialogue last = client.mainDialogue;
            for (int i = 0; i < lines.Count; i++)
            {
                if (visit == LonaVisit.Story && story == LonaStory.Above90 && i == 3)
                {
                    last.startAction = GiveIceCream;
                }
                string speaker = lines[i].Player ? "PLAYER" : client.displayName;
                last.SetText(speaker, lines[i].Text);
                if (i < lines.Count - 1)
                {
                    last = last.NextDialogue();
                }
            }

            if (visit == LonaVisit.Buy)
            {
                Dialogue giftLabel = PlayerLine(LonaText.GiftHint);
                last.AddChoice(DirectoryMaster.Item("paper_towel"), LonaText.GiftHint, "", giftLabel,
                    delegate { Gift.Arm(client); });

                // 不想送礼时也必须能继续进入正常交易，否则这个唯一选项会把玩家卡在送礼流程里。
                Dialogue skipLabel = PlayerLine(LonaText.GiftSkip);
                last.AddChoice(DirectoryMaster.Item("toilet_paper"), LonaText.GiftSkip, "", skipLabel, null);
            }
            else if (visit == LonaVisit.Sell)
            {
                last.SetEndAction(delegate { AddGoods(); });
            }

            if (visit == LonaVisit.Story)
            {
                last.SetEndAction(DismissLona);
            }
        }

        /// <summary>100 好感告白：三个选项，第一次「……」再选一次，第二次「……」按拒绝。</summary>
        private static void BuildConfession(StoreClient client)
        {
            LonaLine[] lines = LonaText.Reach100Intro();
            Dialogue last = client.mainDialogue;
            for (int i = 0; i < lines.Length; i++)
            {
                string speaker = lines[i].Player ? "PLAYER" : client.displayName;
                last.SetText(speaker, lines[i].Text);
                if (i < lines.Length - 1) last = last.NextDialogue();
            }
            AddConfessionChoices(last, false);
        }

        private static void AddConfessionChoices(Dialogue node, bool second)
        {
            string agreeText = LonaText.ConfessionChoiceLabel("agree");
            string dotsText = LonaText.ConfessionChoiceLabel("dots");
            string refuseText = LonaText.ConfessionChoiceLabel("refuse");

            Dialogue agreeLabel = PlayerLine(agreeText);
            agreeLabel.nextDialogue = BuildOutcome(LonaText.ConfessionAgree());

            // 选项的目标节点会被 ContinueConversation 跳过一层，所以这里要让
            // 选项目标先指向“洛娜：……”节点，再由该节点承载第二次选项。
            Dialogue dotsLabel = PlayerLine(dotsText);
            Dialogue dotsNode = ClientLine(dotsText);
            dotsLabel.nextDialogue = dotsNode;
            if (second)
            {
                dotsNode.nextDialogue = BuildOutcome(LonaText.ConfessionRefuse());
            }
            else
            {
                AddConfessionChoices(dotsNode, true);
            }

            Dialogue refuseLabel = PlayerLine(refuseText);
            refuseLabel.nextDialogue = BuildOutcome(LonaText.ConfessionRefuse());

            node.AddChoice(DirectoryMaster.Item("paper_towel"), agreeText, "", agreeLabel, null);
            node.AddChoice(DirectoryMaster.Item("toilet_paper"), dotsText, "", dotsLabel, null);
            node.AddChoice(DirectoryMaster.Item("cup_noodle"), refuseText, "", refuseLabel, null);
        }

        private static Dialogue PlayerLine(string text)
        {
            Dialogue dialogue = new Dialogue();
            dialogue.SetText("PLAYER", text);
            return dialogue;
        }

        private static Dialogue ClientLine(string text)
        {
            Dialogue dialogue = new Dialogue();
            dialogue.SetText(LonaText.DisplayName, text);
            return dialogue;
        }

        private static Dialogue BuildOutcome(LonaLine[] lines)
        {
            Dialogue head = new Dialogue();
            Dialogue current = head;
            for (int i = 0; i < lines.Length; i++)
            {
                current.SetText(lines[i].Player ? "PLAYER" : LonaText.DisplayName, lines[i].Text);
                if (i < lines.Length - 1) current = current.NextDialogue();
            }
            current.SetEndAction(DismissLona);
            return head;
        }

        private static void GiveIceCream()
        {
            try
            {
                PlayerStore.Instance.AddDirectSellingItemToTable("beis_icecream", true);
                ModEntry.Log("好感 90 剧情：贝氏优质香草冰淇淋已放到柜台");
            }
            catch (Exception e)
            {
                ModEntry.Log("好感 90 剧情礼物失败: " + e.Message);
            }
        }

        /// <summary>把 (说话人, 文本) 串成对话链；玩家行用 PLAYER 气泡，不显示名字。</summary>
        private static Dialogue BuildStoryLines(StoreClient client, List<LonaLine> lines)
        {
            Dialogue current = client.mainDialogue;
            for (int i = 0; i < lines.Count; i++)
            {
                string speaker = lines[i].Player ? "PLAYER" : client.displayName;
                current.SetText(speaker, lines[i].Text);
                if (i < lines.Count - 1)
                {
                    current = current.NextDialogue();
                }
            }
            return current;
        }

        /// <summary>揭面：第一句后放香水，报完电话后放名片，最后离开。</summary>
        private static void BuildRevealDialogue(StoreClient client)
        {
            List<LonaLine> lines = LonaText.RevealLines();
            Dialogue current = client.mainDialogue;
            for (int i = 0; i < lines.Count; i++)
            {
                if (i == 1) current.startAction = GiveRevealPerfume;
                if (i == 4) current.startAction = GiveRevealCard;
                current.SetText(lines[i].Player ? "PLAYER" : client.displayName, lines[i].Text);
                if (i < lines.Count - 1) current = current.NextDialogue();
            }
            current.SetEndAction(DismissLona);
        }

        private static void GiveRevealPerfume()
        {
            try
            {
                PlayerStore.Instance.AddDirectSellingItemToTable("neuroactive_perfume", true);
                ModEntry.Log("揭面礼物：神经活性香水已放到柜台");
            }
            catch (Exception e)
            {
                ModEntry.Log("揭面香水失败: " + e.Message);
            }
        }

        private static void GiveRevealCard()
        {
            try
            {
                GameItem card = DirectoryMaster.Item(LonaCard.ItemId);
                if (!PlayerStore.Instance.TryAddDirectSellingItemToTable(card, true))
                {
                    Delivery.GiveToPlayer(card);
                    ModEntry.Log("柜台放不下名片，改放到背包");
                }
                Phone.EnsureContact();
                ModEntry.Log("揭面礼物：洛娜的名片已给出");
            }
            catch (Exception e)
            {
                ModEntry.Log("揭面名片失败: " + e.Message);
            }
        }

        private static void DismissLona()
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
                ModEntry.Log("洛娜离场失败: " + e.Message);
            }
        }

        /// <summary>
        /// 第一次见面：想买几样就买几样，玩家点「离开」时才说「下次见」然后走。
        /// · acceptDealDialogue 置空 —— 每次成交后不再播原版的「就这么定了 / 还有别的事吗」
        /// · tryInterceptDismiss 拦截第一次离场，播完告别台词再真正离开
        /// </summary>
        private static void ApplyVisitOverrides(StoreClient client, LonaVisit visit, int dailyStory)
        {
            if (visit == LonaVisit.FirstPurchase)
            {
                // 每成交一件都说一句「……」，替代原版的「还有什么事吗？」。
                Dialogue chatter = new Dialogue();
                chatter.SetText(client.displayName, LonaText.FirstPurchaseChatter());
                client.acceptDealDialogue = chatter;
                client.acceptDealDialoguePlayed = false;
                client.onItemBought = delegate { client.acceptDealDialoguePlayed = false; };

                client.acceptDealDialogueLastLine = null;
                client.allDoneDialogue = BuildFarewell(client);
                client.tryInterceptDismiss = delegate { return PlayFarewell(client); };
                return;
            }

            if (visit == LonaVisit.StoryBuy)
            {
                Dialogue ack = new Dialogue();
                ack.SetText(client.displayName, LonaText.DailyStoryBuyAck());
                client.acceptDealDialogue = ack;
                client.acceptDealDialoguePlayed = false;
                client.acceptDealDialogueLastLine = null;
                client.onItemBought = delegate { client.acceptDealDialoguePlayed = false; };

                ApplyClosing(client, LonaText.DailyStoryClose(dailyStory == 12 ? 12 : 1));
                return;
            }

            if (visit == LonaVisit.Buy)
            {
                ApplyClosing(client, LonaText.GenericBuyClose());
                return;
            }

            if (visit == LonaVisit.Sell)
            {
                ApplyClosing(client, LonaText.GenericSellClose());
            }
        }

        private static void ApplyClosing(StoreClient client, string line)
        {
            Dialogue closing = BuildClosing(client, line);
            client.allDoneDialogue = closing;
            client.tryInterceptDismiss = delegate { return PlayClosing(client, line); };
        }

        private static Dialogue BuildClosing(StoreClient client, string line)
        {
            Dialogue closing = new Dialogue();
            closing.SetText(client.displayName, line);
            closing.SetEndAction(DismissLona);
            return closing;
        }

        private static bool PlayClosing(StoreClient client, string line)
        {
            try
            {
                if (DialogUIManager.Instance == null) return false;
                return DialogUIManager.Instance.StartDialogue(BuildClosing(client, line));
            }
            catch (Exception e)
            {
                ModEntry.Log("收尾台词失败: " + e.Message);
                return false;
            }
        }

        private static Dialogue BuildFarewell(StoreClient client)
        {
            Dialogue farewell = new Dialogue();
            farewell.SetText(client.displayName, LonaText.FirstPurchaseFarewell());
            farewell.SetEndAction(DismissLona);
            return farewell;
        }

        /// <summary>点离开时先播「下次见」，返回 true 取消这次离场；台词播完再由 DismissLona 走。</summary>
        private static bool PlayFarewell(StoreClient client)
        {
            try
            {
                if (DialogUIManager.Instance == null) return false;
                return DialogUIManager.Instance.StartDialogue(BuildFarewell(client));
            }
            catch (Exception e)
            {
                ModEntry.Log("告别台词失败: " + e.Message);
                return false;
            }
        }

        // ---------------------------------------------------------------
        // 卖货：按展示柜里的霓虹灯牌决定品类
        // ---------------------------------------------------------------
        private static void AddGoods()
        {
            if (!State.Ready) return;

            List<string> pool = new List<string>();
            AddIfSign("sign_food", FoodItems, pool);
            AddIfSign("sign_water", WaterItems, pool);
            AddIfSign("sign_medical", MedicalItems, pool);
            AddIfSign("sign_household", HouseholdItems, pool);
            AddIfSign("sign_material", MaterialItems, pool);

            if (pool.Count == 0)
            {
                pool.AddRange(DefaultItems);
            }

            int count = RNG.GetRandomInt(1, 3);
            for (int i = 0; i < count; i++)
            {
                string id = pool[RNG.GetRandomInt(0, pool.Count - 1)];
                PlayerStore.Instance.AddDirectSellingItemToTable(id);
            }
            ModEntry.Log("洛娜上货 " + count + " 件，灯牌品类 " + pool.Count);
        }

        private static void AddIfSign(string signItemId, string[] items, List<string> pool)
        {
            try
            {
                if (ShowcaseHelper.IsShowcaseContainItemById(signItemId))
                {
                    pool.AddRange(items);
                }
            }
            catch (Exception e)
            {
                ModEntry.Log("读取展示柜失败: " + e.Message);
            }
        }
    }
}
