using UnityEngine;

namespace XRMultiplayer
{
    /// <summary>
    /// Drives humanoid avatar rig targets from the existing networked XR player head and hand transforms.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkHumanoidRigBinder : MonoBehaviour
    {
        [SerializeField] XRINetworkPlayer m_NetworkPlayer;
        [SerializeField] Transform m_VisualRoot;

        [Header("IK Targets")]
        [SerializeField] Transform m_HeadTarget;
        [SerializeField] Transform m_LeftHandTarget;
        [SerializeField] Transform m_RightHandTarget;

        [Header("Hand Rotation Offsets")]
        [SerializeField] Vector3 m_LeftHandRotationOffsetEuler;
        [SerializeField] Vector3 m_RightHandRotationOffsetEuler;

        [Header("Hand Position Offsets")]
        [SerializeField] Vector3 m_LeftHandPositionOffset;
        [SerializeField] Vector3 m_RightHandPositionOffset;

        [Header("Body Follow")]
        [SerializeField] float m_HeadToRootHeight = 1.55f;
        [SerializeField] float m_BodyYawSmoothing = 12.0f;

        void Reset()
        {
            m_NetworkPlayer = GetComponent<XRINetworkPlayer>();
        }

        void Awake()
        {
            if (m_NetworkPlayer == null)
                m_NetworkPlayer = GetComponent<XRINetworkPlayer>();
        }

        void LateUpdate()
        {
            if (m_NetworkPlayer == null)
                return;

            UpdateTarget(m_HeadTarget, m_NetworkPlayer.head, Vector3.zero);
            UpdateTarget(m_LeftHandTarget, m_NetworkPlayer.leftHand, m_LeftHandPositionOffset, m_LeftHandRotationOffsetEuler);
            UpdateTarget(m_RightHandTarget, m_NetworkPlayer.rightHand, m_RightHandPositionOffset, m_RightHandRotationOffsetEuler);
            UpdateVisualRoot();
        }

        void UpdateTarget(Transform target, Transform source, Vector3 rotationOffsetEuler)
        {
            UpdateTarget(target, source, Vector3.zero, rotationOffsetEuler);
        }

        void UpdateTarget(Transform target, Transform source, Vector3 positionOffset, Vector3 rotationOffsetEuler)
        {
            if (target == null || source == null)
                return;

            Vector3 targetPosition = source.TransformPoint(positionOffset);
            Quaternion targetRotation = ApplyLocalAxisRotationOffset(source, rotationOffsetEuler);
            target.SetPositionAndRotation(targetPosition, targetRotation);
        }

        static Quaternion ApplyLocalAxisRotationOffset(Transform source, Vector3 rotationOffsetEuler)
        {
            Quaternion rotation = source.rotation;
            rotation = Quaternion.AngleAxis(rotationOffsetEuler.x, source.right) * rotation;
            rotation = Quaternion.AngleAxis(rotationOffsetEuler.y, source.up) * rotation;
            rotation = Quaternion.AngleAxis(rotationOffsetEuler.z, source.forward) * rotation;
            return rotation;
        }

        void UpdateVisualRoot()
        {
            if (m_VisualRoot == null || m_NetworkPlayer.head == null)
                return;

            Vector3 headPosition = m_NetworkPlayer.head.position;
            m_VisualRoot.position = new Vector3(headPosition.x, headPosition.y - m_HeadToRootHeight, headPosition.z);

            Vector3 forward = Vector3.ProjectOnPlane(m_NetworkPlayer.head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
                return;

            Quaternion targetYaw = Quaternion.LookRotation(forward.normalized, Vector3.up);
            m_VisualRoot.rotation = Quaternion.Slerp(m_VisualRoot.rotation, targetYaw, Time.deltaTime * m_BodyYawSmoothing);
        }
    }
}
