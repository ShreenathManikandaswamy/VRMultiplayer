using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace XRMultiplayer
{
    /// <summary>
    /// Hides selected avatar skinned meshes for the owning player while keeping them visible for remote players.
    /// </summary>
    public class LocalOwnerSkinnedMeshVisibility : NetworkBehaviour
    {
        [SerializeField, Tooltip("Skinned meshes to hide for the local owner. If empty, child skinned meshes are collected on Awake.")]
        SkinnedMeshRenderer[] m_Renderers;

        [SerializeField, Tooltip("Keep the body visible when the object is not spawned by Netcode, useful while previewing prefabs.")]
        bool m_ShowWhenNotSpawned = true;

        void Awake()
        {
            if (m_Renderers == null || m_Renderers.Length == 0)
            {
                SkinnedMeshRenderer[] childRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
                List<SkinnedMeshRenderer> visibleRenderers = new();

                foreach (SkinnedMeshRenderer renderer in childRenderers)
                {
                    if (renderer != null && renderer.enabled)
                    {
                        visibleRenderers.Add(renderer);
                    }
                }

                m_Renderers = visibleRenderers.ToArray();
            }

            if (!IsSpawned)
            {
                SetRenderersVisible(m_ShowWhenNotSpawned);
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            SetRenderersVisible(!IsOwner);
        }

        public override void OnGainedOwnership()
        {
            base.OnGainedOwnership();
            SetRenderersVisible(false);
        }

        public override void OnLostOwnership()
        {
            base.OnLostOwnership();
            SetRenderersVisible(true);
        }

        public override void OnNetworkDespawn()
        {
            SetRenderersVisible(m_ShowWhenNotSpawned);
            base.OnNetworkDespawn();
        }

        void SetRenderersVisible(bool visible)
        {
            if (m_Renderers == null)
                return;

            foreach (SkinnedMeshRenderer renderer in m_Renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
        }
    }
}
