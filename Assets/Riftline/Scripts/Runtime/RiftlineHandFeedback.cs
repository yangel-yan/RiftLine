using UnityEngine;

namespace Riftline
{
    public sealed class RiftlineHandFeedback : MonoBehaviour
    {
        const int RingSegments = 32;
        readonly LineRenderer[] arms = new LineRenderer[4];
        RiftlineGame game;
        LineRenderer ring;
        Material material;
        Vector3 anchor;
        HandGestureState previousState;
        float pulseUntil;

        public void Initialize(RiftlineGame owner)
        {
            if (game == owner && material) return;
            game = owner;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader)
            {
                enabled = false;
                return;
            }
            material = new Material(shader) { color = new Color(.12f, .9f, 1f, .9f) };
            for (int i = 0; i < arms.Length; i++)
                arms[i] = CreateLine("Gesture Direction " + i, 2, false);
            ring = CreateLine("Gesture Hold", RingSegments + 1, true);
            Hide();
        }

        LineRenderer CreateLine(string lineName, int positions, bool loop)
        {
            var child = new GameObject(lineName);
            child.transform.SetParent(transform, false);
            var renderer = child.AddComponent<LineRenderer>();
            renderer.positionCount = positions;
            renderer.loop = loop;
            renderer.startWidth = renderer.endWidth = .004f;
            renderer.sharedMaterial = material;
            renderer.useWorldSpace = true;
            return renderer;
        }

        public void Present(HandGestureSignal signal, Pose pinchPose, bool tracked)
        {
            if (!tracked || game == null || !game.ViewCamera)
            {
                Hide();
                previousState = signal.State;
                return;
            }

            if (signal.State == HandGestureState.Armed && previousState != HandGestureState.Armed)
                anchor = pinchPose.position;
            if (signal.Command != GameCommand.None)
            {
                anchor = pinchPose.position;
                pulseUntil = Time.unscaledTime + .08f;
            }
            if (signal.CandidateCancelled)
                game.PlayInputFeedback(FeedbackKind.Ui, pinchPose.position);

            bool showDirections = signal.State == HandGestureState.Armed ||
                Time.unscaledTime < pulseUntil;
            for (int i = 0; i < arms.Length; i++) arms[i].enabled = showDirections;
            if (showDirections) DrawDirections(signal);

            bool showHold = signal.State == HandGestureState.PhaseHolding ||
                signal.State == HandGestureState.PauseHolding;
            ring.enabled = showHold;
            if (showHold) DrawRing(pinchPose.position, signal.HoldProgress);
            previousState = signal.State;
        }

        void DrawDirections(HandGestureSignal signal)
        {
            Transform view = game.ViewCamera.transform;
            Vector3[] directions = { -view.right, view.right, view.up, -view.up };
            int selected = Mathf.Abs(signal.Displacement.x) > Mathf.Abs(signal.Displacement.y)
                ? signal.Displacement.x < 0f ? 0 : 1
                : signal.Displacement.y > 0f ? 2 : 3;
            float strength = Mathf.Clamp01(signal.Displacement.magnitude / .06f);
            for (int i = 0; i < arms.Length; i++)
            {
                arms[i].SetPosition(0, anchor + directions[i] * .012f);
                arms[i].SetPosition(1, anchor + directions[i] * .055f);
                Color color = i == selected
                    ? Color.Lerp(new Color(.2f, .55f, .7f, .65f), new Color(.3f, 1f, 1f, 1f), strength)
                    : new Color(.2f, .55f, .7f, .45f);
                arms[i].startColor = arms[i].endColor = color;
            }
        }

        void DrawRing(Vector3 center, float progress)
        {
            Transform view = game.ViewCamera.transform;
            int visible = Mathf.Clamp(Mathf.CeilToInt(RingSegments * progress), 1, RingSegments);
            ring.positionCount = visible + 1;
            ring.loop = progress >= .999f;
            for (int i = 0; i <= visible; i++)
            {
                float angle = i / (float)RingSegments * Mathf.PI * 2f;
                ring.SetPosition(i, center + (view.right * Mathf.Cos(angle) + view.up * Mathf.Sin(angle)) * .035f);
            }
            ring.startColor = ring.endColor = new Color(.3f, 1f, 1f, .9f);
        }

        void Hide()
        {
            foreach (LineRenderer arm in arms) if (arm) arm.enabled = false;
            if (ring) ring.enabled = false;
        }

        void OnDestroy()
        {
            if (material) Destroy(material);
        }
    }
}
