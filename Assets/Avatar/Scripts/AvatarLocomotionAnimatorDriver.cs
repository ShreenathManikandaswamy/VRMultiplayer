using UnityEngine;


namespace XRMultiplayer
{
    /// <summary>
    /// Drives a humanoid avatar locomotion blend parameter from horizontal avatar movement.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public class AvatarLocomotionAnimatorDriver : MonoBehaviour
    {
        [SerializeField] Animator m_Animator;
        [SerializeField] Transform m_MovementRoot;
        [SerializeField] string m_MoveSpeedParameter = "MoveSpeed";
        [SerializeField] bool m_DriveDirectionalParameters = true;
        [SerializeField] string m_MoveXParameter = "MoveX";
        [SerializeField] string m_MoveZParameter = "MoveZ";

        [Header("Speed Mapping")]
        [SerializeField, Min(0.01f)] float m_SpeedForFullWalk = 1.2f;
        [SerializeField, Min(0.0f)] float m_DeadZone = 0.05f;
        [SerializeField, Min(0.0f)] float m_DampTime = 0.12f;

        Vector3 m_LastPosition;
        int m_MoveSpeedParameterHash;
        int m_MoveXParameterHash;
        int m_MoveZParameterHash;
        bool m_HasLastPosition;

        void Reset()
        {
            m_Animator = GetComponentInChildren<Animator>();
            if (m_Animator != null)
            {
                m_MovementRoot = m_Animator.transform;
            }
        }

        void Awake()
        {
            if (m_Animator == null)
            {
                m_Animator = GetComponentInChildren<Animator>();
            }

            if (m_MovementRoot == null && m_Animator != null)
            {
                m_MovementRoot = m_Animator.transform;
            }

            m_MoveSpeedParameterHash = Animator.StringToHash(m_MoveSpeedParameter);
            m_MoveXParameterHash = Animator.StringToHash(m_MoveXParameter);
            m_MoveZParameterHash = Animator.StringToHash(m_MoveZParameter);
        }

        void OnEnable()
        {
            ResetLastPosition();
        }

        void LateUpdate()
        {
            if (m_Animator == null || m_MovementRoot == null)
            {
                return;
            }

            if (!m_HasLastPosition)
            {
                ResetLastPosition();
                SetMoveSpeed(0.0f);
                return;
            }

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0.0f)
            {
                return;
            }

            Vector3 currentPosition = m_MovementRoot.position;
            Vector3 delta = currentPosition - m_LastPosition;
            delta.y = 0.0f;

            float worldSpeed = delta.magnitude / deltaTime;
            float normalizedSpeed = worldSpeed < m_DeadZone ? 0.0f : Mathf.Clamp01(worldSpeed / m_SpeedForFullWalk);
            Vector3 localVelocity = Vector3.zero;

            if (normalizedSpeed > 0.0f)
            {
                Vector3 worldVelocity = delta / deltaTime;
                localVelocity = m_MovementRoot.InverseTransformDirection(worldVelocity);
                localVelocity.y = 0.0f;
                localVelocity /= m_SpeedForFullWalk;
                localVelocity = Vector3.ClampMagnitude(localVelocity, 1.0f);
            }

            SetMoveSpeed(normalizedSpeed);
            SetDirectionalMovement(localVelocity.x, localVelocity.z);
            m_LastPosition = currentPosition;
        }

        void ResetLastPosition()
        {
            if (m_MovementRoot == null)
            {
                m_HasLastPosition = false;
                return;
            }

            m_LastPosition = m_MovementRoot.position;
            m_HasLastPosition = true;
        }

        void SetMoveSpeed(float value)
        {
            m_Animator.SetFloat(m_MoveSpeedParameterHash, value, m_DampTime, Time.deltaTime);
        }

        void SetDirectionalMovement(float moveX, float moveZ)
        {
            if (!m_DriveDirectionalParameters)
            {
                return;
            }

            m_Animator.SetFloat(m_MoveXParameterHash, moveX, m_DampTime, Time.deltaTime);
            m_Animator.SetFloat(m_MoveZParameterHash, moveZ, m_DampTime, Time.deltaTime);
        }

        void OnValidate()
        {
            m_SpeedForFullWalk = Mathf.Max(0.01f, m_SpeedForFullWalk);
            m_DeadZone = Mathf.Max(0.0f, m_DeadZone);
            m_DampTime = Mathf.Max(0.0f, m_DampTime);
        }
    }
}
