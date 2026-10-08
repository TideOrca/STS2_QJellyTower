namespace QJellyTower.Core
{
    /// <summary>
    /// 一个可弹动目标。游戏本身也会改这些节点的 Scale（入场动画、悬停放大等等），
    /// 所以这里不直接覆写，而是记住"上一次自己施加的系数"，先用除法把游戏值还原出来，
    /// 再乘上当前系数。这样弹动就是叠加在游戏动画之上的，不会把游戏自己的缩放吃掉。
    ///
    /// 角色额外支持水平翻转：翻转作用的是 NCreatureVisuals 内部的「当前可见身体」节点，
    /// 而不是 NCreatureVisuals 本身——后者被 NCreature.UpdateBounds 读去算 Hitbox 尺寸，
    /// 出现负值会让命中框和选择框全乱。身体节点没人读它的 Scale，翻它是安全的。
    /// </summary>
    internal sealed class JellyTarget
    {
        private readonly Node2D _node2D;
        private readonly Control _control;

        /// <summary>每帧重新取「当前可见身体」。恐高症模式会换节点，不能只捕获一次。</summary>
        private readonly Func<Node2D> _flipSource;

        /// <summary>上一帧施加的弹动系数，用于还原出游戏自己的 Scale。</summary>
        private Vector2 _lastFactor = Vector2.One;

        /// <summary>上一帧施加的翻转系数，同样用于还原。</summary>
        private float _lastFlipFactor = 1f;

        /// <summary>上一帧实际翻转的节点，用于身体被换掉时先把旧节点还原。</summary>
        private Node2D _appliedFlipNode;

        public JellyTarget(Node2D node2D, Func<Node2D> flipSource = null)
        {
            _node2D = node2D;
            _flipSource = flipSource;
        }

        public JellyTarget(Control control)
        {
            _control = control;
        }

        public bool IsValid
        {
            get
            {
                return _node2D != null
                    ? GodotObject.IsInstanceValid(_node2D)
                    : GodotObject.IsInstanceValid(_control);
            }
        }

        public void Apply(Vector2 factor, float flipFactor)
        {
            if (!IsValid)
            {
                return;
            }

            ApplyJelly(factor);
            ApplyFlip(flipFactor);
        }

        /// <summary>把节点交还给游戏，恢复成本 mod 介入之前的样子。</summary>
        public void Release()
        {
            // 翻转的还原必须独立于 _lastFactor 判断：
            // Amplitude 为 0 时弹动系数恒为 One，跟着早退的话翻转就永远还不了原。
            RestoreFlip(_appliedFlipNode);
            _appliedFlipNode = null;
            _lastFlipFactor = 1f;

            if (!IsValid || _lastFactor == Vector2.One)
            {
                _lastFactor = Vector2.One;
                return;
            }

            if (_node2D != null)
            {
                _node2D.Scale = UndoScale(_node2D.Scale);
            }
            else
            {
                _control.Scale = UndoScale(_control.Scale);
            }

            _lastFactor = Vector2.One;
        }

        private void ApplyJelly(Vector2 factor)
        {
            if (_node2D != null)
            {
                _node2D.Scale = UndoScale(_node2D.Scale) * factor;
            }
            else
            {
                // Control 默认从左上角缩放，把轴心挪到中心，弹动才是"原地呼吸"而不是"向下生长"
                _control.PivotOffset = _control.Size * 0.5f;
                _control.Scale = UndoScale(_control.Scale) * factor;
            }

            _lastFactor = factor;
        }

        private void ApplyFlip(float flipFactor)
        {
            if (_flipSource == null)
            {
                return;
            }

            Node2D body = ResolveFlipNode();

            // 身体被换掉了（恐高症模式切换）：先把旧节点还原，再接管新节点
            if (body != _appliedFlipNode)
            {
                RestoreFlip(_appliedFlipNode);
                _appliedFlipNode = body;
                _lastFlipFactor = 1f;
            }

            if (body == null || !GodotObject.IsInstanceValid(body))
            {
                return;
            }

            Vector2 scale = body.Scale;
            float native = _lastFlipFactor == 0f ? scale.X : scale.X / _lastFlipFactor;
            body.Scale = new Vector2(native * flipFactor, scale.Y);
            _lastFlipFactor = flipFactor;
        }

        private Node2D ResolveFlipNode()
        {
            try
            {
                Node2D body = _flipSource();
                return body != null && GodotObject.IsInstanceValid(body) ? body : null;
            }
            catch (Exception ex)
            {
                Log.Warn("[Q弹尖塔] failed to resolve flip target: " + ex.Message, 2);
                return null;
            }
        }

        private void RestoreFlip(Node2D node)
        {
            if (node == null || !GodotObject.IsInstanceValid(node))
            {
                return;
            }

            Vector2 scale = node.Scale;
            float native = _lastFlipFactor == 0f ? scale.X : scale.X / _lastFlipFactor;
            node.Scale = new Vector2(native, scale.Y);
        }

        private Vector2 UndoScale(Vector2 current)
        {
            return new Vector2(
                _lastFactor.X == 0f ? current.X : current.X / _lastFactor.X,
                _lastFactor.Y == 0f ? current.Y : current.Y / _lastFactor.Y);
        }
    }
}
