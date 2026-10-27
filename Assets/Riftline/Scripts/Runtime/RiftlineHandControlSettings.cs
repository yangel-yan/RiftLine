using UnityEngine;
using UnityEngine.XR.Hands.Gestures;

namespace Riftline
{
    [CreateAssetMenu(fileName = "RiftlineHandControlSettings",
        menuName = "Riftline/Hand Control Settings")]
    public sealed class RiftlineHandControlSettings : ScriptableObject
    {
        public HandGestureThresholds Thresholds = HandGestureThresholds.Default;
        public XRHandShape OpenShape;
        public XRHandShape FistShape;
        public XRHandPose PausePose;
    }
}
