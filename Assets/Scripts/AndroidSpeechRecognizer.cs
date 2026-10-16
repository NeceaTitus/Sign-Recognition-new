using System;
using System.Collections;
using TMPro;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using UnityEngine.Windows.Speech;
#endif

/// <summary>
/// Drives continuous speech dictation:
/// - On Android / Meta Quest: runs through Assets/Plugins/Android/SpeechRecognizerPlugin.java (ro-RO).
/// - On Windows / Unity Editor: captures the PC microphone live using UnityEngine.Windows.Speech.DictationRecognizer.
/// Implements ISpeechService to decouple STT from UI components.
/// </summary>
public class AndroidSpeechRecognizer : MonoBehaviour, ISpeechService
{
    private const string PluginClassName = "com.unisign.speech.SpeechRecognizerPlugin";

    [Header("Configuration")]
    [Tooltip("BCP-47 language tag for speech recognition (e.g. ro-RO, en-US). Used on Android.")]
    [SerializeField] private string language = "ro-RO";

    [Header("Optional Direct UI Bindings")]
    [Tooltip("Optional: Chat window to receive finalized text. Alternatively, subscribe to OnSpeechFinalized.")]
    [SerializeField] private ChatWindow spokenTextChatWindow;

    [Tooltip("Optional: updated live with in-progress speech before it's finalized. Leave empty to ignore partial results.")]
    [SerializeField] private TMP_Text liveCaptionText;

    [Header("Editor Testing")]
    [Tooltip("When in Unity Editor, pressing this key will simulate a speech recognition result.")]
    [SerializeField] private KeyCode editorSimulateKey = KeyCode.F2;
    [SerializeField] private string editorSamplePhrase = "Buna ziua, aceasta este o testare a recunoasterii vocale.";

    public string Language
    {
        get => language;
        set => language = value;
    }

    public bool IsMicrophoneEnabled { get; private set; } = true;

    // ISpeechService events
    public event Action<bool> OnMicrophoneStateChanged;
    public event Action<string> OnSpeechFinalized;
    public event Action<string> OnSpeechPartial;
    public event Action<string> OnSpeechErrorOccurred;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject _pluginInstance;
#elif UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    private DictationRecognizer _dictationRecognizer;
    private Coroutine _restartCoroutine;
#endif

    private void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            StartPlugin();
        }
        else
        {
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => StartPlugin();
            callbacks.PermissionDenied += _ =>
            {
                string msg = "Permisiunea RECORD_AUDIO a fost refuzata de utilizator.";
                Debug.LogError($"[AndroidSpeechRecognizer] {msg}");
                OnSpeechErrorOccurred?.Invoke(msg);
            };
            Permission.RequestUserPermission(Permission.Microphone, callbacks);
        }
#elif UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        Debug.Log($"[AndroidSpeechRecognizer] Rulare in Unity Editor pe Windows. Initializare microfon PC prin Windows Speech Recognition (tasta {editorSimulateKey} ramane disponibila ca fallback).");
        StartWindowsDictation();
#else
        Debug.Log($"[AndroidSpeechRecognizer] Rulare in Unity Editor (Limba: {language}). Apasa {editorSimulateKey} pentru a simula vorbirea.");
#endif
    }

#if UNITY_EDITOR
    private void Update()
    {
        if (Input.GetKeyDown(editorSimulateKey))
        {
            if (IsMicrophoneEnabled)
            {
                Debug.Log($"[AndroidSpeechRecognizer] Simulare vorbire in Editor (tasta {editorSimulateKey}): \"{editorSamplePhrase}\"");
                OnSpeechPartialResult_Internal("Buna ziua...");
                Invoke(nameof(SimulateFinish), 0.6f);
            }
            else
            {
                Debug.LogWarning("[AndroidSpeechRecognizer] Nu se poate simula: microfonul este oprit (Muted).");
            }
        }
    }

    private void SimulateFinish()
    {
        OnSpeechResult_Internal(editorSamplePhrase);
    }
