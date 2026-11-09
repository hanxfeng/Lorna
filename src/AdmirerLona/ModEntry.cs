using System;
using UnityEngine;
using HarmonyLib;

namespace AdmirerLona
{
    /// <summary>
    /// Entry point. The loader finds the one class implementing IMod, creates it,
    /// applies the [HarmonyPatch] classes of this assembly, then calls Init and OnEnable.
    /// All of that happens in the main menu: no save is loaded yet, so do not touch game
    /// state here. Subscribe to ModHook events instead (see ModdingGuide.zh-CN.md, "钩子点").
    /// </summary>
    public class ModEntry : IMod
    {
        /// <summary>前置模组的文件夹 id，与 manifest.xml 的 &lt;Prerequisites&gt; 保持一致（唯一来源见 State.BaseModId）。</summary>
        public const string PrerequisiteId = State.BaseModId;

        public static ModEntry Instance { get; private set; }
        public ModManifest Manifest { get; private set; }
        public ModLog Logger { get; private set; }

        public void Init(ModManifest manifest)
        {
            Instance = this;
            Manifest = manifest;
            Logger = new ModLog(manifest);

            State.DefineConfig(manifest.ID);
            Logger.Log($"{manifest.Name} v{manifest.ModVersion} loaded.");
        }

        public void OnEnable()
        {
            LonaCoroutineHost.Ensure();

            ModHook.OnGameLoadedInit += OnRunLoaded;
            ModHook.OnShutterOpenedEarly += OnShutterOpened;
            ModHook.OnGenerateCustomerVeryLate += Spawner.OnGenerate;
            ModHook.OnModItemDirectoryInit += LonaCard.Register;

            Phone.EnsureRegistered();
            LonaDebug.Register();
            ModHelper.AddDebugFunction(Manifest.ID, ModHelper.DEBUG_CATEGORY_COMMAND,
                "AdmirerLona: Self Test", SelfTest);

            Logger.Log("Enabled.");
        }

        public void OnDisable()
        {
            Gift.Reset();
            LonaCoroutineHost.DestroyHost();

            ModHook.OnGameLoadedInit -= OnRunLoaded;
            ModHook.OnShutterOpenedEarly -= OnShutterOpened;
            ModHook.OnGenerateCustomerVeryLate -= Spawner.OnGenerate;
            ModHook.OnModItemDirectoryInit -= LonaCard.Register;

            ModHelper.RemoveDebugFunctions(Manifest.ID);
            Phone.Unregister();
            Logger.Log("Disabled.");
        }

        /// <summary>读档 / 新开一局。</summary>
        private void OnRunLoaded()
        {
            try
            {
                BaseModSchema.Reset();
                BaseModSchema.Check();
                Portrait.EnsureInjected();
                State.OnRunLoaded();
                Phone.EnsureRegistered();
                Phone.EnsureContact();
            }
            catch (Exception e)
            {
                ModEntry.Log("读档初始化失败: " + e);
            }
        }

        /// <summary>每天开门：投递警告纸条、补激活检查。</summary>
        private void OnShutterOpened()
        {
            try
            {
                Portrait.EnsureInjected();
                State.TrackUpperTier();
                State.CheckActivation();
                State.ClearPauseIfReached();
                Notes.DeliverPending();
                Gift.Reset();
                Phone.EnsureContact();
            }
            catch (Exception e)
            {
                ModEntry.Log("开门处理失败: " + e);
            }
        }

        /// <summary>前置模组 Enhanced_Secret_Admirers_And_More_News 是否已加载并启用。</summary>
        public bool IsPrerequisiteReady()
        {
            foreach (LoadedMod mod in ModLoader.Mods)
            {
                if (mod == null || mod.Manifest == null) continue;
                if (mod.Manifest.ID != PrerequisiteId) continue;
                return mod.IsEnabled && mod.LoadError == null;
            }
            return false;
        }

        private void SelfTest()
        {
            Logger.Log($"self-test: id={Manifest.ID} prerequisite={PrerequisiteId}:{(IsPrerequisiteReady() ? "enabled" : "missing")} " +
                    $"schema={(BaseModSchema.Ok ? "ok" : BaseModSchema.Reason)} activated={State.Activated} " +
                    $"rep={State.Reputation} affection={State.Affection:0.##} left={State.Left}");
        }

        public static void Log(string message)
        {
            if (Instance != null && Instance.Logger != null)
            {
                Instance.Logger.Log(message);
            }
            else
            {
                Debug.Log("[AdmirerLona] " + message);
            }
        }
    }
}
