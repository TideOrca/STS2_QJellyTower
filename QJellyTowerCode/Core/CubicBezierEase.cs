namespace QJellyTower.Core
{
    /// <summary>
    /// 三次贝塞尔缓动求解器，语义与 CSS 的 cubic-bezier(x1, y1, x2, y2) 一致：
    /// 曲线两端固定为 (0,0) 与 (1,1)，两个控制点由参数给出。
    /// 用牛顿迭代解 x(t) = 输入，再取 y(t) 作为输出。
    /// </summary>
    public static class CubicBezierEase
    {
        // ease-in-out：起步缓、中间快、收尾缓，适合果冻的拍与拍之间过渡
        public const float EaseInOutX1 = 0.42f;
        public const float EaseInOutY1 = 0.00f;
        public const float EaseInOutX2 = 0.58f;
        public const float EaseInOutY2 = 1.00f;

        public static float EaseInOut(float x)
        {
            return Eval(x, EaseInOutX1, EaseInOutY1, EaseInOutX2, EaseInOutY2);
        }

        public static float Eval(float x, float x1, float y1, float x2, float y2)
        {
            if (x <= 0f)
            {
                return 0f;
            }

            if (x >= 1f)
            {
                return 1f;
            }

            float t = SolveTForX(x, x1, x2);
            return SampleCurve(y1, y2, t);
        }

        // 三次贝塞尔（p0 = 0，p3 = 1）：B(t) = 3(1-t)^2·t·p1 + 3(1-t)·t^2·p2 + t^3
        private static float SampleCurve(float p1, float p2, float t)
        {
            float mt = 1f - t;
            return 3f * mt * mt * t * p1 + 3f * mt * t * t * p2 + t * t * t;
        }

        // B'(t) = 3(1-t)^2·p1 + 6(1-t)t·(p2-p1) + 3t^2·(1-p2)
        private static float SampleDerivative(float p1, float p2, float t)
        {
            float mt = 1f - t;
            return 3f * mt * mt * p1 + 6f * mt * t * (p2 - p1) + 3f * t * t * (1f - p2);
        }

        private static float SolveTForX(float x, float x1, float x2)
        {
            float t = x;

            for (int i = 0; i < 8; i++)
            {
                float err = SampleCurve(x1, x2, t) - x;
                if (Mathf.Abs(err) < 1e-6f)
                {
                    return t;
                }

                float d = SampleDerivative(x1, x2, t);
                if (Mathf.Abs(d) < 1e-6f)
                {
                    break;
                }

                t -= err / d;
            }

            // 牛顿迭代没收敛时退回二分查找，保证结果一定在 [0,1] 内
            float lo = 0f;
            float hi = 1f;
            for (int i = 0; i < 24; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (SampleCurve(x1, x2, mid) < x)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }

            return (lo + hi) * 0.5f;
        }
    }
}
