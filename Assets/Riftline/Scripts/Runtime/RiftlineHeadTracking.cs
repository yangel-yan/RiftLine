using UnityEngine;
using UnityEngine.InputSystem.XR;

namespace Riftline
{
    [RequireComponent(typeof(TrackedPoseDriver))]
    public sealed class RiftlineHeadTracking : MonoBehaviour
    {
        const float StablePoseSeconds = .15f;
        const float MaximumPoseWait = .75f;
        const float PositionThresholdSqr = .000004f;
        const float RotationThreshold = .2f;

        TrackedPoseDriver driver;
        RiftlineGame game;
        Vector3 initialPosition;
        Quaternion initialRotation;
        Vector3 candidatePosition;
        Quaternion candidateRotation;
        float firstPoseAt = -1f;
        float stablePoseAt;
        bool poseObserved;

        void Awake()
        {
            driver = GetComponent<TrackedPoseDriver>();
            driver.enabled = true;
            game = GetComponentInParent<RiftlineGame>();
            initialPosition = transform.localPosition;
            initialRotation = transform.localRotation;
        }

        void LateUpdate()
        {
            if (poseObserved || !game || game.Phase == RunPhase.Running)
            {
                return;
            }

            bool poseChanged = (transform.localPosition - initialPosition).sqrMagnitude > .000001f ||
                Quaternion.Angle(transform.localRotation, initialRotation) > .1f;
            if (!poseChanged)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (firstPoseAt < 0f)
            {
                firstPoseAt = now;
                stablePoseAt = now;
                candidatePosition = transform.localPosition;
                candidateRotation = transform.localRotation;
                return;
            }

            bool candidateMoved = (transform.localPosition - candidatePosition).sqrMagnitude > PositionThresholdSqr ||
                Quaternion.Angle(transform.localRotation, candidateRotation) > RotationThreshold;
            if (candidateMoved)
            {
                candidatePosition = transform.localPosition;
                candidateRotation = transform.localRotation;
                stablePoseAt = now;
            }
            if (now - stablePoseAt < StablePoseSeconds && now - firstPoseAt < MaximumPoseWait)
            {
                return;
            }

            poseObserved = true;
            game.TryInitialAlignPresentationToHead();
        }
    }
}
