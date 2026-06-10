using Unity.Netcode;
using UnityEngine;
using XRMultiplayer.PPE;

namespace XRMultiplayer.Assessment
{
    /// <summary>
    /// Networked checklist state for the trainee's required PPE.
    /// </summary>
    [DisallowMultipleComponent]
    public class PPETraineeAssessmentState : NetworkBehaviour
    {
        readonly NetworkVariable<bool> m_CoatEquipped = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<bool> m_CapEquipped = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<bool> m_GlassesEquipped = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<bool> m_LeftGloveEquipped = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<bool> m_RightGloveEquipped = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public bool coatEquipped => m_CoatEquipped.Value;
        public bool capEquipped => m_CapEquipped.Value;
        public bool glassesEquipped => m_GlassesEquipped.Value;
        public bool leftGloveEquipped => m_LeftGloveEquipped.Value;
        public bool rightGloveEquipped => m_RightGloveEquipped.Value;
        public bool glovesEquipped => m_LeftGloveEquipped.Value && m_RightGloveEquipped.Value;
        public bool allRequiredPpeEquipped => m_CoatEquipped.Value && m_CapEquipped.Value && m_GlassesEquipped.Value && glovesEquipped;

        public static bool TryFindForClient(ulong ownerClientId, out PPETraineeAssessmentState assessmentState)
        {
            assessmentState = null;
            PPETraineeAssessmentState[] allStates = FindObjectsByType<PPETraineeAssessmentState>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (PPETraineeAssessmentState candidate in allStates)
            {
                if (candidate != null && candidate.IsSpawned && candidate.OwnerClientId == ownerClientId)
                {
                    assessmentState = candidate;
                    return true;
                }
            }

            return false;
        }

        public void SetEquipped(PPEItemType itemType, bool equipped)
        {
            if (!IsOwner)
                return;

            switch (itemType)
            {
                case PPEItemType.Coat:
                    m_CoatEquipped.Value = equipped;
                    break;
                case PPEItemType.Cap:
                    m_CapEquipped.Value = equipped;
                    break;
                case PPEItemType.Glasses:
                    m_GlassesEquipped.Value = equipped;
                    break;
                case PPEItemType.LeftGlove:
                    m_LeftGloveEquipped.Value = equipped;
                    break;
                case PPEItemType.RightGlove:
                    m_RightGloveEquipped.Value = equipped;
                    break;
            }
        }
    }
}
