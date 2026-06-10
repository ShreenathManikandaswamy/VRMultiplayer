using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRMultiplayer.Assessment;

namespace XRMultiplayer.PPE
{
    /// <summary>
    /// Networked state for a grabbable PPE object that can become worn on a player avatar.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public class PPEWearableItem : NetworkBehaviour
    {
        [SerializeField] PPEItemType m_ItemType;
        [SerializeField] Vector3 m_EquippedLocalPositionOffset;
        [SerializeField] Vector3 m_EquippedLocalRotationOffsetEuler;
        [SerializeField] bool m_DisableInteractionWhenEquipped = true;

        readonly NetworkVariable<bool> m_IsEquipped = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<ulong> m_WearerClientId = new(ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        Rigidbody m_Rigidbody;
        Collider[] m_Colliders;
        XRBaseInteractable[] m_Interactables;
        Transform m_CurrentAnchor;

        public PPEItemType itemType => m_ItemType;
        public bool isEquipped => m_IsEquipped.Value;
        public ulong wearerClientId => m_WearerClientId.Value;

        void Awake()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
            m_Colliders = GetComponentsInChildren<Collider>(true);
            m_Interactables = GetComponentsInChildren<XRBaseInteractable>(true);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            m_IsEquipped.OnValueChanged += OnEquippedChanged;
            m_WearerClientId.OnValueChanged += OnWearerChanged;
            RefreshEquippedState();
        }

        public override void OnNetworkDespawn()
        {
            m_IsEquipped.OnValueChanged -= OnEquippedChanged;
            m_WearerClientId.OnValueChanged -= OnWearerChanged;
            base.OnNetworkDespawn();
        }

        void LateUpdate()
        {
            if (!m_IsEquipped.Value)
                return;

            if (m_CurrentAnchor == null)
                ResolveAnchor();

            if (m_CurrentAnchor == null)
                return;

            transform.SetPositionAndRotation(
                m_CurrentAnchor.TransformPoint(m_EquippedLocalPositionOffset),
                m_CurrentAnchor.rotation * Quaternion.Euler(m_EquippedLocalRotationOffsetEuler));
        }

        public bool TryEquip(ulong wearerClientId)
        {
            if (!IsSpawned || m_IsEquipped.Value)
                return false;

            if (!IsOwner)
            {
                NetworkObject.ChangeOwnership(NetworkManager.Singleton.LocalClientId);
                return false;
            }

            m_WearerClientId.Value = wearerClientId;
            m_IsEquipped.Value = true;
            return true;
        }

        public bool TryUnequip()
        {
            if (!IsSpawned || !m_IsEquipped.Value || !IsOwner)
                return false;

            m_IsEquipped.Value = false;
            m_WearerClientId.Value = ulong.MaxValue;
            return true;
        }

        void OnEquippedChanged(bool previousValue, bool currentValue)
        {
            RefreshEquippedState();
            UpdateAssessmentState(currentValue);
        }

        void OnWearerChanged(ulong previousValue, ulong currentValue)
        {
            m_CurrentAnchor = null;
            ResolveAnchor();
        }

        void RefreshEquippedState()
        {
            if (m_IsEquipped.Value)
            {
                ResolveAnchor();
            }

            SetPhysicsEnabled(!m_IsEquipped.Value);
            SetInteractionEnabled(!m_IsEquipped.Value || !m_DisableInteractionWhenEquipped);
        }

        void ResolveAnchor()
        {
            m_CurrentAnchor = null;
            if (m_WearerClientId.Value == ulong.MaxValue)
                return;

            if (PPEAvatarEquipmentAnchors.TryFindForClient(m_WearerClientId.Value, out PPEAvatarEquipmentAnchors anchors))
            {
                anchors.TryGetAnchor(m_ItemType, out m_CurrentAnchor);
            }
        }

        void UpdateAssessmentState(bool equipped)
        {
            if (m_WearerClientId.Value == ulong.MaxValue)
                return;

            if (!PPETraineeAssessmentState.TryFindForClient(m_WearerClientId.Value, out PPETraineeAssessmentState assessmentState))
                return;

            assessmentState.SetEquipped(m_ItemType, equipped);
        }

        void SetPhysicsEnabled(bool enabled)
        {
            if (m_Rigidbody != null)
            {
                m_Rigidbody.isKinematic = !enabled;
                if (!enabled)
                {
                    m_Rigidbody.linearVelocity = Vector3.zero;
                    m_Rigidbody.angularVelocity = Vector3.zero;
                }
            }

            foreach (Collider itemCollider in m_Colliders)
            {
                if (itemCollider != null)
                    itemCollider.enabled = enabled;
            }
        }

        void SetInteractionEnabled(bool enabled)
        {
            foreach (XRBaseInteractable interactable in m_Interactables)
            {
                if (interactable != null)
                    interactable.enabled = enabled;
            }
        }
    }
}
