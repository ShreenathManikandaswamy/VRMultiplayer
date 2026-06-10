using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.Networking;

#if HAS_MPPM
using UnityEngine.XR.Interaction.Toolkit.UI;
#endif

#if UNITY_EDITOR

#if HAS_PARRELSYNC
using ParrelSync;
#endif

#endif

namespace XRMultiplayer
{
    public class AuthenticationManager : MonoBehaviour
    {
        const string k_DebugPrepend = "<color=#938FFF>[Authentication Manager]</color> ";

        /// <summary>
        /// The argument ID to search for in the command line args.
        /// </summary>
        const string k_playerArgID = "PlayerArg";

        /// <summary>
        /// Determines if the AuthenticationManager should use command line args to determine the player ID when launching a build.
        /// </summary>
        [SerializeField] bool m_UseCommandLineArgs = true;

        /// <summary>
        /// Runs short network probes before authentication so Quest builds can report whether Unity service endpoints are reachable.
        /// </summary>
        [SerializeField] bool m_RunConnectivityDiagnostics = true;

        /// <summary>
        /// Endpoints used only for diagnostics. A protocol error still proves DNS/TLS/routing worked; connection errors are the key failure.
        /// </summary>
        [SerializeField]
        string[] m_DiagnosticUrls =
        {
            "https://services.api.unity.com",
            "https://player-auth.services.api.unity.com"
        };

#if HAS_MPPM

        const string k_MppmEditorName = "-name";
        const string k_MppmCloneProcess = "--virtual-project-clone";

        bool m_IsVirtualPlayer;

        /// <summary>
        /// The XRUIInputModule that is used to control the XRUI -- Cache this value and it with the MPPM virtual players.
        /// </summary>
        public XRUIInputModule inputModule
        {
            get
            {
                if (m_InputModule == null)
                {
                    m_InputModule = FindAnyObjectByType<XRUIInputModule>();
                }
                return m_InputModule;
            }
        }

        XRUIInputModule m_InputModule;
#endif

        /// <summary>
        /// Simple Authentication function. This uses bare bones authentication and anonymous sign in.
        /// </summary>
        /// <returns></returns>
        public virtual async Task<bool> Authenticate()
        {
            try
            {
                LogRuntimeDiagnostics("authentication start");
                await LogConnectivityDiagnostics("before Unity Services initialization");

                // Check if UGS has not been initialized yet, and initialize.
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    var options = new InitializationOptions();
                    string playerId = "Player";
                    // Check for editor clones (MPPM or ParrelSync).
                    // This allows for multiple instances of the editor to connect to UGS.
#if UNITY_EDITOR
                    playerId = "Editor";

#if HAS_MPPM
                    //Check for MPPM
                    playerId += CheckMPPM();
#elif HAS_PARRELSYNC
                    // Check for ParrelSync
                    playerId += CheckParrelSync();
#endif
#endif
                    // Check for command line args in builds
                    if (!Application.isEditor && m_UseCommandLineArgs)
                    {
                        playerId += GetPlayerIDArg();
                    }

                    playerId = SanitizeString(playerId);

                    Utils.Log($"{k_DebugPrepend}Signing in with profile {playerId}");
                    options.SetProfile(playerId);

                    // Initialize UGS using any options defined
                    Utils.Log($"{k_DebugPrepend}UnityServices.InitializeAsync starting.");
                    await UnityServices.InitializeAsync(options);
                    Utils.Log($"{k_DebugPrepend}UnityServices.InitializeAsync completed. State: {UnityServices.State}");
                }
                else
                {
                    Utils.Log($"{k_DebugPrepend}Unity Services already initialized. State: {UnityServices.State}");
                }

                // If not already signed on then do so.
                if (!AuthenticationService.Instance.IsAuthorized)
                {
                    // Signing in anonymously for simplicity sake.
                    Utils.Log($"{k_DebugPrepend}SignInAnonymouslyAsync starting.");
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    Utils.Log($"{k_DebugPrepend}SignInAnonymouslyAsync completed. PlayerId: {AuthenticationService.Instance.PlayerId}");
                }
                else
                {
                    Utils.Log($"{k_DebugPrepend}Authentication service already authorized. PlayerId: {AuthenticationService.Instance.PlayerId}");
                }

                // Cache PlayerId.
                XRINetworkGameManager.AuthenicationId = AuthenticationService.Instance.PlayerId;
                return UnityServices.State == ServicesInitializationState.Initialized;
            }
            catch (RequestFailedException e)
            {
                Utils.Log($"{k_DebugPrepend}Unity Services request failed. Type: {e.GetType().FullName}, ErrorCode: {e.ErrorCode}, Message: {e.Message}", 2);
                if (e.InnerException != null)
                {
                    Utils.Log($"{k_DebugPrepend}Inner exception. Type: {e.InnerException.GetType().FullName}, Message: {e.InnerException.Message}", 2);
                }

                LogRuntimeDiagnostics("authentication failure");
                await LogConnectivityDiagnostics("after authentication failure");
                Utils.Log($"{k_DebugPrepend}Error during authentication: {e}", 2);
                return false;
            }
            catch (System.Exception e)
            {
                Utils.Log($"{k_DebugPrepend}Unexpected authentication failure. Type: {e.GetType().FullName}, Message: {e.Message}", 2);
                if (e.InnerException != null)
                {
                    Utils.Log($"{k_DebugPrepend}Inner exception. Type: {e.InnerException.GetType().FullName}, Message: {e.InnerException.Message}", 2);
                }

                LogRuntimeDiagnostics("authentication failure");
                await LogConnectivityDiagnostics("after authentication failure");
                Utils.Log($"{k_DebugPrepend}Error during authentication: {e}", 2);
                return false;
            }
        }

