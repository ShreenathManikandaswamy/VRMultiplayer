using System;
using Unity.Netcode;
using UnityEngine;

namespace XRMultiplayer.PPE
{
    /// <summary>
    /// Per-player registry of transforms where equipped PPE visuals should snap.
    /// </summary>
    [DisallowMultipleComponent]
    public class PPEAvatarEquipmentAnchors : NetworkBehaviour
    {
        [SerializeField] Transform m_CoatAnchor;
        [SerializeField] Transform m_CapAnchor;
        [SerializeField] Transform m_GlassesAnchor;
        [SerializeField] Transform m_LeftGloveAnchor;
        [SerializeField] Transform m_RightGloveAnchor;

        public bool TryGetAnchor(PPEItemType itemType, out Transform anchor)
        {
            anchor = itemType switch
            {
                PPEItemType.Coat => m_CoatAnchor,
                PPEItemType.Cap => m_CapAnchor,
                PPEItemType.Glasses => m_GlassesAnchor,
                PPEItemType.LeftGlove => m_LeftGloveAnchor,
                PPEItemType.RightGlove => m_RightGloveAnchor,
                _ => null
            };

            return anchor != null;
        }

        public static bool TryFindForClient(ulong ownerClientId, out PPEAvatarEquipmentAnchors anchors)
        {
            anchors = null;
            PPEAvatarEquipmentAnchors[] allAnchors = FindObjectsByType<PPEAvatarEquipmentAnchors>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (PPEAvatarEquipmentAnchors candidate in allAnchors)
            {
                if (candidate != null && candidate.IsSpawned && candidate.OwnerClientId == ownerClientId)
                {
                    anchors = candidate;
                    return true;
                }
            }

            return false;
        }

        void OnValidate()
        {
            if (m_CoatAnchor == null)
                m_CoatAnchor = FindByName("CoatAnchor", "JacketAnchor", "BodyAnchor", "ChestAnchor");
            if (m_CapAnchor == null)
                m_CapAnchor = FindByName("CapAnchor", "HelmetAnchor", "HeadAnchor");
            if (m_GlassesAnchor == null)
                m_GlassesAnchor = FindByName("GlassesAnchor", "FaceAnchor");
            if (m_LeftGloveAnchor == null)
                m_LeftGloveAnchor = FindByName("LeftGloveAnchor", "LeftHandAnchor");
            if (m_RightGloveAnchor == null)
                m_RightGloveAnchor = FindByName("RightGloveAnchor", "RightHandAnchor");
        }

        Transform FindByName(params string[] names)
        {
            foreach (string childName in names)
            {
                Transform child = FindChildRecursive(transform, childName);
                if (child != null)
                    return child;
            }

            return null;
        }

        static Transform FindChildRecursive(Transform root, string name)
        {
            if (root == null)
                return null;

            if (string.Equals(root.name, name, StringComparison.Ordinal))
                return root;

            foreach (Transform child in root)
            {
                Transform result = FindChildRecursive(child, name);
                if (result != null)
                    return result;
            }

            return null;
        }
    }
}