#endif

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    private void StartWindowsDictation()
    {
        if (_dictationRecognizer == null)
        {
            try
            {
                _dictationRecognizer = new DictationRecognizer();
                _dictationRecognizer.AutoSilenceTimeoutSeconds = 4f;
                _dictationRecognizer.InitialSilenceTimeoutSeconds = 10f;

                _dictationRecognizer.DictationResult += (text, confidence) =>
                {
                    Debug.Log($"[Windows Speech PC] Recunoscut: \"{text}\" ({confidence})");
                    OnSpeechResult_Internal(text);
                };

                _dictationRecognizer.DictationHypothesis += (text) =>
                {
                    OnSpeechPartialResult_Internal(text);
                };

                _dictationRecognizer.DictationComplete += (cause) =>
                {
                    Debug.Log($"[Windows Speech PC] Sesiune finalizata ({cause}). Repornesc ascultarea continua...");
                    TriggerRestartWindowsDictation();
                };

                _dictationRecognizer.DictationError += (error, hresult) =>
                {
                    Debug.LogWarning($"[Windows Speech PC] Eroare ({hresult}): {error}");
                    OnSpeechErrorOccurred?.Invoke(error);
                    TriggerRestartWindowsDictation();
                };
            }
            catch (Exception ex)
            {
                string help = "Asigura-te ca in Windows: Settings -> Privacy -> Speech -> 'Online speech recognition' este activat.";
                Debug.LogError($"[Windows Speech PC] Nu s-a putut initializa DictationRecognizer: {ex.Message}.\n{help}");
                OnSpeechErrorOccurred?.Invoke(ex.Message);
                return;
            }
        }

        if (_dictationRecognizer.Status != SpeechSystemStatus.Running && IsMicrophoneEnabled)
        {
            try
            {
                _dictationRecognizer.Start();
                Debug.Log("[Windows Speech PC] Microfonul PC-ului asculta live! Vorbeste acum.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Windows Speech PC] Start() eroare: {ex.Message}");
            }
        }
    }

    private void TriggerRestartWindowsDictation()
    {
        if (!IsMicrophoneEnabled || !gameObject.activeInHierarchy)
        {
            return;
        }

        if (_restartCoroutine != null)
        {
            StopCoroutine(_restartCoroutine);
        }
        _restartCoroutine = StartCoroutine(RestartWindowsDictationRoutine());
    }

    private IEnumerator RestartWindowsDictationRoutine()
    {
        yield return new WaitForSeconds(0.3f);

        if (_dictationRecognizer != null && IsMicrophoneEnabled && _dictationRecognizer.Status != SpeechSystemStatus.Running)
        {
            try
            {
                _dictationRecognizer.Start();
                Debug.Log("[Windows Speech PC] Reascultare pornita.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Windows Speech PC] Restart esuat: {ex.Message}");
            }
        }

        _restartCoroutine = null;
    }

    private void StopWindowsDictation()
    {
        if (_restartCoroutine != null)
        {
            StopCoroutine(_restartCoroutine);
            _restartCoroutine = null;
        }

        if (_dictationRecognizer != null && _dictationRecognizer.Status == SpeechSystemStatus.Running)
        {
            try
            {
                _dictationRecognizer.Stop();
                Debug.Log("[Windows Speech PC] Microfonul PC a fost oprit.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Windows Speech PC] Stop() eroare: {ex.Message}");
            }
        }
    }

    private void CleanupWindowsDictation()
    {
        if (_restartCoroutine != null)
        {
            StopCoroutine(_restartCoroutine);
            _restartCoroutine = null;
        }

        if (_dictationRecognizer != null)
        {
            if (_dictationRecognizer.Status == SpeechSystemStatus.Running)
            {
                try
                {
                    _dictationRecognizer.Stop();
                }
                catch {}
            }
            _dictationRecognizer.Dispose();
            _dictationRecognizer = null;
        }
    }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
    private void StartPlugin()
    {
        try
        {
            _pluginInstance = new AndroidJavaObject(PluginClassName, gameObject.name, language);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AndroidSpeechRecognizer] Initializarea pluginului Java a esuat: {ex.Message}");
            OnSpeechErrorOccurred?.Invoke(ex.Message);
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            _pluginInstance?.Call("stopListening");
        }
        else if (IsMicrophoneEnabled)
        {
            _pluginInstance?.Call("startListening");
        }
    }

    private void OnDestroy()
    {
        _pluginInstance?.Call("destroy");
        _pluginInstance = null;
    }
#else
    private void OnApplicationPause(bool pauseStatus)
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (pauseStatus)
        {
            StopWindowsDictation();
        }
        else if (IsMicrophoneEnabled)
        {
            StartWindowsDictation();
        }
#endif
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        CleanupWindowsDictation();
#endif
    }
#endif

    public void ToggleMicrophone()
    {
        if (IsMicrophoneEnabled)
        {
            StopMicrophone();
        }
        else
        {
            StartMicrophone();
        }
    }

    public void StartMicrophone()
    {
        IsMicrophoneEnabled = true;

#if UNITY_ANDROID && !UNITY_EDITOR
        _pluginInstance?.Call("startListening");
#elif UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        StartWindowsDictation();
#endif

        OnMicrophoneStateChanged?.Invoke(true);
        Debug.Log("[AndroidSpeechRecognizer] Microfon activat.");
    }

    public void StopMicrophone()
    {
        IsMicrophoneEnabled = false;

#if UNITY_ANDROID && !UNITY_EDITOR
        _pluginInstance?.Call("stopListening");
#elif UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        StopWindowsDictation();
#endif

        if (liveCaptionText != null)
        {
            liveCaptionText.text = string.Empty;
        }

        OnMicrophoneStateChanged?.Invoke(false);
        Debug.Log("[AndroidSpeechRecognizer] Microfon dezactivat.");
    }

    /// <summary>
    /// Programmatically triggers a simulated transcription result (useful for testing and debugging).
    /// </summary>
    public void SimulateSpeechResult(string text)
    {
        OnSpeechResult_Internal(text);
    }

    // ---- Invoked by Java plugin via UnitySendMessage or Windows Speech ----

    public void OnSpeechResult(string text)
    {
        OnSpeechResult_Internal(text);
    }

    public void OnSpeechPartialResult(string text)
    {
        OnSpeechPartialResult_Internal(text);
    }

    public void OnSpeechError(string error)
    {
        Debug.LogWarning($"[AndroidSpeechRecognizer] Eroare recunoastere vocala: {error}");
        OnSpeechErrorOccurred?.Invoke(error);
    }

    private void OnSpeechResult_Internal(string text)
    {
        if (string.IsNullOrEmpty(text) || !IsMicrophoneEnabled)
        {
            return;
        }

        Debug.Log($"[AndroidSpeechRecognizer] Rezultat final: {text}");

        if (liveCaptionText != null)
        {
            liveCaptionText.text = string.Empty;
        }

        if (spokenTextChatWindow != null)
        {
            spokenTextChatWindow.AddMessage(text);
        }

        OnSpeechFinalized?.Invoke(text);
    }

    private void OnSpeechPartialResult_Internal(string text)
    {
        if (!IsMicrophoneEnabled)
        {
            return;
        }

        if (liveCaptionText != null)
        {
            liveCaptionText.text = text;
        }

        OnSpeechPartial?.Invoke(text);
    }
}