        public static bool IsAuthenticated()
        {
            try
            {
                return AuthenticationService.Instance.IsSignedIn;
            }
            catch (Exception e)
            {
                Utils.Log($"{k_DebugPrepend}Checking for AuthenticationService.Instance before initialized.{e}");
                return false;
            }
        }

        static string GetPlayerIDArg()
        {
            string playerID = "";
            string[] args = Environment.GetCommandLineArgs();
            foreach (string arg in args)
            {
                if (arg.ToLower().Contains(k_playerArgID.ToLower()))
                {
                    var splitArgs = arg.Split(':');
                    if (splitArgs.Length > 0)
                    {
                        playerID += splitArgs[1];
                    }
                }
            }
            return playerID;
        }

        static string SanitizeString(string playerId)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                return string.Empty;
            }

            // Define the regular expression pattern to match characters that are NOT alphanumeric, dash, or underscore.
            // \w matches [a-zA-Z0-9_] (alphanumeric and underscore)
            // \- matches the literal hyphen
            // The caret ^ inside the character set [] negates the set, matching anything NOT in the set.
            string pattern = @"[^\w-]";
            return Regex.Replace(playerId, pattern, "");
        }

        void LogRuntimeDiagnostics(string stage)
        {
            Utils.Log($"{k_DebugPrepend}Auth diagnostics ({stage}) " +
                      $"platform={Application.platform}, " +
                      $"identifier={Application.identifier}, " +
                      $"unityServicesState={UnityServices.State}, " +
                      $"internetReachability={Application.internetReachability}, " +
                      $"cloudProjectId={Application.cloudProjectId}, " +
                      $"company={Application.companyName}, " +
                      $"product={Application.productName}, " +
                      $"isEditor={Application.isEditor}");
        }

        async Task LogConnectivityDiagnostics(string stage)
        {
            if (!m_RunConnectivityDiagnostics)
            {
                return;
            }

            for (int i = 0; i < m_DiagnosticUrls.Length; i++)
            {
                string url = m_DiagnosticUrls[i];
                if (string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                using UnityWebRequest request = UnityWebRequest.Get(url);
                request.timeout = 5;

                try
                {
                    UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                    while (!operation.isDone)
                    {
                        await Task.Yield();
                    }

                    Utils.Log($"{k_DebugPrepend}Connectivity probe ({stage}) url={url}, result={request.result}, responseCode={request.responseCode}, error={request.error}");
                }
                catch (Exception e)
                {
                    Utils.Log($"{k_DebugPrepend}Connectivity probe exception ({stage}) url={url}, type={e.GetType().FullName}, message={e.Message}", 1);
                }
            }
        }

#if UNITY_EDITOR
#if HAS_MPPM
        string CheckMPPM()
        {
            Utils.Log($"{k_DebugPrepend}MPPM Found");
            string mppmString = "";

            var arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length; ++i)
            {
                if (arguments[i] == k_MppmCloneProcess)
                {
                    m_IsVirtualPlayer = true;
                    inputModule.enableMouseInput = false;
                    inputModule.enableTouchInput = false;
                }
                if (arguments[i] == k_MppmEditorName && (i + 1) < arguments.Length)
                {
                    mppmString += arguments[i + 1];
                }
            }

            if (m_IsVirtualPlayer && string.IsNullOrEmpty(mppmString))
            {
                Utils.LogWarning("An MPPM virtual player was detected, but the Player Name was not set. This may cause authentication failures when trying to connect.");
            }

            return mppmString;
        }

        // This prevents the XRUIInputModule from throwing errors when MPPM is active but focus on the editor has not happened yet.
        void OnApplicationFocus(bool focus)
        {
            // Check to make sure it's an MPPM Virtual Player.
            if (focus && m_IsVirtualPlayer)
            {
                inputModule.enableMouseInput = true;
                inputModule.enableTouchInput = true;
            }
        }
#endif

#if HAS_PARRELSYNC
        string CheckParrelSync()
        {
            Utils.Log($"{k_DebugPrepend}ParrelSync Found");
            string pSyncString = "";
            if (ClonesManager.IsClone()) pSyncString += ClonesManager.GetArgument();
            return pSyncString;
        }
#endif
#endif
    }
}
