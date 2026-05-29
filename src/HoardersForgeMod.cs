using System;
using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace HoardersForge
{
    public class HoardersForgeMod : ModSystem
    {
        private static readonly object stateLock = new object();
        private static int activeInstances = 0;
        private static Harmony harmony;
        private ICoreAPI api;
        public static ICoreAPI InstanceApi;

        public static HoardersForgeConfig CurrentConfig { get; set; } = new HoardersForgeConfig();
        private static GuiDialogHoardersForgeConfig configGui;

        public override void Start(ICoreAPI api)
        {
            this.api = api;
            InstanceApi = api;
            lock (stateLock)
            {
                activeInstances++;
                if (harmony == null)
                {
                    harmony = new Harmony("hoardersforge");
                    harmony.PatchAll();
                    api.Logger.Notification("[HoardersForge] Harmony patches applied successfully (instance count: {0}).", activeInstances);
                }
                else
                {
                    api.Logger.Notification("[HoardersForge] Harmony patches already applied (instance count: {0}).", activeInstances);
                }
            }

            // Load Config
            try
            {
                CurrentConfig = api.LoadModConfig<HoardersForgeConfig>("HoardersForgeConfig.json");
            }
            catch { CurrentConfig = null; }

            if (CurrentConfig == null)
            {
                CurrentConfig = new HoardersForgeConfig();
                api.StoreModConfig(CurrentConfig, "HoardersForgeConfig.json");
            }
        }

        private Vintagestory.API.Client.ICoreClientAPI capi;

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            // Register network channel
            api.Network.RegisterChannel("hoardersforgeconfig")
                .RegisterMessageType<ConfigSyncPacket>()
                .RegisterMessageType<OpenGuiPacket>()
                .RegisterMessageType<RunTestsPacket>()
                .SetMessageHandler<ConfigSyncPacket>((player, packet) => OnServerReceiveConfigPacket(api, player, packet))
                .SetMessageHandler<RunTestsPacket>((player, packet) => OnServerReceiveRunTestsPacket(api, player, packet));

            // Sync config on join
            api.Event.PlayerJoin += (player) =>
            {
                var packet = new ConfigSyncPacket
                {
                    DebugLogging = CurrentConfig.DebugLogging,
                    LossPercentage = CurrentConfig.LossPercentage
                };
                api.Network.GetChannel("hoardersforgeconfig").SendPacket(packet, player);
            };

            // Register /hoardersforge server command with gui and test subcommands
            api.ChatCommands.Create("hoardersforge")
                .WithDescription("Commands for Hoarder's Forge mod")
                .RequiresPrivilege(Privilege.controlserver)
                .BeginSubCommand("gui")
                    .WithDescription("Open the configuration GUI dialog")
                    .HandleWith((args) =>
                    {
                        if (args.Caller?.Player is IServerPlayer serverPlayer)
                        {
                            api.Network.GetChannel("hoardersforgeconfig").SendPacket(new OpenGuiPacket(), serverPlayer);
                        }
                        return TextCommandResult.Success();
                    })
                .EndSubCommand()
                .BeginSubCommand("test")
                    .WithDescription("Runs integration tests for Hoarder's Forge")
                    .HandleWith((args) =>
                    {
                        string message = ExecuteIntegrationTests(api);
                        return TextCommandResult.Success(message);
                    })
                .EndSubCommand();
        }

        public override void StartClientSide(Vintagestory.API.Client.ICoreClientAPI api)
        {
            base.StartClientSide(api);
            this.capi = api;

            // Register network channel
            api.Network.RegisterChannel("hoardersforgeconfig")
                .RegisterMessageType<ConfigSyncPacket>()
                .RegisterMessageType<OpenGuiPacket>()
                .RegisterMessageType<RunTestsPacket>()
                .SetMessageHandler<ConfigSyncPacket>(OnClientReceiveConfigPacket)
                .SetMessageHandler<OpenGuiPacket>(OnClientReceiveOpenGuiPacket);

            // Register hotkey Ctrl+H
            api.Input.RegisterHotKey("hoardersforgeconfiggui", "Open Hoarder's Forge Config GUI", Vintagestory.API.Client.GlKeys.H, Vintagestory.API.Client.HotkeyType.CharacterControls, ctrlPressed: true);
            api.Input.SetHotKeyHandler("hoardersforgeconfiggui", (keyEvent) =>
            {
                OpenConfigGui(api);
                return true;
            });

            // Register client-side /hoardersforge command
            api.ChatCommands.Create("hoardersforge")
                .WithDescription("Commands for Hoarder's Forge mod")
                .BeginSubCommand("gui")
                    .WithDescription("Open the configuration GUI dialog")
                    .HandleWith((args) =>
                    {
                        OpenConfigGui(api);
                        return TextCommandResult.Success();
                    })
                .EndSubCommand()
                .BeginSubCommand("test")
                    .WithDescription("Runs integration tests for Hoarder's Forge")
                    .HandleWith((args) =>
                    {
                        api.Network.GetChannel("hoardersforgeconfig").SendPacket(new RunTestsPacket());
                        return TextCommandResult.Success();
                    })
                .EndSubCommand();
        }

        private void OnClientReceiveConfigPacket(ConfigSyncPacket packet)
        {
            if (packet == null) return;
            CurrentConfig.DebugLogging = packet.DebugLogging;
            CurrentConfig.LossPercentage = packet.LossPercentage;
            if (CurrentConfig.DebugLogging)
            {
                api.Logger.Notification("[HoardersForge] Configuration synced from server: Loss = {0}%, DebugLogs = {1}", CurrentConfig.LossPercentage, CurrentConfig.DebugLogging);
            }
        }

        private void OnClientReceiveOpenGuiPacket(OpenGuiPacket packet)
        {
            if (capi != null)
            {
                capi.Event.EnqueueMainThreadTask(() =>
                {
                    OpenConfigGui(capi);
                }, "openhoardersforgegui");
            }
        }

        private void OnServerReceiveConfigPacket(ICoreServerAPI api, IServerPlayer player, ConfigSyncPacket packet)
        {
            if (packet == null || player == null) return;

            // Check privileges
            if (!player.HasPrivilege(Privilege.controlserver))
            {
                api.SendMessage(player, 0, "You do not have permission to modify Hoarder's Forge settings.", EnumChatType.CommandError);
                return;
            }

            // Clamp values
            double loss = packet.LossPercentage;
            if (loss < 0.0) loss = 0.0;
            if (loss > 100.0) loss = 100.0;

            CurrentConfig.DebugLogging = packet.DebugLogging;
            CurrentConfig.LossPercentage = loss;

            // Save config
            api.StoreModConfig(CurrentConfig, "HoardersForgeConfig.json");

            if (CurrentConfig.DebugLogging)
            {
                api.Logger.Notification("[HoardersForge] Configuration updated by {0}: Loss = {1}%, DebugLogs = {2}", player.PlayerName, CurrentConfig.LossPercentage, CurrentConfig.DebugLogging);
            }

            // Broadcast new config to all clients
            var syncPacket = new ConfigSyncPacket
            {
                DebugLogging = CurrentConfig.DebugLogging,
                LossPercentage = CurrentConfig.LossPercentage
            };
            api.Network.GetChannel("hoardersforgeconfig").BroadcastPacket(syncPacket);
        }

        private void OnServerReceiveRunTestsPacket(ICoreServerAPI api, IServerPlayer player, RunTestsPacket packet)
        {
            if (player == null) return;

            // Check privileges
            if (!player.HasPrivilege(Privilege.controlserver))
            {
                api.SendMessage(player, 0, "You do not have permission to run integration tests.", EnumChatType.CommandError);
                return;
            }

            string results = ExecuteIntegrationTests(api);
            api.SendMessage(player, 0, results, EnumChatType.CommandSuccess);
        }

        private void OpenConfigGui(Vintagestory.API.Client.ICoreClientAPI api)
        {
            if (configGui == null)
            {
                configGui = new GuiDialogHoardersForgeConfig(api, CurrentConfig);
            }
            if (configGui.IsOpened())
            {
                configGui.TryClose();
            }
            else
            {
                configGui.TryOpen();
            }
        }

        private string ExecuteIntegrationTests(ICoreAPI api)
        {
            var sb = new System.Text.StringBuilder();
            double currentLoss = CurrentConfig?.LossPercentage ?? 5.0;
            sb.AppendLine($"=== Hoarder's Forge - Integration Tests (Loss: {currentLoss}%) ===");

            bool hasSmithingPlus = api.ModLoader.IsModEnabled("smithingplus");
            sb.AppendLine($"SmithingPlus Active: {hasSmithingPlus}");

            string[] testItems = {
                "game:pickaxe-copper",
                "game:chutesection-copper",
                "game:metalnailsandstrips-copper",
                "game:padlock-tinbronze",
                "game:arrowhead-copper",
                "game:metalplate-copper"
            };

            foreach (var code in testItems)
            {
                var assetLoc = new AssetLocation(code);
                var coll = api.World.GetItem(assetLoc) as CollectibleObject ?? api.World.GetBlock(assetLoc) as CollectibleObject;
                if (coll == null)
                {
                    sb.AppendLine($"[SKIP] {code} - Not found");
                    continue;
                }

                bool isSmithed = IsSmithedItem(coll, coll.Code.Path);
                var props = coll.GetCombustibleProperties(null, null, null);
                bool isMeltable = props != null && props.SmeltedStack != null;

                double baseUnits = GetFinishedToolBaseUnits(coll.Code.Path);
                double expectedYield = isSmithed ? ForgeMath.CalculateDurabilityYield(baseUnits, 1.0, currentLoss) : baseUnits;

                string status = (isSmithed && isMeltable) ? "PASS" : "FAIL";
                sb.AppendLine($"[{status}] {coll.Code.Path} | Smithed: {isSmithed}, Meltable: {isMeltable} | Base: {baseUnits}u | Expected Yield (pristine): {expectedYield}u");
            }

            string message = sb.ToString();
            api.Logger.Notification("[HoardersForge] Test results:\n" + message);
            return message;
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            base.AssetsFinalize(api);
            RegisterMeltableProperties(api);
        }

        private void RegisterMeltableProperties(ICoreAPI api)
        {
            var meltingPoints = new Dictionary<string, int>
            {
                { "copper", 1084 },
                { "tinbronze", 950 },
                { "bismuthbronze", 850 },
                { "blackbronze", 1020 },
                { "gold", 1063 },
                { "silver", 961 },
                { "brass", 920 },
                { "lead", 327 },
                { "tin", 232 },
                { "zinc", 419 },
                { "bismuth", 271 }
            };

            foreach (var collObj in api.World.Collectibles)
            {
                if (collObj == null || collObj.Code == null) continue;

                string path = collObj.Code.Path;

                // Identify if it's a tool, tool head, arrowhead, workitem, or dynamically resolved smithed item
                bool isTarget = path.StartsWith("workitem-") || HoardersForgeMod.IsSmithedItem(collObj, path);

                if (!isTarget) continue;

                // Determine the metal type
                string matchedMetal = null;
                foreach (var metal in meltingPoints.Keys)
                {
                    if (path.EndsWith("-" + metal) || path.Contains("-" + metal + "-") || path == "workitem-" + metal)
                    {
                        matchedMetal = metal;
                        break;
                    }
                }

                if (matchedMetal == null) continue;

                // Ensure it has the Metallurgy storage flag to be allowed in crucible slots
                collObj.StorageFlags |= EnumItemStorageFlags.Metallurgy;

                // If it already has CombustibleProps with a SmeltedStack, keep it
                if (collObj.CombustibleProps != null && collObj.CombustibleProps.SmeltedStack != null)
                {
                    continue;
                }

                int meltPoint = meltingPoints[matchedMetal];
                string ingotCode = "game:ingot-" + matchedMetal;

                var smeltedStack = new JsonItemStack
                {
                    Type = EnumItemClass.Item,
                    Code = new AssetLocation(ingotCode),
                    StackSize = 1
                };

                if (!smeltedStack.Resolve(api.World, "[HoardersForge] smelted stack resolution"))
                {
                    api.Logger.Warning("[HoardersForge] Failed to resolve smelted stack for {0}", collObj.Code);
                    continue;
                }

                collObj.CombustibleProps = new CombustibleProperties
                {
                    MeltingPoint = meltPoint,
                    MeltingDuration = 30,
                    SmeltedRatio = 1,
                    SmeltedStack = smeltedStack,
                    RequiresContainer = true
                };

                api.Logger.VerboseDebug("[HoardersForge] Registered smelting properties for {0} (metal: {1}, ingot: {2})", collObj.Code, matchedMetal, ingotCode);
            }
        }

        public static double GetWorkItemUnits(ItemStack stack)
        {
            if (stack?.Attributes == null) return 100.0;
            byte[] voxels = stack.Attributes.GetBytes("voxels");
            if (voxels == null) return 100.0;

            byte[,,] decodedVoxels = BlockEntityAnvil.deserializeVoxels(voxels);
            if (decodedVoxels == null) return 100.0;

            int voxelCount = 0;
            int len0 = decodedVoxels.GetLength(0);
            int len1 = decodedVoxels.GetLength(1);
            int len2 = decodedVoxels.GetLength(2);
            for (int x = 0; x < len0; x++)
            {
                for (int y = 0; y < len1; y++)
                {
                    for (int z = 0; z < len2; z++)
                    {
                        if (decodedVoxels[x, y, z] != 0)
                        {
                            voxelCount++;
                        }
                    }
                }
            }

            if (voxelCount == 0) return 0.0;

            bool hasSmithingPlus = InstanceApi?.ModLoader?.IsModEnabled("smithingplus") ?? false;
            return ForgeMath.CalculateBaseUnits(voxelCount, hasSmithingPlus);
        }

        private static Dictionary<string, int> dynamicVoxelCache = new Dictionary<string, int>();
        private static Dictionary<string, double> resolvedBaseUnitsCache = new Dictionary<string, double>();

        public static bool IsSmithedItem(CollectibleObject collObj, string path)
        {
            if (collObj == null) return false;
            if (collObj.Tool.HasValue || path.Contains("head-") || path.Contains("blade-") || path.Contains("arrowhead"))
            {
                return true;
            }
            string metal = ExtractMetalFromPath(path);
            return metal != null && GetRecipeVoxelCount(path, metal) > 0;
        }

        public static string ExtractMetalFromPath(string path)
        {
            string[] metals = { "copper", "tinbronze", "bismuthbronze", "blackbronze", "gold", "silver", "brass", "lead", "tin", "zinc", "bismuth" };
            foreach (var metal in metals)
            {
                if (path.EndsWith("-" + metal) || path.Contains("-" + metal + "-"))
                {
                    return metal;
                }
            }
            return null;
        }

        public static int GetRecipeVoxelCount(string itemCodePath, string metal)
        {
            itemCodePath = ForgeMath.GetRecipePath(itemCodePath);
            if (dynamicVoxelCache.TryGetValue(itemCodePath, out int cachedCount))
            {
                return cachedCount;
            }

            var system = InstanceApi?.ModLoader?.GetModSystem<RecipeRegistrySystem>();
            var recipes = system?.SmithingRecipes;
            if (recipes != null)
            {
                double minVoxelsPerItem = double.MaxValue;

                foreach (var recipe in recipes)
                {
                    if (recipe?.Output?.Code == null) continue;

                    string recipePath = recipe.Output.Code.Path;
                    string resolvedPath = recipePath.Replace("{metal}", metal).Replace("*", metal);

                    if (resolvedPath == itemCodePath)
                    {
                        int count = 0;
                        var voxels = recipe.Voxels;
                        if (voxels != null)
                        {
                            int len0 = voxels.GetLength(0);
                            int len1 = voxels.GetLength(1);
                            int len2 = voxels.GetLength(2);
                            for (int x = 0; x < len0; x++)
                            {
                                for (int y = 0; y < len1; y++)
                                {
                                    for (int z = 0; z < len2; z++)
                                    {
                                        if (voxels[x, y, z]) count++;
                                    }
                                }
                            }
                        }

                        if (count > 0)
                        {
                            int stackSize = recipe.Output.StackSize > 0 ? recipe.Output.StackSize : 1;
                            double voxelsPerItem = (double)count / stackSize;
                            if (voxelsPerItem < minVoxelsPerItem)
                            {
                                minVoxelsPerItem = voxelsPerItem;
                            }
                        }
                    }
                }

                if (minVoxelsPerItem < double.MaxValue)
                {
                    int finalVoxelCount = (int)Math.Round(minVoxelsPerItem);
                    if (finalVoxelCount < 1) finalVoxelCount = 1;
                    dynamicVoxelCache[itemCodePath] = finalVoxelCount;
                    InstanceApi?.Logger.VerboseDebug("[HoardersForge] Dynamically resolved minimum voxel count for {0}: {1} voxels (exact min: {2})", itemCodePath, finalVoxelCount, minVoxelsPerItem);
                    return finalVoxelCount;
                }
            }

            // Cache the negative result as well to prevent repeatedly scanning recipes for non-smithed items
            dynamicVoxelCache[itemCodePath] = -1;
            return -1;
        }

        public static double GetFinishedToolBaseUnits(string path)
        {
            string cleanPath = ForgeMath.GetRecipePath(path);
            if (resolvedBaseUnitsCache.TryGetValue(cleanPath, out double cachedUnits))
            {
                return cachedUnits;
            }

            bool hasSmithingPlus = InstanceApi?.ModLoader?.IsModEnabled("smithingplus") ?? false;
            string metal = ExtractMetalFromPath(cleanPath);

            if (metal != null)
            {
                var system = InstanceApi?.ModLoader?.GetModSystem<RecipeRegistrySystem>();
                var recipes = system?.SmithingRecipes;
                if (recipes != null && recipes.Count > 0)
                {
                    double bestUnits = -1;
                    foreach (var recipe in recipes)
                    {
                        if (recipe?.Output?.Code == null) continue;

                        string recipePath = recipe.Output.Code.Path;
                        string resolvedPath = recipePath.Replace("{metal}", metal).Replace("*", metal);

                        if (resolvedPath == cleanPath)
                        {
                            int totalVoxels = 0;
                            var voxels = recipe.Voxels;
                            if (voxels != null)
                            {
                                int len0 = voxels.GetLength(0);
                                int len1 = voxels.GetLength(1);
                                int len2 = voxels.GetLength(2);
                                for (int x = 0; x < len0; x++)
                                {
                                    for (int y = 0; y < len1; y++)
                                    {
                                        for (int z = 0; z < len2; z++)
                                        {
                                            if (voxels[x, y, z]) totalVoxels++;
                                        }
                                    }
                                }
                            }

                            if (totalVoxels > 0)
                            {
                                int stackSize = recipe.Output.StackSize > 0 ? recipe.Output.StackSize : 1;
                                double units;
                                if (hasSmithingPlus)
                                {
                                    double voxelsPerItem = (double)totalVoxels / stackSize;
                                    double rawUnits = voxelsPerItem * (100.0 / 42.0);
                                    units = Math.Floor(rawUnits / 5.0) * 5.0;
                                    if (units < 5.0 && rawUnits > 0) units = 5.0;
                                }
                                else
                                {
                                    int totalIngots = (totalVoxels <= 42) ? 1 : 2;
                                    units = (totalIngots * 100.0) / stackSize;
                                }

                                if (bestUnits < 0 || units < bestUnits)
                                {
                                    bestUnits = units;
                                }
                            }
                        }
                    }

                    if (bestUnits > 0)
                    {
                        resolvedBaseUnitsCache[cleanPath] = bestUnits;
                        InstanceApi?.Logger.VerboseDebug("[HoardersForge] Resolved base units for {0}: {1}u", cleanPath, bestUnits);
                        return bestUnits;
                    }
                }
            }

            // Default Fallbacks
            double fallback = 100.0;
            if (cleanPath.Contains("plate") || cleanPath.Contains("longbladehead") || cleanPath.Contains("swordblade"))
            {
                fallback = 200.0;
            }
            else if (cleanPath.Contains("arrowhead"))
            {
                fallback = 10.0;
            }

            resolvedBaseUnitsCache[cleanPath] = fallback;
            return fallback;
        }

        public static double GetVanillaSmeltableUnits(ItemStack stack)
        {
            if (stack == null) return 0;
            var props = stack.Collectible.GetCombustibleProperties(null, stack, null);
            if (props != null && props.SmeltedStack != null)
            {
                double ratio = props.SmeltedRatio > 0 ? props.SmeltedRatio : 1.0;
                double stackSizeMultiplier = props.SmeltedStack.StackSize;
                return (stack.StackSize * stackSizeMultiplier) / ratio * 100.0;
            }
            return 0;
        }

        public static string GetMetalType(ItemStack stack)
        {
            if (stack?.Collectible?.Code?.Path == null) return null;
            
            var props = stack.Collectible.GetCombustibleProperties(null, stack, null);
            if (props?.SmeltedStack?.Code != null)
            {
                string smeltedPath = props.SmeltedStack.Code.Path;
                if (smeltedPath.StartsWith("ingot-"))
                {
                    return smeltedPath.Substring("ingot-".Length);
                }
                if (smeltedPath.StartsWith("metalportion-"))
                {
                    return smeltedPath.Substring("metalportion-".Length);
                }
            }

            string path = stack.Collectible.Code.Path;
            string[] metals = { "copper", "tinbronze", "bismuthbronze", "blackbronze", "gold", "silver", "brass", "lead", "tin", "zinc", "bismuth" };
            foreach (var metal in metals)
            {
                if (path.EndsWith("-" + metal) || path.Contains("-" + metal + "-") || path == "workitem-" + metal)
                {
                    return metal;
                }
            }
            return null;
        }

        public override void Dispose()
        {
            lock (stateLock)
            {
                activeInstances--;
                if (activeInstances <= 0)
                {
                    harmony?.UnpatchAll("hoardersforge");
                    harmony = null;
                    InstanceApi?.Logger.Notification("[HoardersForge] Harmony patches removed.");
                }
            }
            base.Dispose();
        }
    }

    [HarmonyPatch(typeof(BlockSmeltingContainer), "GetSingleSmeltableStack")]
    public static class BlockSmeltingContainer_GetSingleSmeltableStack_Patch
    {
        public static void Postfix(ItemStack[] stacks, ref MatchedSmeltableStack __result)
        {
            if (stacks == null || __result == null) return;

            bool modified = false;
            double totalUnits = 0;

            foreach (var stack in stacks)
            {
                if (stack?.Collectible?.Code?.Path == null) continue;

                string path = stack.Collectible.Code.Path;

                if (path.StartsWith("workitem-"))
                {
                    modified = true;
                    double units = HoardersForgeMod.GetWorkItemUnits(stack);
                    totalUnits += units * stack.StackSize;
                    if (HoardersForgeMod.CurrentConfig.DebugLogging)
                    {
                        HoardersForgeMod.InstanceApi?.Logger.Notification("[HoardersForge] GetSingleSmeltableStack: workitem {0} (size {1}) -> {2} units", path, stack.StackSize, units);
                    }
                }
                else if (HoardersForgeMod.IsSmithedItem(stack.Collectible, path))
                {
                    string metal = HoardersForgeMod.GetMetalType(stack);
                    if (metal == null) continue;

                    modified = true;
                    double baseUnits = HoardersForgeMod.GetFinishedToolBaseUnits(path);

                    int maxDurability = stack.Collectible.GetMaxDurability(stack);
                    double durabilityRatio = 1.0;
                    if (maxDurability > 0)
                    {
                        int remainingDurability = stack.Collectible.GetRemainingDurability(stack);
                        if (remainingDurability < 0) remainingDurability = 0;
                        if (remainingDurability > maxDurability) remainingDurability = maxDurability;
                        durabilityRatio = (double)remainingDurability / maxDurability;
                    }
                    double loss = HoardersForgeMod.CurrentConfig?.LossPercentage ?? 5.0;
                    double totalBaseUnits = baseUnits * stack.StackSize;
                    double units = ForgeMath.CalculateDurabilityYield(totalBaseUnits, durabilityRatio, loss);
                    totalUnits += units;
                    if (HoardersForgeMod.CurrentConfig.DebugLogging)
                    {
                        HoardersForgeMod.InstanceApi?.Logger.Notification("[HoardersForge] GetSingleSmeltableStack: tool/head {0} (size {1}) -> {2} units (durability: {3}/{4})", path, stack.StackSize, units, stack.Collectible.GetRemainingDurability(stack), maxDurability);
                    }
                }
                else
                {
                    double units = HoardersForgeMod.GetVanillaSmeltableUnits(stack);
                    totalUnits += units;
                    if (units > 0)
                    {
                        if (HoardersForgeMod.CurrentConfig.DebugLogging)
                        {
                            HoardersForgeMod.InstanceApi?.Logger.Notification("[HoardersForge] GetSingleSmeltableStack: vanilla smeltable {0} (size {1}) -> {2} units", path, stack.StackSize, units);
                        }
                    }
                }
            }

            if (modified)
            {
                double oldSize = __result.stackSize;
                __result.stackSize = totalUnits / 100.0;
                if (HoardersForgeMod.CurrentConfig.DebugLogging)
                {
                    HoardersForgeMod.InstanceApi?.Logger.Notification("[HoardersForge] GetSingleSmeltableStack: modified result from {0} to {1} (totalUnits: {2})", oldSize, __result.stackSize, totalUnits);
                }
            }
        }
    }

    [HarmonyPatch(typeof(AlloyRecipe), "mergeAndCompareStacks")]
    public static class AlloyRecipe_mergeAndCompareStacks_Patch
    {
        public static void Postfix(ItemStack[] inputStacks, object __result)
        {
            if (inputStacks == null || __result == null) return;

            var list = __result as System.Collections.IList;
            if (list == null || list.Count == 0) return;

            var dynamicUnitsByMetal = new Dictionary<string, double>();

            foreach (var stack in inputStacks)
            {
                if (stack?.Collectible?.Code?.Path == null) continue;

                string matchedMetal = HoardersForgeMod.GetMetalType(stack);
                if (matchedMetal == null) continue;

                string path = stack.Collectible.Code.Path;
                double units = 0;
                bool isMeltable = false;

                if (path.StartsWith("workitem-"))
                {
                    isMeltable = true;
                    double workItemUnits = HoardersForgeMod.GetWorkItemUnits(stack);
                    units = workItemUnits * stack.StackSize;
                    if (HoardersForgeMod.CurrentConfig.DebugLogging)
                    {
                        HoardersForgeMod.InstanceApi?.Logger.Notification("[HoardersForge] mergeAndCompareStacks processing: workitem {0} (size {1}) -> metal: {2}, units: {3}", path, stack.StackSize, matchedMetal, units);
                    }
                }
                else if (HoardersForgeMod.IsSmithedItem(stack.Collectible, path))
                {
                    isMeltable = true;
                    double baseUnits = HoardersForgeMod.GetFinishedToolBaseUnits(path);

                    int maxDurability = stack.Collectible.GetMaxDurability(stack);
                    double durabilityRatio = 1.0;
                    if (maxDurability > 0)
                    {
                        int remainingDurability = stack.Collectible.GetRemainingDurability(stack);
                        if (remainingDurability < 0) remainingDurability = 0;
                        if (remainingDurability > maxDurability) remainingDurability = maxDurability;
                        durabilityRatio = (double)remainingDurability / maxDurability;
                    }
                    double loss = HoardersForgeMod.CurrentConfig?.LossPercentage ?? 5.0;
                    double totalBaseUnits = baseUnits * stack.StackSize;
                    units = ForgeMath.CalculateDurabilityYield(totalBaseUnits, durabilityRatio, loss);
                    if (HoardersForgeMod.CurrentConfig.DebugLogging)
                    {
                        HoardersForgeMod.InstanceApi?.Logger.Notification("[HoardersForge] mergeAndCompareStacks processing: tool/head {0} (size {1}) -> metal: {2}, units: {3} (durability: {4}/{5})", path, stack.StackSize, matchedMetal, units, stack.Collectible.GetRemainingDurability(stack), maxDurability);
                    }
                }
                else
                {
                    double vanillaUnits = HoardersForgeMod.GetVanillaSmeltableUnits(stack);
                    if (vanillaUnits > 0)
                    {
                        isMeltable = true;
                        units = vanillaUnits;
                        if (HoardersForgeMod.CurrentConfig.DebugLogging)
                        {
                            HoardersForgeMod.InstanceApi?.Logger.Notification("[HoardersForge] mergeAndCompareStacks processing: vanilla smeltable {0} (size {1}) -> metal: {2}, units: {3}", path, stack.StackSize, matchedMetal, units);
                        }
                    }
                }

                if (isMeltable)
                {
                    if (dynamicUnitsByMetal.ContainsKey(matchedMetal))
                    {
                        dynamicUnitsByMetal[matchedMetal] += units;
                    }
                    else
                    {
                        dynamicUnitsByMetal[matchedMetal] = units;
                    }
                }
            }

            if (dynamicUnitsByMetal.Count == 0) return;

            foreach (var matched in list)
            {
                if (matched == null) continue;

                var type = matched.GetType();
                var stackField = type.GetField("stack", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var stackSizeField = type.GetField("stackSize", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                if (stackField == null || stackSizeField == null) continue;

                ItemStack matchedStack = stackField.GetValue(matched) as ItemStack;
                if (matchedStack?.Collectible?.Code?.Path == null) continue;

                string codePath = matchedStack.Collectible.Code.Path;
                string metal = null;
                if (codePath.StartsWith("ingot-"))
                {
                    metal = codePath.Substring("ingot-".Length);
                }
                else if (codePath.StartsWith("metalportion-"))
                {
                    metal = codePath.Substring("metalportion-".Length);
                }

                if (metal != null)
                {
                    if (dynamicUnitsByMetal.TryGetValue(metal, out double dynamicUnits))
                    {
                        double originalContribution = 0;
                        foreach (var stack in inputStacks)
                        {
                            if (stack?.Collectible?.Code?.Path == null) continue;
                            string stackMetal = HoardersForgeMod.GetMetalType(stack);
                            if (stackMetal == metal)
                            {
                                var props = stack.Collectible.GetCombustibleProperties(null, stack, null);
                                double ratio = (props != null && props.SmeltedRatio > 0) ? props.SmeltedRatio : 1.0;
                                double stackSizeMultiplier = (props != null && props.SmeltedStack != null) ? props.SmeltedStack.StackSize : 1.0;
                                originalContribution += (stack.StackSize * stackSizeMultiplier) / ratio;
                            }
                        }

                        double currentStackSize = (double)stackSizeField.GetValue(matched);
                        double newStackSize = currentStackSize - originalContribution + (dynamicUnits / 100.0);
                        if (newStackSize < 0) newStackSize = 0;

                        stackSizeField.SetValue(matched, newStackSize);
                        if (HoardersForgeMod.CurrentConfig.DebugLogging)
                        {
                            HoardersForgeMod.InstanceApi?.Logger.Notification("[HoardersForge] mergeAndCompareStacks modified: matched {0}. Original contribution: {1}, New contribution: {2} (dynamicUnits: {3}, total stack size in ingot units)", codePath, originalContribution, newStackSize, dynamicUnits);
                        }
                    }
                }
            }
        }
    }
}
