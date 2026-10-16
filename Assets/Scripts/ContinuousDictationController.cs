using System;
using System.Collections;
using Oculus.Voice.Dictation;
using UnityEngine;

/// <summary>
/// Legacy STT Controller (Wit.ai / Meta Voice SDK).
/// Implements ISpeechService for consistency across speech engines.
/// Keeps dictation running continuously: starts it on Start(), logs each full
/// transcription, and immediately reactivates after transcription, error, or abort.
/// </summary>
[RequireComponent(typeof(AppDictationExperience))]
public class ContinuousDictationController : MonoBehaviour, ISpeechService
{
    [SerializeField] private AppDictationExperience dictationExperience;
    [SerializeField] private ChatWindow spokenTextChatWindow;

    private const float DuplicateTranscriptionWindowSeconds = 1.5f;

    private bool _manuallyStopped;
    private bool _restartInProgress;
    private string _lastTranscription;
    private float _lastTranscriptionRealtime = -DuplicateTranscriptionWindowSeconds;
    private int _enableCount;
    private int _transcriptionCallCount;
    private int _restartRunCount;

    public bool IsMicrophoneEnabled => !_manuallyStopped;

    // ISpeechService events
    public event Action<bool> OnMicrophoneStateChanged;
    public event Action<string> OnSpeechFinalized;
#pragma warning disable CS0067
    public event Action<string> OnSpeechPartial;
#pragma warning restore CS0067
    public event Action<string> OnSpeechErrorOccurred;

    private void Reset()
    {
        dictationExperience = GetComponent<AppDictationExperience>();
    }

    private void Awake()
    {
        if (dictationExperience == null)
        {
            dictationExperience = GetComponent<AppDictationExperience>();
        }

        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
    }

    private void OnEnable()
    {
        _enableCount++;
        Debug.Log($"[Dictation] OnEnable #{_enableCount} pe obiectul {GetInstanceID()} ({gameObject.name})");

        if (dictationExperience != null && dictationExperience.DictationEvents != null)
        {
            dictationExperience.DictationEvents.OnFullTranscription.AddListener(HandleFullTranscription);
            dictationExperience.DictationEvents.OnError.AddListener(HandleError);
            dictationExperience.DictationEvents.OnAborted.AddListener(HandleAborted);
        }
    }

    private void OnDisable()
    {
        if (dictationExperience != null && dictationExperience.DictationEvents != null)
        {
            dictationExperience.DictationEvents.OnFullTranscription.RemoveListener(HandleFullTranscription);
            dictationExperience.DictationEvents.OnError.RemoveListener(HandleError);
            dictationExperience.DictationEvents.OnAborted.RemoveListener(HandleAborted);
        }
    }

    private void Start()
    {
        Debug.Log($"[Dictation] Start() pe obiectul {GetInstanceID()} ({gameObject.name})");
        if (dictationExperience != null)
        {
            dictationExperience.Activate();
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        Debug.Log($"[Dictation] OnApplicationPause({pauseStatus})");

        if (pauseStatus || _manuallyStopped || dictationExperience == null)
        {
            return;
        }

        Debug.Log("[Dictation] Reluare dupa pauza, fortez repornirea dictarii.");
        dictationExperience.Cancel();
        _restartInProgress = false;
        RestartDictation();
    }

    public void ToggleMicrophone()
    {
        if (_manuallyStopped)
        {
            StartMicrophone();
        }
        else
        {
            StopMicrophone();
        }
    }

    public void StartMicrophone()
    {
        _manuallyStopped = false;

        if (dictationExperience != null && !dictationExperience.Active)
        {
            dictationExperience.Activate();
        }

        OnMicrophoneStateChanged?.Invoke(true);
    }

    public void StopMicrophone()
    {
        _manuallyStopped = true;

        if (dictationExperience != null && dictationExperience.Active)
        {
            dictationExperience.Deactivate();
        }

        OnMicrophoneStateChanged?.Invoke(false);
    }

    private void HandleFullTranscription(string transcription)
    {
        _transcriptionCallCount++;
        Debug.Log($"[Dictation] CALL #{_transcriptionCallCount} obj={GetInstanceID()} text=\"{transcription}\"");

        bool isDuplicate = transcription == _lastTranscription
            && Time.realtimeSinceStartup - _lastTranscriptionRealtime < DuplicateTranscriptionWindowSeconds;
        _lastTranscription = transcription;
        _lastTranscriptionRealtime = Time.realtimeSinceStartup;

        if (isDuplicate)
        {
            Debug.LogWarning($"[Dictation] (obj {GetInstanceID()}) Transcriere duplicata ignorata: {transcription}");
            return;
        }

        Debug.Log($"[Dictation] (obj {GetInstanceID()}) Transcriere completa: {transcription}");

        if (spokenTextChatWindow != null)
        {
            spokenTextChatWindow.AddMessage(transcription);
        }

        OnSpeechFinalized?.Invoke(transcription);

        RestartDictation();
    }

    private void HandleError(string error, string message)
    {
        string fullError = $"Eroare ({error}): {message}";
        Debug.LogWarning($"[Dictation] {fullError}. Repornesc dictarea.");
        OnSpeechErrorOccurred?.Invoke(fullError);
        RestartDictation();
    }

    private void HandleAborted()
    {
        Debug.LogWarning("[Dictation] Dictarea s-a oprit (timeout/abort). Repornesc dictarea.");
        OnSpeechErrorOccurred?.Invoke("Dictation aborted/timeout");
        RestartDictation();
    }

    private void RestartDictation()
    {
        if (_manuallyStopped || _restartInProgress)
        {
            return;
        }

        _restartInProgress = true;
        StartCoroutine(RestartWhenInactive());
    }

    private IEnumerator RestartWhenInactive()
    {
        if (dictationExperience == null)
        {
            _restartInProgress = false;
            yield break;
        }

        int runId = ++_restartRunCount;
        Debug.Log($"[Dictation] RestartWhenInactive #{runId} pornit (Active={dictationExperience.Active}, MicActive={dictationExperience.MicActive})");

        yield return WaitForMicRelease(runId);

        int attempt = 0;
        while (!_manuallyStopped && dictationExperience != null)
        {
            attempt++;
            dictationExperience.Activate();
            Debug.Log($"[Dictation] RestartWhenInactive #{runId} Activate() apelat, incercarea {attempt}");

            float waitedForMic = 0f;
            while (!dictationExperience.MicActive && waitedForMic < 1f)
            {
                yield return null;
                waitedForMic += Time.deltaTime;
            }

            if (dictationExperience.MicActive)
            {
                Debug.Log($"[Dictation] RestartWhenInactive #{runId} MicActive=true dupa {waitedForMic:F2}s, gata.");
                break;
            }

            Debug.LogWarning($"[Dictation] RestartWhenInactive #{runId} Microfonul nu a pornit la incercarea {attempt}, reincerc.");
            yield return new WaitForSeconds(0.5f);
        }

        _restartInProgress = false;
    }

    private IEnumerator WaitForMicRelease(int runId)
    {
        const float timeoutSeconds = 2f;
        float elapsed = 0f;

        while (dictationExperience != null && (dictationExperience.Active || dictationExperience.MicActive) && elapsed < timeoutSeconds)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }

        if (dictationExperience != null)
        {
            Debug.Log($"[Dictation] RestartWhenInactive #{runId} WaitForMicRelease gata dupa {elapsed:F2}s (Active={dictationExperience.Active}, MicActive={dictationExperience.MicActive})");
        }
    }
}
