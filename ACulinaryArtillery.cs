﻿using ACulinaryArtillery.Util;
using HarmonyLib;
using System;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace ACulinaryArtillery
{
    public class ACulinaryArtillery : ModSystem
    {
        private static Harmony harmony;
        public static ILogger logger;

        public override void Start(ICoreAPI api)
        {
            //base.Start(api);

            api.RegisterBlockClass("BlockMeatHooks", typeof(BlockMeatHooks));
            api.RegisterBlockEntityClass("MeatHooks", typeof(BlockEntityMeatHooks));

            api.RegisterBlockClass("BlockBottleRack", typeof(BlockBottleRack));
            api.RegisterBlockEntityClass("BottleRack", typeof(BlockEntityBottleRack));

            api.RegisterBlockClass("BlockMixingBowl", typeof(BlockMixingBowl));
            api.RegisterBlockEntityClass("MixingBowl", typeof(BlockEntityMixingBowl));

            api.RegisterBlockClass("BlockBottle", typeof(BlockBottle));
            api.RegisterBlockEntityClass("Bottle", typeof(BlockEntityBottle));

            api.RegisterBlockClass("BlockSpile", typeof(BlockSpile));
            api.RegisterBlockEntityClass("Spile", typeof(BlockEntitySpile));

            api.RegisterBlockClass("BlockSaucepan", typeof(BlockSaucepan));
            api.RegisterBlockEntityClass("Saucepan", typeof(BlockEntitySaucepan));

            api.RegisterBlockEntityClass("ExpandedOven", typeof(BlockEntityExpandedOven));
            api.RegisterItemClass("ExpandedRawFood", typeof(ItemExpandedRawFood));
            api.RegisterItemClass("ExpandedFood", typeof(ItemExpandedFood));
            api.RegisterItemClass("ExpandedLiquid", typeof(ItemExpandedLiquid));
            api.RegisterItemClass("ExpandedDough", typeof(ItemExpandedDough));

            api.RegisterCollectibleBehaviorClass("EggCrack", typeof(CollectibleBehaviorEggCrack));

            //Check for Existing Config file, create one if none exists
            try
            {
                var Config = api.LoadModConfig<ACulinaryArtilleryConfig>("aculinaryartillery.json");
                if (Config != null)
                {
                    api.Logger.Notification("Mod Config successfully loaded.");
                    ACulinaryArtilleryConfig.Current = Config;
                }
                else
                {
                    api.Logger.Notification("No Mod Config specified. Falling back to default settings");
                    ACulinaryArtilleryConfig.Current = ACulinaryArtilleryConfig.GetDefault();
                }
            }
            catch
            {
                ACulinaryArtilleryConfig.Current = ACulinaryArtilleryConfig.GetDefault();
                api.Logger.Error("Failed to load custom mod configuration. Falling back to default settings!");
            }
            finally
            {
                api.StoreModConfig(ACulinaryArtilleryConfig.Current, "aculinaryartillery.json");
            }

            logger = api.Logger;

            if (harmony is null)
            {
                harmony = new Harmony("com.jakecool19.efrecipes.cookingoverhaul");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
            }

        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            var meatHookTransformConfig = new TransformConfig
            {
                AttributeName = "meatHookTransform",
                Title = "On Meathook"
            };
            GuiDialogTransformEditor.extraTransforms.Add(meatHookTransformConfig);
        }

        public override void Dispose()
        {
            logger.Debug("Unpatching harmony methods");
            harmony.UnpatchAll(harmony.Id);
            harmony = null;
        }

        internal static void LogError(string message)
        {
            logger?.Error("(ACulinaryArtillery): {0}", message);
        }
    }
}