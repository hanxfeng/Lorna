using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdmirerLona
{
    /// <summary>电话里可选的一个送货大类。基础五类有低/中/高档，特殊类只有一套池子。</summary>
    internal sealed class LonaDeliveryCategory
    {
        public readonly string Id;
        public readonly string NameKey;
        public readonly string NameFallback;
        public readonly bool Special;

        private readonly Func<int, List<Func<GameItem>>> poolFactory;
        private readonly Func<bool> unlocked;

        public LonaDeliveryCategory(string id, string nameKey, string nameFallback, bool special,
            Func<int, List<Func<GameItem>>> poolFactory, Func<bool> unlocked = null)
        {
            Id = id;
            NameKey = nameKey;
            NameFallback = nameFallback;
            Special = special;
            this.poolFactory = poolFactory;
            this.unlocked = unlocked;
        }

        public string Name
        {
            get { return LonaText.CategoryName(NameKey, NameFallback); }
        }

        public bool Unlocked
        {
            get { return unlocked == null || unlocked(); }
        }

        public int CooldownDays
        {
            get { return Special ? State.SpecialDeliveryDaysValue : State.NormalDeliveryDaysValue; }
        }

        public List<Func<GameItem>> Pool(int tier)
        {
            return poolFactory(Special ? 0 : tier);
        }
    }

    /// <summary>洛娜电话送货：物品池、档位、预约和到店。</summary>
    internal static class Delivery
    {
        private static List<LonaDeliveryCategory> all;

        public static List<LonaDeliveryCategory> All
        {
            get
            {
                if (all == null) BuildCategories();
                return all;
            }
        }

        public static List<LonaDeliveryCategory> BaseCategories()
        {
            List<LonaDeliveryCategory> list = new List<LonaDeliveryCategory>();
            foreach (LonaDeliveryCategory category in All)
            {
                if (!category.Special) list.Add(category);
            }
            return list;
        }

        public static List<LonaDeliveryCategory> UnlockedSpecialCategories()
        {
            List<LonaDeliveryCategory> list = new List<LonaDeliveryCategory>();
            foreach (LonaDeliveryCategory category in All)
            {
                if (category.Special && category.Unlocked) list.Add(category);
            }
            return list;
        }

        public static LonaDeliveryCategory Find(string id)
        {
            foreach (LonaDeliveryCategory category in All)
            {
                if (category.Id == id) return category;
            }
            return null;
        }

        /// <summary>上层区声望 &lt;70 低档，70–139 中档；140 且好感满才高档。</summary>
        public static int CurrentTier()
        {
            int upper = StoreReputation.GetULReputation();
            if (upper < 70) return 0;
            if (upper >= 140 && State.AffectionInt >= 100) return 2;
            return 1;
        }

        public static int CountForAffection()
        {
            int affection = State.AffectionInt;
            if (affection >= 90) return 10;
            if (affection >= 80) return 8;
            return 6;
        }

        public static bool FreeForAffection()
        {
            return State.AffectionInt >= 100;
        }

        public static List<GameItem> Build(string categoryId, int tier, int count, int budget)
        {
            List<GameItem> result = new List<GameItem>();
            List<long> values = new List<long>();
            LonaDeliveryCategory category = Find(categoryId);
            if (category == null || count <= 0) return result;

            // 酿酒是特殊职业送货：固定保证酒瓶至少 2/3，其他物品最多 1/3。
            if (category.Id == "brewery")
            {
                return BuildBrewery(count);
            }

            List<Func<GameItem>> pool = category.Pool(tier);
            if (pool == null || pool.Count == 0)
            {
                ModEntry.Log("送货池为空：" + categoryId + " tier=" + tier);
                return result;
            }

            List<int> bag = new List<int>();
            for (int i = 0; i < count; i++)
            {
                if (bag.Count == 0)
                {
                    for (int j = 0; j < pool.Count; j++) bag.Add(j);
                }
                int pick = RNG.GetRandomInt(0, bag.Count - 1);
                int index = bag[pick];
                bag.RemoveAt(pick);
                try
                {
                    GameItem item = pool[index]();
                    if (item != null)
                    {
                        result.Add(item);
                        values.Add(Math.Max(0L, item.GetValue(null, null)));
                    }
                }
                catch (Exception e)
                {
                    ModEntry.Log("送货物品生成失败: " + e.Message);
                }
            }

            long totalValue = 0L;
            for (int i = 0; i < values.Count; i++) totalValue += values[i];

            int drawnCount = result.Count;
            if (totalValue > budget)
            {
                result = TrimToBudget(result, values, budget);
            }

            long keptValue = 0L;
            for (int i = 0; i < result.Count; i++)
            {
                GameItem item = result[i];
                keptValue += Math.Max(0L, item.GetValue(null, null));
            }

            ModEntry.Log("送货池：" + categoryId + " tier=" + tier + " 抽取=" + drawnCount
                         + " 原价值=" + totalValue + " 保留=" + result.Count + " 价值=" + keptValue
                         + " 上限=" + budget + " pool=" + pool.Count);
            return result;
        }

        /// <summary>
        /// 酿酒送货：酒瓶至少 2/3，其他物品最多 1/3。
        /// 6 件 → 4 酒瓶 + 2 其他；8 件 → 6 + 2；10 件 → 7 + 3。
        /// 现有酿酒物品总价值低于默认 1000 下限，不做价值裁剪，避免比例被删坏。
        /// </summary>
        private static List<GameItem> BuildBrewery(int count)
        {
            List<GameItem> result = new List<GameItem>();
            int bottleCount = Mathf.CeilToInt(count * 2f / 3f);
            for (int i = 0; i < bottleCount; i++)
            {
                GameItem bottle = DirectoryMaster.Item("wine_bottle");
                if (bottle != null) result.Add(bottle);
            }

            string[] others =
            {
                "wine_yeast_red", "wine_yeast", "wine_superyeast",
                "wine_berry", "wine_yeast_infinite", "water_ration"
            };
            List<int> bag = new List<int>();
            int otherCount = count - result.Count;
            for (int i = 0; i < otherCount; i++)
            {
                if (bag.Count == 0)
                {
                    for (int j = 0; j < others.Length; j++) bag.Add(j);
                }
                int pick = RNG.GetRandomInt(0, bag.Count - 1);
                int index = bag[pick];
                bag.RemoveAt(pick);
                GameItem item = DirectoryMaster.Item(others[index]);
                if (item != null) result.Add(item);
            }

            // 打乱顺序，避免酒瓶全排在最前面。
            for (int i = result.Count - 1; i > 0; i--)
            {
                int j = RNG.GetRandomInt(0, i);
                GameItem tmp = result[i];
                result[i] = result[j];
                result[j] = tmp;
            }

            long totalValue = 0L;
            for (int i = 0; i < result.Count; i++)
            {
                totalValue += Math.Max(0L, result[i].GetValue(null, null));
            }
            ModEntry.Log("酿酒送货：件数=" + count + " 酒瓶=" + bottleCount
                         + " 其他=" + (result.Count - bottleCount) + " 价值=" + totalValue);
            return result;
        }

        /// <summary>
        /// 件数固定后，若总价值超上限，就从抽取结果里挑一个子集，
        /// 让保留物品的总价值尽量高但不超过上限；价值相同时保留更多件。
        /// 件数最多 10，直接枚举 2^n 种子集即可。
        /// </summary>
        private static List<GameItem> TrimToBudget(List<GameItem> items, List<long> values, long budget)
        {
            int n = items.Count;
            if (n <= 0) return items;
            if (n > 20) return items; // 正常不会到这里；避免位移溢出

            int bestMask = 0;
            long bestSum = 0L;
            int bestCount = -1;
            int totalMasks = 1 << n;
            for (int mask = 1; mask < totalMasks; mask++)
            {
                long sum = 0L;
                int selected = 0;
                bool over = false;
                for (int i = 0; i < n; i++)
                {
                    if ((mask & (1 << i)) == 0) continue;
                    sum += values[i];
                    selected++;
                    if (sum > budget)
                    {
                        over = true;
                        break;
                    }
                }
                if (over) continue;
                if (sum > bestSum || (sum == bestSum && selected > bestCount))
                {
                    bestSum = sum;
                    bestCount = selected;
                    bestMask = mask;
                }
            }

            List<GameItem> kept = new List<GameItem>();
            for (int i = 0; i < n; i++)
            {
                if ((bestMask & (1 << i)) != 0) kept.Add(items[i]);
            }
            if (kept.Count == 0)
            {
                ModEntry.Log("送货价值上限低于任何单件物品，本次没有保留物品");
            }
            return kept;
        }

        /// <summary>100 好感免费送货、名片等：直接进玩家背包（物品归玩家所有）。</summary>
        public static void GiveToPlayer(GameItem item)
        {
            if (item == null) return;
            try
            {
                GeneralHelper.SetItemOwned(item, true);
                if (EmporiumEntry.Instance != null && EmporiumEntry.Instance.invElement != null)
                {
                    GraphUtils.EmporiumTryAdd(EmporiumEntry.Instance.invElement, item);
                }
                else if (PlayerStore.Instance != null)
                {
                    PlayerStore.Instance.AddDirectToWeightedTable(item, true);
                }
            }
            catch (Exception e)
            {
                ModEntry.Log("物品放入背包失败: " + e.Message);
            }
        }

        // ---------------------------------------------------------------
        // 池子
        // ---------------------------------------------------------------
        private static void BuildCategories()
        {
            all = new List<LonaDeliveryCategory>
            {
                new LonaDeliveryCategory("material", "lona_cat_material", "材料", false,
                    tier => tier == 0 ? Ids("flux_agent", "scrap_metal", "printer_plastic", "nuts_metal")
                        : tier == 1 ? Ids("common_ore", "wire", "gun_part", "rare_ore")
                        : Ids("kotton_fabric", "metal_ingot", "kotton_fiber", "common_electronic")),

                new LonaDeliveryCategory("tool", "lona_cat_tool", "工具", false,
                    tier => Ids("module_extractor_advanced", "module_extractor", "welder", "crowbar",
                        "hatchet", "wire_cutter", "flashlight", "turbo_booster", "screwdriver")),

                new LonaDeliveryCategory("household", "lona_cat_household", "日用品", false,
                    tier => tier == 0 ? Ids("bottled_water", "small_bottled_water", "water_test_strip",
                            "mini_bottle", "water_tablet_container", "water_test_container")
                        : tier == 1 ? Ids("toilet_paper", "water_pitcher", "toothpaste", "wine_yeast",
                            "large_bottled_water", "water_tablet")
                        : Ids("uv_filter", "box_tampon", "water_filter", "paper_towel", "pack_condom", "shampoo")),

                new LonaDeliveryCategory("food", "lona_cat_food", "食物", false,
                    tier => tier == 0 ? Ids("soda_red", "cat_bar", "processed_juice", "nutrifruit",
                            "zerochew", "galaxy_blend")
                        : tier == 1 ? Ids("processed_cheese", "cup_noodle", "energy_drink",
                            "li_eat_snackbar", "processed_milk", "processed_meat")
                        : Ids("nudka", "small_raw_meat", "fat_meat", "beis_icecream", "processed_meat")),

                new LonaDeliveryCategory("medical", "lona_cat_medical", "医疗品", false,
                    tier => tier == 0 ? Ids("hemostatic_bandage_item", "smelling_salt", "birth_control_pill",
                            "caffeine_pill", "bandage_item")
                        : tier == 1 ? Ids("common_medical", "rubbing_alcohol", "blood_bag",
                            "pure_white_injector", "topical_bandage_item")
                        : Ids("black_injector", "blue_blood_bag", "surgery_tool",
                            "large_purple_injector", "salve", "pink_injector")),

                new LonaDeliveryCategory("farm", "lona_cat_farm", "水培农场", true,
                    tier => Ids("bloomberry_seed", "nutrifruit_seed", "nutrifruit_seed_packet",
                        "fillerweed_seed", "kotton_seed", "nutrient_tablet_container", "dream_cap_seed",
                        "hydroreed_seed"),
                    () => StartType(NewGameData.StartType.FARMER) || Upgrade("RETIRED_FARMER")),

                new LonaDeliveryCategory("aquaculture", "lona_cat_aquaculture", "水产养殖", true,
                    tier =>
                    {
                        List<Func<GameItem>> pool = new List<Func<GameItem>>
                        {
                            delegate { return AquacultureItems.TilapiaRoe(); },
                            delegate { return AquacultureItems.GutterTilapiaRoe(); },
                            delegate { return AquacultureItems.GraftTilapiaRoe(); }
                        };
                        pool.AddRange(Ids("industrial_fish_feed", "gutterflow_pellet", "ras"));
                        if (Upgrade("AQUACULTURE_II"))
                        {
                            pool.Add(Fish(FishSpecies.Eel));
                            pool.Add(Fish(FishSpecies.ElectricEel));
                            pool.Add(Fish(FishSpecies.Sturgeon));
                        }
                        return pool;
                    },
                    () => StartType(NewGameData.StartType.AQUAFARMER) || Upgrade("AQUACULTURE")),

                new LonaDeliveryCategory("husbandry", "lona_cat_husbandry", "养殖", true,
                    tier => Ids("rat", "serum_green", "serum_red", "kitchen_cleaver",
                        "livestock_feed_dispenser", "animal_cage"),
                    () => StartType(NewGameData.StartType.RANCHER) || Upgrade("RETIRED_RANCHER")),

                new LonaDeliveryCategory("brewery", "lona_cat_brewery", "酿酒", true,
                    tier => Ids("wine_yeast_red", "wine_yeast_red", "wine_yeast", "wine_yeast",
                        "wine_superyeast", "wine_superyeast", "wine_bottle", "wine_bottle",
                        "wine_berry", "wine_berry", "wine_yeast_infinite", "water_ration", "water_ration"),
                    () => Upgrade("WINEMAKER")),

                new LonaDeliveryCategory("water", "lona_cat_water", "水商", true,
                    tier => WaterPool(),
                    () => StartType(NewGameData.StartType.WATER_MERCHANT) || Upgrade("WATER_TRADER"))
            };
        }

        private static List<Func<GameItem>> WaterPool()
        {
            return new List<Func<GameItem>>
            {
                Water("bottled_water"),
                Water("small_bottled_water"),
                Water("large_bottled_water"),
                Water("mini_bottle"),
                IdString("water_ration"),
                IdString("water_ration_small"),
                IdString("water_filter_adv"),
                IdString("water_purifier"),
                IdString("moisture_farm"),
                IdString("water_pitcher"),
                IdString("water_filter")
            };
        }

        private static List<Func<GameItem>> Ids(params string[] ids)
        {
            List<Func<GameItem>> list = new List<Func<GameItem>>();
            foreach (string id in ids) list.Add(IdString(id));
            return list;
        }

        private static Func<GameItem> IdString(string id)
        {
            return delegate { return DirectoryMaster.Item(id); };
        }

        private static Func<GameItem> Water(string size)
        {
            return delegate { return WaterPremadeHelper.GetRandomWater(size); };
        }

        private static Func<GameItem> Fish(FishSpecies species)
        {
            return delegate
            {
                string sex = RNG.Roll(50) ? "MALE" : "FEMALE";
                return AquacultureItems.SpawnFish(species, sex, 0);
            };
        }

        // ---------------------------------------------------------------
        // 解锁判断
        // ---------------------------------------------------------------
        private static bool Upgrade(string id)
        {
            if (PlayerStore.Instance == null || PlayerStore.Instance.networkUpgrade == null) return false;
            NetworkUpgrade upgrade;
            return PlayerStore.Instance.networkUpgrade.TryGetValue(id, out upgrade)
                   && upgrade != null && upgrade.IsUnlocked();
        }

        private static bool StartType(NewGameData.StartType type)
        {
            return PlayerStore.Instance != null && PlayerStore.Instance.startType == type;
        }
    }
}
