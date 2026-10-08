namespace QJellyTower
{
    /// <summary>
    /// mod 入口。游戏 ModManager 通过 [ModInitializer] 特性反射调用 Initialize。
    /// </summary>
    [ModInitializer(nameof(Initialize))]
    public static class QJellyTowerMain
    {
        public const string ModId = "Q弹尖塔";

        /// <summary>实际生效的弹动开关。顶栏按钮 / 快捷键切换它，并同步写回配置。</summary>
        public static bool JellyActive;

        private static bool _initialized;

        public static void Initialize()
        {
            // 防御性去重：attribute 与自动 PatchAll 两条路径理论上只有一条会跑
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            // 让静态配置字段被反序列化填充，并注册到 BaseLib 的模组配置面板
            var config = new QJellyConfig();
            ModConfigRegistry.Register(ModId, config);

            // 配置面板里勾选/取消要立刻作用到运行时，并同步顶栏按钮的图标状态
            config.ConfigChanged += (_, __) => SyncFromConfig();
            config.OnConfigReloaded += SyncFromConfig;

            JellyActive = QJellyConfig.JellyEnabled;

            new Harmony("qjellytower.main").PatchAll();

            // 尽早把帧驱动挂上；顶栏补丁里还会再兜底调用一次（幂等）
            JellyDriver.EnsureInstalled();

            Log.Info("[Q弹尖塔] initialized. BaseLib=" + typeof(ModConfigRegistry).Assembly.GetName().Version, 2);
        }

        /// <summary>设置弹动开关并同步到配置（顶栏按钮与快捷键共用）。</summary>
        public static void SetJellyActive(bool active)
        {
            JellyActive = active;
            QJellyConfig.JellyEnabled = active;
            ModConfigRegistry.Get(ModId)?.SaveDebounced();
        }

        public static void ToggleJelly()
        {
            SetJellyActive(!JellyActive);
        }

        /// <summary>把配置里的值拉回运行时状态。配置面板改动的回调，不写回配置以免递归。</summary>
        public static void SyncFromConfig()
        {
            JellyActive = QJellyConfig.JellyEnabled;
            UI.JellyToggleButton.Refresh();
        }
    }
}
