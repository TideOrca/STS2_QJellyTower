namespace QJellyTower.Core
{
    /// <summary>
    /// 弹动总驱动。挂在 SceneTree 的 ProcessFrame 事件上常驻，每帧按 2 拍相位算出拉压系数，
    /// 施加到全部目标。
    ///
    /// 一拍循环（2 拍正好一轮）：
    ///   前半拍 压扁 → 拉高   后半拍 拉高 → 压扁
    /// 中间不做停留，只在过渡中快速穿过原状；拍内用三次贝塞尔（ease-in-out）过渡。
    ///
    /// 刻意做成静态类 + 引擎事件，而不是自定义 Node 的 _Process：
    /// 后者依赖 C# 脚本绑定被引擎正确识别，跨版本更容易失效。
    /// </summary>
    public static class JellyDriver
    {
        /// <summary>翻转时的最小宽度系数。绝不让它归零，否则幅度基准会永久丢失、角色再也回不来。</summary>
        private const float MinFlipWidth = 0.12f;

        /// <summary>翻转周期相对弹动周期的比例。2.0 表示翻转比弹动慢一倍。</summary>
        private const float FlipPeriodRatio = 2.0f;

        private static readonly List<JellyTarget> Active = new();
        private static readonly Dictionary<ulong, JellyTarget> Cache = new();
        private static readonly List<ulong> DeadKeys = new();

        /// <summary>选卡界面的卡牌行。由 NCardRewardSelectionScreen / NChooseACardSelectionScreen 的补丁登记。</summary>
        private static readonly List<Control> CardRows = new();

        private static bool _installed;
        private static ulong _lastTicks;
        private static double _elapsed;
        private static double _flipElapsed;
        private static double _refreshTimer;
        private static bool _released = true;
        private static bool _wasActive;
        private static bool _cardScreenActive;

        // 只为诊断用：目标数量变化时打一次日志，便于定位"某类没弹起来"
        private static int _lastCreatures = -1;
        private static int _lastCards = -1;
        private static int _lastPotions = -1;

        /// <summary>幂等地把驱动接到 SceneTree 上。由 NTopBar 的补丁在进入游戏界面时调用。</summary>
        public static void EnsureInstalled()
        {
            if (_installed)
            {
                return;
            }

            if (Engine.GetMainLoop() is not SceneTree tree)
            {
                return;
            }

            tree.ProcessFrame += OnProcessFrame;
            _lastTicks = Time.GetTicksUsec();
            _installed = true;

            Log.Info("[Q弹尖塔] jelly driver installed on SceneTree.ProcessFrame", 2);
        }

        /// <summary>登记一个选卡界面的卡牌行（幂等）。</summary>
        public static void RegisterCardRow(Control row)
        {
            if (row == null || !GodotObject.IsInstanceValid(row))
            {
                return;
            }

            if (!CardRows.Contains(row))
            {
                CardRows.Add(row);
            }
        }

        private static void OnProcessFrame()
        {
            ulong now = Time.GetTicksUsec();
            double delta = (now - _lastTicks) / 1_000_000.0;
            _lastTicks = now;

            // 首帧或长时间卡顿（窗口拖动、加载）后，差值不可信，按 60fps 兜底
            if (delta <= 0.0 || delta > 0.25)
            {
                delta = 1.0 / 60.0;
            }

            Tick(delta);
        }

        private static void Tick(double delta)
        {
            // 选卡界面得先探出来才能决定要不要激活，所以这一步必须无条件跑
            _refreshTimer -= delta;
            bool doRefresh = _refreshTimer <= 0.0;
            if (doRefresh)
            {
                _refreshTimer = 0.1;
                RefreshCardRows();
            }

            bool inCombat = NCombatRoom.Instance != null;
            bool active = QJellyTowerMain.JellyActive && (inCombat || _cardScreenActive);

            // 音乐与弹动同进同出，避免在其他房间一直响
            JellyMusic.Tick(active, QJellyConfig.MusicVolume);

            if (!active)
            {
                _wasActive = false;
                ReleaseAll();
                return;
            }

            // 每次重新进入都从头起拍，接上上一段的相位会显得突兀
            if (!_wasActive)
            {
                _wasActive = true;
                _elapsed = 0.0;
                _flipElapsed = 0.0;
            }

            _released = false;

            if (doRefresh)
            {
                RefreshTargets();
            }

            float period = Mathf.Max(0.05f, QJellyConfig.CycleSeconds);
            _elapsed += delta;

            float phase = (float)(_elapsed % period) / period;

            // 相位先拆好再分别喂给两个效果：否则弹动那边"幅度为 0 就早退"会连带跳过翻转
            bool firstHalf = phase < 0.5f;
            float local = firstHalf ? phase * 2f : (phase - 0.5f) * 2f;
            float eased = CubicBezierEase.EaseInOut(local);

            Vector2 factor = ComputeFactor(firstHalf, eased, Mathf.Clamp(QJellyConfig.Amplitude, 0f, 0.49f));

            // 翻转跑在自己的节奏上，周期是弹动的两倍
            float flipPeriod = Mathf.Max(0.05f, period * FlipPeriodRatio);
            _flipElapsed += delta;

            float flipPhase = (float)(_flipElapsed % flipPeriod) / flipPeriod;
            bool flipFirstHalf = flipPhase < 0.5f;
            float flipLocal = flipFirstHalf ? flipPhase * 2f : (flipPhase - 0.5f) * 2f;
            float flipEased = CubicBezierEase.EaseInOut(flipLocal);

            float flipFactor = ComputeFlipFactor(flipFirstHalf, flipEased, QJellyConfig.JellyFlip);

            for (int i = 0; i < Active.Count; i++)
            {
                Active[i].Apply(factor, flipFactor);
            }
        }

        /// <summary>算出一个循环内的拉压系数。k &gt; 0 压扁（宽矮），k &lt; 0 拉高（窄高）。</summary>
        private static Vector2 ComputeFactor(bool firstHalf, float eased, float amplitude)
        {
            if (amplitude <= 0f)
            {
                return Vector2.One;
            }

            // eased 从 0 走到 1，映射成 +amp → -amp 的连续摆动
            float swing = amplitude * (1f - 2f * eased);
            float k = firstHalf ? swing : -swing;

            float sx = 1f + k;

            // 体积守恒：横向拉伸多少，纵向就相应收窄多少（反之亦然），这样最像果冻
            return new Vector2(sx, 1f / sx);
        }

        /// <summary>
        /// 算出一个循环内的水平翻转系数（前半拍 +1 → -1，后半拍走回来）。
        /// 朝向符号与宽度分开处理：宽度留 MinFlipWidth 下限，绝不归零——
        /// 系数一旦精确为 0，上一帧的宽度基准就被抹掉了，角色会永久不可见。
        /// </summary>
        private static float ComputeFlipFactor(bool firstHalf, float eased, bool enabled)
        {
            if (!enabled)
            {
                return 1f;
            }

            float f = 1f - 2f * eased;
            float v = firstHalf ? f : -f;

            float sign = v >= 0f ? 1f : -1f;
            float width = Mathf.Lerp(MinFlipWidth, 1f, Mathf.Abs(v));
            return sign * width;
        }

        /// <summary>清理失效的卡牌行，并判断当前是否有选卡界面开着。</summary>
        private static void RefreshCardRows()
        {
            bool found = false;

            for (int i = CardRows.Count - 1; i >= 0; i--)
            {
                Control row = CardRows[i];

                if (row == null || !GodotObject.IsInstanceValid(row))
                {
                    CardRows.RemoveAt(i);
                    continue;
                }

                if (!found && row.IsVisibleInTree() && row.GetChildCount() > 0)
                {
                    found = true;
                }
            }

            _cardScreenActive = found;
        }

        private static void RefreshTargets()
        {
            // 先剔除已销毁的节点，避免 InstanceId 被复用后拿到过期的缩放基准
            DeadKeys.Clear();
            foreach (KeyValuePair<ulong, JellyTarget> pair in Cache)
            {
                if (!pair.Value.IsValid)
                {
                    DeadKeys.Add(pair.Key);
                }
            }

            for (int i = 0; i < DeadKeys.Count; i++)
            {
                Cache.Remove(DeadKeys[i]);
            }

            Active.Clear();
            int creatures = CollectCreatures();
            int cards = CollectHandCards() + CollectGridCards();
            int potions = CollectPotions();

            if (creatures != _lastCreatures || cards != _lastCards || potions != _lastPotions)
            {
                _lastCreatures = creatures;
                _lastCards = cards;
                _lastPotions = potions;

                Log.Info("[Q弹尖塔] jelly targets: creatures=" + creatures
                    + " cards=" + cards + " potions=" + potions, 2);
            }
        }

        private static int CollectCreatures()
        {
            NCombatRoom room = NCombatRoom.Instance;
            if (room == null)
            {
                return 0;
            }

            bool wantPlayers = QJellyConfig.JellyPlayers;
            bool wantEnemies = QJellyConfig.JellyEnemies;
            if (!wantPlayers && !wantEnemies)
            {
                return 0;
            }

            int count = 0;
            foreach (NCreature creature in room.CreatureNodes)
            {
                if (!GodotObject.IsInstanceValid(creature))
                {
                    continue;
                }

                bool isMonster = creature.Entity != null && creature.Entity.IsMonster;
                if (isMonster ? !wantEnemies : !wantPlayers)
                {
                    continue;
                }

                NCreatureVisuals visuals = creature.Visuals;
                if (visuals == null || !GodotObject.IsInstanceValid(visuals))
                {
                    continue;
                }

                // 「包围」遭遇里游戏自己在翻转同一个身体节点，让给它，免得两边抢方向
                bool gameDrivesFlip = creature.Entity != null
                    && creature.Entity.HasPower<SurroundedPower>();

                // 每帧重新取当前可见身体：恐高症模式会换节点，不能只捕获一次
                Func<Node2D> flipSource = gameDrivesFlip ? null : () => visuals.GetCurrentBody();

                Active.Add(Cached(visuals, () => new JellyTarget(visuals, flipSource)));
                count++;
            }

            return count;
        }

        /// <summary>
        /// 战斗手牌。缩放的是 holder 而不是卡面本身：holder 恰好是手牌布局用来做
        /// 「数量多时整体缩小 / 悬停放大」的那个节点，缩放它视觉上就是整张卡在弹。
        /// </summary>
        private static int CollectHandCards()
        {
            if (!QJellyConfig.JellyCards)
            {
                return 0;
            }

            NPlayerHand hand = NCombatRoom.Instance?.Ui?.Hand;
            if (hand == null || !GodotObject.IsInstanceValid(hand))
            {
                return 0;
            }

            int count = 0;
            foreach (NHandCardHolder holder in hand.ActiveHolders)
            {
                if (!GodotObject.IsInstanceValid(holder))
                {
                    continue;
                }

                Active.Add(Cached(holder, () => new JellyTarget(holder)));
                count++;
            }

            return count;
        }

        /// <summary>选卡界面（战斗奖励、事件选卡等）里排成一行的卡。</summary>
        private static int CollectGridCards()
        {
            if (!QJellyConfig.JellyCards || CardRows.Count == 0)
            {
                return 0;
            }

            int count = 0;
            foreach (Control row in CardRows)
            {
                if (row == null || !GodotObject.IsInstanceValid(row) || !row.IsVisibleInTree())
                {
                    continue;
                }

                foreach (Node child in row.GetChildren())
                {
                    if (child is not NGridCardHolder holder || !GodotObject.IsInstanceValid(holder))
                    {
                        continue;
                    }

                    Active.Add(Cached(holder, () => new JellyTarget(holder)));
                    count++;
                }
            }

            return count;
        }

        private static int CollectPotions()
        {
            if (!QJellyConfig.JellyPotions)
            {
                return 0;
            }

            NPotionContainer container = NRun.Instance?.GlobalUi?.TopBar?.PotionContainer;
            if (container == null || !GodotObject.IsInstanceValid(container))
            {
                return 0;
            }

            Control holders = container.GetNodeOrNull<Control>("MarginContainer/PotionHolders");
            if (holders == null)
            {
                return 0;
            }

            int count = 0;
            foreach (Node child in holders.GetChildren())
            {
                if (child is not NPotionHolder holder)
                {
                    continue;
                }

                // 空槽位不弹，只弹真正装了药的格子
                if (holder.Potion == null || !GodotObject.IsInstanceValid(holder.Potion))
                {
                    continue;
                }

                Active.Add(Cached(holder, () => new JellyTarget(holder)));
                count++;
            }

            return count;
        }

        /// <summary>按节点实例 ID 复用 JellyTarget，这样它记下的缩放基准能跨帧保持。</summary>
        private static JellyTarget Cached(Node node, Func<JellyTarget> factory)
        {
            ulong id = node.GetInstanceId();
            if (Cache.TryGetValue(id, out JellyTarget existing) && existing.IsValid)
            {
                return existing;
            }

            JellyTarget created = factory();
            Cache[id] = created;
            return created;
        }

        private static void ReleaseAll()
        {
            if (_released)
            {
                return;
            }

            foreach (JellyTarget target in Cache.Values)
            {
                target.Release();
            }

            _released = true;
        }
    }
}
