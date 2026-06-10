using Unity.Netcode;
using UnityEngine;

namespace XRMultiplayer.PPE
{
    /// <summary>
    /// Trigger zone on the local trainee avatar that equips a matching PPE item when it enters.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class PPEWearZone : MonoBehaviour
    {
        [SerializeField] PPEItemType m_AcceptedItemType;
        [SerializeField] PPEAvatarEquipmentAnchors m_AvatarAnchors;

        Collider m_TriggerCollider;
        NetworkObject m_PlayerNetworkObject;

        void Awake()
        {
            m_TriggerCollider = GetComponent<Collider>();
            m_TriggerCollider.isTrigger = true;

            if (m_AvatarAnchors == null)
                m_AvatarAnchors = GetComponentInParent<PPEAvatarEquipmentAnchors>();

            if (m_AvatarAnchors != null)
                m_PlayerNetworkObject = m_AvatarAnchors.GetComponent<NetworkObject>();
        }

        void OnTriggerEnter(Collider other)
        {
            TryEquipFromCollider(other);
        }

        void OnTriggerStay(Collider other)
        {
            TryEquipFromCollider(other);
        }

        void TryEquipFromCollider(Collider other)
        {
            if (m_PlayerNetworkObject == null || !m_PlayerNetworkObject.IsSpawned || !m_PlayerNetworkObject.IsOwner)
                return;

            PPEWearableItem item = other.GetComponentInParent<PPEWearableItem>();
            if (item == null || item.itemType != m_AcceptedItemType || item.isEquipped)
                return;

            item.TryEquip(m_PlayerNetworkObject.OwnerClientId);
        }
    }
}
