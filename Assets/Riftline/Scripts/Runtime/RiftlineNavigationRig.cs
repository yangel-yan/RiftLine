using UnityEngine;

namespace Riftline
{
    [DisallowMultipleComponent]
    public sealed class RiftlineNavigationRig : MonoBehaviour
    {
        public Transform TrackingOrigin;
        float calibratedYaw;
        public float Heading { get; private set; }
        public Quaternion WorldFrameRotation => Quaternion.Euler(0, calibratedYaw, 0);

        public void SetHeading(float heading)
        {
            Heading = heading;
            transform.localRotation = Quaternion.Euler(0, calibratedYaw + heading, 0);
        }

        public void Calibrate(Transform head)
        {
            if (!head || !TrackingOrigin) return;
            Vector3 originPosition = TrackingOrigin.position;
            Quaternion originRotation = TrackingOrigin.rotation;
            Vector3 point = transform.parent.InverseTransformPoint(head.position);
            Vector3 direction = transform.parent.InverseTransformDirection(head.forward);
            calibratedYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg - Heading;
            transform.localPosition = new Vector3(point.x, 0, point.z);
            SetHeading(Heading);
            TrackingOrigin.SetPositionAndRotation(originPosition, originRotation);
        }

        public void ResetHeadingPreservingTracking()
        {
            Vector3 position = TrackingOrigin.position;
            Quaternion rotation = TrackingOrigin.rotation;
            SetHeading(0);
            TrackingOrigin.SetPositionAndRotation(position, rotation);
        }
    }
}
