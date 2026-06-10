using UnityEngine;
using TMPro;


namespace XRMultiplayer
{
    /// <summary>
    /// Keeps role join buttons hidden while joining/connected and restores them after failure or disconnect.
    /// </summary>
    public class RoleJoinButtonsVisibility : MonoBehaviour
    {
        [SerializeField] GameObject m_JoinAsTraineeButton;
        [SerializeField] GameObject m_JoinAsSafetyOfficerButton;
        [SerializeField] GameObject m_LoadingStatusObject;
        [SerializeField] TMP_Text m_LoadingStatusText;
        [SerializeField] string m_InitialJoiningMessage = "Joining room...";

        void OnEnable()
        {
            XRINetworkGameManager.CurrentConnectionState.Subscribe(ConnectionStateUpdated);
            XRINetworkGameManager.Connected.Subscribe(ConnectedUpdated);
            SessionManager.status.Subscribe(ConnectionUpdated);

            if (XRINetworkGameManager.Instance != null)
            {
                XRINetworkGameManager.Instance.OnConnectionFailedAction += ConnectionFailed;
                XRINetworkGameManager.Instance.OnConnectionUpdated += ConnectionUpdated;
            }

            Refresh();
        }

        void OnDisable()
        {
            XRINetworkGameManager.CurrentConnectionState.Unsubscribe(ConnectionStateUpdated);
            XRINetworkGameManager.Connected.Unsubscribe(ConnectedUpdated);
            SessionManager.status.Unsubscribe(ConnectionUpdated);

            if (XRINetworkGameManager.Instance != null)
            {
                XRINetworkGameManager.Instance.OnConnectionFailedAction -= ConnectionFailed;
                XRINetworkGameManager.Instance.OnConnectionUpdated -= ConnectionUpdated;
            }
        }

        public void JoinAsTrainee()
        {
            BeginJoining("Joining as Trainee...");
            XRINetworkGameManager.Instance.QuickJoinLobbyAsTrainee();
        }

        public void JoinAsSafetyOfficer()
        {
            BeginJoining("Joining as Safety Officer...");
            XRINetworkGameManager.Instance.QuickJoinLobbyAsSafetyOfficer();
        }

        public void HideJoinButtons()
        {
            SetJoinButtonsVisible(false);
        }

        public void ShowJoinButtons()
        {
            SetJoinButtonsVisible(true);
        }

        void ConnectionStateUpdated(XRINetworkGameManager.ConnectionState connectionState)
        {
            bool shouldShow = connectionState == XRINetworkGameManager.ConnectionState.None ||
                              connectionState == XRINetworkGameManager.ConnectionState.Authenticated;

            SetJoinButtonsVisible(shouldShow && !XRINetworkGameManager.Connected.Value);
            SetLoadingVisible(connectionState == XRINetworkGameManager.ConnectionState.Connecting);

            if (connectionState == XRINetworkGameManager.ConnectionState.Authenticating)
                SetLoadingText("Authenticating...");
            else if (connectionState == XRINetworkGameManager.ConnectionState.Connecting)
                SetLoadingText(m_InitialJoiningMessage);
        }

        void ConnectedUpdated(bool connected)
        {
            if (connected)
            {
                SetJoinButtonsVisible(false);
                SetLoadingVisible(false);
                return;
            }

            Refresh();
        }

        void ConnectionFailed(string reason)
        {
            SetLoadingVisible(false);
            SetJoinButtonsVisible(true);
        }

        void ConnectionUpdated(string status)
        {
            if (XRINetworkGameManager.CurrentConnectionState.Value != XRINetworkGameManager.ConnectionState.Connecting)
                return;

            SetLoadingVisible(true);
            SetLoadingText(status);
        }

        void BeginJoining(string status)
        {
            SetJoinButtonsVisible(false);
            SetLoadingVisible(true);
            SetLoadingText(status);
        }

        void Refresh()
        {
            ConnectionStateUpdated(XRINetworkGameManager.CurrentConnectionState.Value);
        }

        void SetJoinButtonsVisible(bool visible)
        {
            if (m_JoinAsTraineeButton != null)
                m_JoinAsTraineeButton.SetActive(visible);

            if (m_JoinAsSafetyOfficerButton != null)
                m_JoinAsSafetyOfficerButton.SetActive(visible);
        }

        void SetLoadingVisible(bool visible)
        {
            if (m_LoadingStatusObject != null)
                m_LoadingStatusObject.SetActive(visible);
        }

        void SetLoadingText(string status)
        {
            if (m_LoadingStatusText != null && !string.IsNullOrEmpty(status))
                m_LoadingStatusText.text = status;
        }
    }
}
