using QJellyTower.UI;

namespace QJellyTower.Patches
{
    /// <summary>
    /// 按 Q 切换弹动开关（无修饰键）。
    /// 挂在游戏自己的 NTopBar._Input 上（而不是自建节点的 _Input），
    /// 这样不依赖本 mod 自定义类型被引擎识别为脚本。
    /// </summary>
    [HarmonyPatch(typeof(NTopBar), "_Input")]
    public static class JellyHotkeyPatch
    {
        /// <summary>去抖窗口。不同键盘/驱动可能连发非 echo 的按下事件，长按会切偶数次看起来像没反应。</summary>
        private const ulong DebounceMs = 250;

        private static ulong _lastTriggerMs;

        [HarmonyPostfix]
        public static void Postfix(NTopBar __instance, InputEvent inputEvent)
        {
            if (inputEvent is not InputEventKey key || !key.Pressed || key.Echo)
            {
                return;
            }

            // 带修饰键的组合一律不响应，只有裸按 Q 才算
            if (key.CtrlPressed || key.AltPressed || key.ShiftPressed || key.MetaPressed)
            {
                return;
            }

            // 用 Keycode 而不是 PhysicalKeycode：跟随用户看到的那个字母
            if (key.Keycode != Key.Q)
            {
                return;
            }

            // Godot 的 _input 早于 _gui_input，不加这道门的话在搜索框里打字会误触发
            if (IsTyping(__instance))
            {
                return;
            }

            ulong now = Time.GetTicksMsec();
            if (now - _lastTriggerMs < DebounceMs)
            {
                return;
            }

            _lastTriggerMs = now;

            QJellyTowerMain.ToggleJelly();
            JellyToggleButton.Refresh();
        }

        private static bool IsTyping(Node node)
        {
            Viewport viewport = node?.GetViewport();
            return viewport?.GuiGetFocusOwner() is LineEdit or TextEdit;
        }
    }
}
