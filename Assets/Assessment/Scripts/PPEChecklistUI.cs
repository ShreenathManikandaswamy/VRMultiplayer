using UnityEngine;
using UnityEngine.UI;

namespace XRMultiplayer.Assessment
{
    /// <summary>
    /// Binds a simple Safety Officer checklist UI to the first remote trainee PPE state it finds.
    /// </summary>
    public class PPEChecklistUI : MonoBehaviour
    {
        [SerializeField] PPETraineeAssessmentState m_AssessmentState;
        [SerializeField] Toggle m_CoatToggle;
        [SerializeField] Toggle m_CapToggle;
        [SerializeField] Toggle m_GlassesToggle;
        [SerializeField] Toggle m_GlovesToggle;

        void Update()
        {
            if (m_AssessmentState == null || !m_AssessmentState.IsSpawned)
                m_AssessmentState = FindAssessmentState();

            if (m_AssessmentState == null)
                return;

            SetToggle(m_CoatToggle, m_AssessmentState.coatEquipped);
            SetToggle(m_CapToggle, m_AssessmentState.capEquipped);
            SetToggle(m_GlassesToggle, m_AssessmentState.glassesEquipped);
            SetToggle(m_GlovesToggle, m_AssessmentState.glovesEquipped);
        }

        static PPETraineeAssessmentState FindAssessmentState()
        {
            PPETraineeAssessmentState[] states = FindObjectsByType<PPETraineeAssessmentState>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (PPETraineeAssessmentState state in states)
            {
                if (state != null && state.IsSpawned && !state.IsOwner)
                    return state;
            }

            return states.Length > 0 ? states[0] : null;
        }

        static void SetToggle(Toggle toggle, bool value)
        {
            if (toggle != null && toggle.isOn != value)
                toggle.SetIsOnWithoutNotify(value);
        }
    }
}
