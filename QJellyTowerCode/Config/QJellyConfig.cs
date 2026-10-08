namespace QJellyTower.Config
{
    /// <summary>
    /// 模组配置。BaseLib 的 SimpleModConfig 会按 public static 属性的类型自动生成
    /// 配置面板（bool → 勾选框，int/float/double → 滑条），并自动存档到
    /// user://mod_configs/QJellyTower.cfg。
    ///
    /// 注意：属性必须是 static 且有 get/set，否则会被 BaseLib 忽略。
    /// </summary>
    public class QJellyConfig : SimpleModConfig
    {
        [ConfigSection("JellySection")]
        public static bool JellyEnabled { get; set; } = false;

        /// <summary>
        /// 压扁 ↔ 拉高 走完一个完整来回所需的秒数。默认 60/128 秒，
        /// 正好是 128 BPM 音乐的一拍，方便弹动踩点。
        /// 滑条步进也用 1/128 秒，保证这个值能被精确保存和还原。
        /// </summary>
        [ConfigSlider(0.0, 2.0, 1.0 / 128.0, Format = "{0:0.000}")]
        public static float CycleSeconds { get; set; } = 60f / 128f;

        /// <summary>拉伸/压扁的强度。0.30 表示横向最多放大 30%、纵向相应收窄。</summary>
        [ConfigSlider(0.0, 0.50, 0.01, Format = "{0:0.00}")]
        public static float Amplitude { get; set; } = 0.30f;

        /// <summary>弹动期间循环 BGM 的音量，0 为静音。</summary>
        [ConfigSlider(0.0, 1.0, 0.05, Format = "{0:0.00}")]
        public static float MusicVolume { get; set; } = 0.5f;

        /// <summary>
        /// 角色左右镜像翻转（轴对称来回过渡），默认关闭。
        /// 它跑在自己的节奏上，周期是弹动周期的两倍（比弹动慢一倍）。
        /// </summary>
        public static bool JellyFlip { get; set; } = false;

        [ConfigSection("TargetsSection")]
        public static bool JellyPlayers { get; set; } = true;

        public static bool JellyEnemies { get; set; } = true;

        /// <summary>战斗手牌与选卡界面里卡牌的弹动。</summary>
        public static bool JellyCards { get; set; } = false;

        public static bool JellyPotions { get; set; } = false;
    }
}
