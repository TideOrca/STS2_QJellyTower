namespace QJellyTower.UI
{
    /// <summary>
    /// 顶栏上的 Q 弹开关。
    ///
    /// 刻意用原生 Control + 信号搭建，而不是派生 NButton：
    /// 派生类要依赖引擎回调 _Ready / _GuiInput，而原生信号（GuiInput / MouseEntered）
    /// 通过 GodotSharp 的托管连接生效，跨版本更稳。
    /// 尺寸沿用顶栏按钮的 80x80；图标是本 mod 自带的彩图，所以开/关用明暗区分
    /// （彩图染色不好看，压暗更直观）：开启保持原色，关闭压暗并略微透明。
    /// 鼠标悬停时弹一个和原版顶栏按钮同款的提示框，里面写明快捷键。
    /// </summary>
    public static class JellyToggleButton
    {
        private const string IconPath = "res://Q弹尖塔/images/ui/jelly_icon.png";
        private const string ClickSfx = "event:/sfx/ui/clicks/ui_click";
        private const string HoverSfx = "event:/sfx/ui/clicks/ui_hover";

        private static readonly Color OnColor = new Color(1f, 1f, 1f, 1f);
        private static readonly Color OffColor = new Color(0.45f, 0.45f, 0.50f, 0.85f);

        private static Control _root;
        private static TextureRect _icon;
        private static Tween _iconTween;

        public static Control Create()
        {
            _root = new Control
            {
                Name = "QJellyToggleButton",
                CustomMinimumSize = new Vector2(80f, 80f),
                MouseFilter = Control.MouseFilterEnum.Stop,
                FocusMode = Control.FocusModeEnum.None,
            };

            MarginContainer margin = new MarginContainer
            {
                Name = "Control",
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            margin.AddThemeConstantOverride("margin_left", 12);
            margin.AddThemeConstantOverride("margin_top", 12);
            margin.AddThemeConstantOverride("margin_right", 12);
            margin.AddThemeConstantOverride("margin_bottom", 12);
            margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _root.AddChild(margin);

            _icon = new TextureRect
            {
                Name = "Icon",
                Texture = GD.Load<Texture2D>(IconPath),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            _icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            margin.AddChild(_icon);

            _root.GuiInput += OnGuiInput;
            _root.MouseEntered += OnHover;
            _root.MouseExited += OnUnhover;

            Refresh();
            return _root;
        }

        /// <summary>把图标颜色同步到当前开关状态。快捷键切换后也靠它刷新。</summary>
        public static void Refresh()
        {
            if (_icon == null || !GodotObject.IsInstanceValid(_icon))
            {
                return;
            }

            _icon.Modulate = QJellyTowerMain.JellyActive ? OnColor : OffColor;
        }

        private static void OnGuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouse
                && mouse.Pressed
                && mouse.ButtonIndex == MouseButton.Left)
            {
                PlaySfx(ClickSfx);
                QJellyTowerMain.ToggleJelly();
                Refresh();
                _root?.AcceptEvent();
            }
        }

        private static void OnHover()
        {
            PlaySfx(HoverSfx);
            ShowHoverTip();
            AnimateIcon(Vector2.One * 1.15f, 0.12f, Tween.TransitionType.Back);
        }

        private static void OnUnhover()
        {
            HideHoverTip();
            AnimateIcon(Vector2.One, 0.2f, Tween.TransitionType.Cubic);
        }

        private static void AnimateIcon(Vector2 target, float duration, Tween.TransitionType transition)
        {
            if (_icon == null || !GodotObject.IsInstanceValid(_icon))
            {
                return;
            }

            _icon.PivotOffset = _icon.Size * 0.5f;
            _iconTween?.Kill();
            _iconTween = _icon.CreateTween();
            _iconTween.TweenProperty(_icon, "scale", target, duration)
                .SetTrans(transition)
                .SetEase(Tween.EaseType.Out);
        }

        private static void ShowHoverTip()
        {
            if (_root == null || !GodotObject.IsInstanceValid(_root))
            {
                return;
            }

            try
            {
                // HoverTip 是结构体，构造很便宜，不必缓存
                HoverTip tip = new HoverTip(
                    new LocString("settings_ui", "QJELLYTOWER-HOVER.title"),
                    new LocString("settings_ui", "QJELLYTOWER-HOVER.description"),
                    null);

                // HoverTipAlignment.None 不会自动定位，得照原版顶栏按钮那样手动摆：
                // 右边缘对齐按钮右边缘，整体垂到按钮下方 20px
                NHoverTipSet set = NHoverTipSet.CreateAndShow(_root, tip, HoverTipAlignment.None);
                set.GlobalPosition = _root.GlobalPosition
                    + new Vector2(_root.Size.X - set.Size.X, _root.Size.Y + 20f);
            }
            catch (Exception ex)
            {
                Log.Warn("[Q弹尖塔] hover tip failed: " + ex.Message, 2);
            }
        }

        private static void HideHoverTip()
        {
            if (_root == null || !GodotObject.IsInstanceValid(_root))
            {
                return;
            }

            try
            {
                NHoverTipSet.Remove(_root);
            }
            catch (Exception ex)
            {
                Log.Warn("[Q弹尖塔] hover tip remove failed: " + ex.Message, 2);
            }
        }

        private static void PlaySfx(string sfx)
        {
            try
            {
                SfxCmd.Play(sfx, 1f);
            }
            catch (Exception ex)
            {
                Log.Warn("[Q弹尖塔] sfx play failed: " + ex.Message, 2);
            }
        }
    }
}
