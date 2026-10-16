using System;

/// <summary>
/// Common interface for speech-to-text / dictation services (Android SpeechRecognizer, Wit.ai, Editor Mock, etc.).
/// Decouples UI and business logic from specific STT SDK implementations.
/// </summary>
public interface ISpeechService
{
    /// <summary>
    /// Indicates whether the microphone is currently active or enabled by the user.
    /// </summary>
    bool IsMicrophoneEnabled { get; }

    /// <summary>
    /// Fired when the microphone is turned on or off.
    /// </summary>
    event Action<bool> OnMicrophoneStateChanged;

    /// <summary>
    /// Fired when a final utterance transcription is produced.
    /// </summary>
    event Action<string> OnSpeechFinalized;

    /// <summary>
    /// Fired when partial / in-progress transcription is updated.
    /// </summary>
    event Action<string> OnSpeechPartial;

    /// <summary>
    /// Fired when a speech recognition error occurs.
    /// </summary>
    event Action<string> OnSpeechErrorOccurred;

    /// <summary>
    /// Toggles the microphone state on/off.
    /// </summary>
    void ToggleMicrophone();

    /// <summary>
    /// Starts or resumes microphone capture and dictation.
    /// </summary>
    void StartMicrophone();

    /// <summary>
    /// Stops or pauses microphone capture.
    /// </summary>
    void StopMicrophone();
}
