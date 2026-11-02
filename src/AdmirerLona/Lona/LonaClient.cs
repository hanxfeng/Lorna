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
        Reveal
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
            StoreClient client = StoreClientList.CreateThief();
            if (Portrait.Has(Portrait.KeyThief))
            {
                client.spriteName = Portrait.KeyThief;
            }
            client.sourceModId = "";
            return client;
        }

        // ---------------------------------------------------------------
        // 150 声望之后
        // ---------------------------------------------------------------
        public static StoreClient Create(LonaVisit visit, LonaStory story, int dailyTier = -1)
        {
            StoreClient client = new StoreClient();
            client.identifier = LonaId;
            client.displayName = LonaText.DisplayName;
            client.clientFaction = "FACTION_LOWER_LEVEL";
            StoreClientFactionSetup.InitLowerLevelCitizen(client);
            client.spriteName = Portrait.Has(Portrait.KeyLona)
                ? Portrait.KeyLona
                : SpriteDict.GetRandomLowerLevelSpriteName();
            client.dismissable = true;
            client.arrestable = true;
            client.cooldown = 1;
            // 举报 = 永久离开（与卖四次芬太片/梦尘相同）
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
                default:
                    client.clientIntent = StoreClient.ClientIntent.DIALOGUE;
                    break;
            }

            BuildDialogue(client, visit, story, dailyTier);
            client.AddBasicDialog();
            client.CompleteClientCreation();
            ApplyVisitOverrides(client, visit);
            client.sourceModId = "";
            return client;
        }

        private static void SetupBuyer(StoreClient client)
        {
            client.clientIntent = StoreClient.ClientIntent.BUY;
            client.clientBuyingTagList.Add("HOUSEHOLD_GOOD");
            client.clientBuyingTagList.Add("FOOD");
            client.clientBuyingTagList.Add("MEDICAL");
            client.SetBudget(120 + Mathf.Clamp(State.Reputation, 0, 200) * 2);
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
                return item != null && item.IsTag("FOOD");
            };
        }

        /// <summary>收购：日用品/食物/医疗，拒绝麻醉品，但接受奥克西莫注射器。</summary>
        private static bool AcceptsAsBuyer(GameItem item)
        {
            if (item == null) return false;
            if (item.IsTag("NARCOTIC"))
            {
                return item.identifier == "pink_injector";
            }
            return item.IsTag("HOUSEHOLD_GOOD") || item.IsTag("FOOD") || item.IsTag("MEDICAL");
        }

        // ---------------------------------------------------------------
        // 对话
        // ---------------------------------------------------------------
        private static void BuildDialogue(StoreClient client, LonaVisit visit, LonaStory story, int dailyTier)
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

            List<string> lines = new List<string>();

            if (visit == LonaVisit.Story)
            {
                lines.AddRange(LonaText.StoryLines(story));
            }
            else
            {
                int tier = dailyTier >= 0 ? dailyTier : State.DailyTier;
                if (tier >= 0)
                {
                    lines.Add(LonaText.DailyLine(tier, RNG.GetRandomInt(0, LonaText.DailyLineCount(tier) - 1)));
                }
                else
                {
                    lines.AddRange(LonaText.ColdGreeting);
                }

                if (visit == LonaVisit.Buy)
                {
                    lines.Add("【来买占位】我看看你今天有什么。");
                }
                else
                {
                    lines.Add("【来卖占位】今天的货，你看看。");
                }
            }

            Dialogue last = client.mainDialogue;
            for (int i = 0; i < lines.Count; i++)
            {
                last.SetText(client.displayName, lines[i]);
                if (i < lines.Count - 1)
                {
                    last = last.NextDialogue();
                }
            }

            if (visit == LonaVisit.Buy)
            {
                Dialogue afterGift = new Dialogue();
                afterGift.SetText(client.displayName, LonaText.GiftHint);
                last.AddChoice(DirectoryMaster.Item("$"), LonaText.GiftOffer, "", afterGift,
                    delegate { Gift.Arm(client); });
            }
            else if (visit == LonaVisit.Sell)
            {
                last.SetEndAction(delegate { AddGoods(); });
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

        /// <summary>揭面：说完第一句后亲手把礼物放到柜台上，最后离开。</summary>
        private static void BuildRevealDialogue(StoreClient client)
        {
            List<LonaLine> lines = LonaText.RevealLines();
            Dialogue head = client.mainDialogue;
            head.SetText(client.displayName, lines[0].Text);

            Dialogue last = head;
            for (int i = 1; i < lines.Count; i++)
            {
                last = last.NextDialogue();
                last.SetText(lines[i].Player ? "PLAYER" : client.displayName, lines[i].Text);
            }

            if (head.nextDialogue != null)
            {
                head.nextDialogue.startAction = GiveRevealGift;
            }
            last.SetEndAction(DismissLona);
        }

        private static void GiveRevealGift()
        {
            try
            {
                PlayerStore.Instance.AddDirectSellingItemToTable("neuroactive_perfume", true);
                ModEntry.Log("揭面礼物已放到柜台：neuroactive_perfume");
            }
            catch (Exception e)
            {
                ModEntry.Log("揭面礼物失败: " + e.Message);
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
        private static void ApplyVisitOverrides(StoreClient client, LonaVisit visit)
        {
            if (visit != LonaVisit.FirstPurchase) return;

            // 每成交一件都说一句「……」，替代原版的「还有什么事吗？」。
            // 关键：这个对话是在 CompleteClientCreation 之后挂上的，没有接 OnDealAccepted，
            // 所以不会再把原版收尾对话带出来；成交时把 played 标志重置，下一件继续播。
            Dialogue chatter = new Dialogue();
            chatter.SetText(client.displayName, LonaText.FirstPurchaseChatter());
            client.acceptDealDialogue = chatter;
            client.acceptDealDialoguePlayed = false;
            client.onItemBought = delegate { client.acceptDealDialoguePlayed = false; };

            client.acceptDealDialogueLastLine = null;
            client.allDoneDialogue = BuildFarewell(client);
            client.tryInterceptDismiss = delegate { return PlayFarewell(client); };
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
