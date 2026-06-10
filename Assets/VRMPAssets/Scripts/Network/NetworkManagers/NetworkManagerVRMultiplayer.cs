using Unity.Netcode;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace XRMultiplayer
{
    /// <summary>
    /// Manages the network functionality for VR multiplayer.
    /// </summary>
    public class NetworkManagerVRMultiplayer : NetworkManager
    {
        public static NetworkManagerVRMultiplayer Instance { get; private set; }

        [SerializeField, Tooltip("Set this to control how much logging is generated")]
        LogLevel m_LogLevel;

        [SerializeField, Tooltip("This should almost always be set to true")]
        bool m_RunInBackground = true;

        [SerializeField]
        NetworkConfig m_NetworkConfig;

        [Header("Role Player Prefabs")]
        [SerializeField] GameObject m_TraineePlayerPrefab;
        [SerializeField] GameObject m_SafetyOfficerPlayerPrefab;

        ///<inheritdoc/>
        void Awake()
        {
            Instance = this;
            LogLevel = m_LogLevel;
            RunInBackground = m_RunInBackground;
            NetworkConfig = m_NetworkConfig;
            Utils.s_LogLevel = LogLevel;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public bool ApplyPlayerRolePrefab(XRPlayerRole role)
        {
            if (IsListening)
            {
                Utils.LogWarning($"Cannot change player prefab while the NetworkManager is listening. Disconnect before changing role.");
                return false;
            }

            GameObject selectedPrefab = role switch
            {
                XRPlayerRole.Trainee => m_TraineePlayerPrefab,
                XRPlayerRole.SafetyOfficer => m_SafetyOfficerPlayerPrefab,
                _ => m_TraineePlayerPrefab
            };

            if (selectedPrefab == null)
            {
                Utils.LogWarning($"No player prefab assigned for role {role}. Keeping current NetworkConfig.PlayerPrefab.");
                return false;
            }

            NetworkConfig.PlayerPrefab = selectedPrefab;
            NetworkConfig.ForceSamePrefabs = false;
            Utils.Log($"Selected player role {role}. Player prefab set to {selectedPrefab.name}.");
            return true;
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(NetworkManagerVRMultiplayer))]
    class VRMutliplayerTemplateNetworkManagerEditor : Editor
    {
        /// <summary>
        /// This function is called when the inspector is drawn.
        /// </summary>
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (Application.isPlaying)
            {
                switch (XRINetworkGameManager.CurrentConnectionState.Value)
                {
                    case XRINetworkGameManager.ConnectionState.None:
                        GUILayout.Box("Authenticating");
                        break;
                    case XRINetworkGameManager.ConnectionState.Authenticating:
                        GUILayout.Box("Authenticating");
                        break;
                    case XRINetworkGameManager.ConnectionState.Authenticated:
                        if (GUILayout.Button("Connect"))
                        {
                            XRINetworkGameManager.Instance.QuickJoinLobby();
                        }
                        break;
                    case XRINetworkGameManager.ConnectionState.Connecting:
                        GUILayout.Box("Connecting");
                        break;
                    case XRINetworkGameManager.ConnectionState.Connected:
                        if (GUILayout.Button("Disconnect"))
                        {
                            XRINetworkGameManager.Instance.Disconnect();
                        }
                        break;
                }
            }
            else
            {
                GUILayout.Box("Game not running.");
            }
        }
    }
#endif
}
