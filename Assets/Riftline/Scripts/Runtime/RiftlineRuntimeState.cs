using System;

namespace Riftline
{
    internal sealed class RiftlineAlignment
    {
        public float Heading { get; private set; }
        public float Age { get; private set; } = 100f;
        int mode;
        int lastTarget;
        float start;
        float delta;

        public void Reset()
        {
            Heading = start = delta = 0;
            lastTarget = 0;
            Age = 100f;
        }

        public void Configure(int value, int target)
        {
            mode = value;
            if (mode == 2) Heading = 0;
            start = Heading;
            delta = DeltaAngle(start, target);
            lastTarget = target;
            Age = Math.Abs(delta) < .001f ? 100f : 0f;
        }

        public void Advance(int target, float dt, bool running)
        {
            if (!running) return;
            if (target != lastTarget)
            {
                lastTarget = target;
                start = Heading;
                delta = DeltaAngle(start, target);
                Age = 0;
            }
            Age = Math.Min(100f, Age + Math.Max(0, dt));
            float t = Math.Min(1f, Age / .65f);
            Heading = mode == 2 ? 0 : mode == 1
                ? start + delta * (t >= 1 ? 1 : t >= .5f ? .5f : 0)
                : start + delta * RiftlineRuntimeMath.Smooth(t);
        }

        static float DeltaAngle(float from, float to)
        {
            float delta = (to - from) % 360f;
            if (delta > 180f) delta -= 360f;
            if (delta < -180f) delta += 360f;
            return delta;
        }
    }

    internal static class RiftlineRuntimeMath
    {
        public static float Smooth(float t) => t * t * (3f - 2f * t);

        public static void NavigationCenter(float runnerX, float runnerZ, float yaw, float distance, out float x, out float z)
        {
            double radians = yaw * Math.PI / 180d;
            x = runnerX - (float)Math.Sin(radians) * distance;
            z = runnerZ - (float)Math.Cos(radians) * distance;
        }

        public static float RunnerBaseHeight(float eyeHeight) => eyeHeight - 1.1f - .04f;
        public static float MenuHeight(float eyeHeight) => eyeHeight - .38f;

        public static void EndlessRegion(float elapsed, out int from, out int to, out float blend)
        {
            int cycle = (int)Math.Floor(Math.Max(0, elapsed) / 120f);
            float time = elapsed - cycle * 120f;
            from = cycle % 3;
            to = time >= 114f ? (from + 1) % 3 : from;
            blend = time < 114f ? 0 : Smooth(Math.Min(1, (time - 114f) / 6f));
        }
    }

    internal sealed class RiftlineTrackingGate
    {
        public bool HasSeenXr { get; private set; }
        public bool CanRun { get; private set; }
        public float WearerSeconds { get; private set; }
        bool wasWearing;

        public void Observe(bool active, bool deviceValid, bool tracked, bool worn, bool focused, float dt)
        {
            HasSeenXr |= active || deviceValid;
            bool wearing = active && deviceValid && tracked && worn && focused;
            CanRun = focused && (!HasSeenXr || wearing);
            if (wearing && wasWearing) WearerSeconds += Math.Max(0, dt);
            wasWearing = wearing;
        }
    }
}
