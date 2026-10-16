using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Legacy component: keeps the button's label in sync with the current microphone state.
/// NOTE: Its functionality has been merged into MicrophoneToggleButton.
/// Kept for backwards-compatibility with existing scenes.
/// </summary>
[Obsolete("Use MicrophoneToggleButton which handles both icon and label.")]
public class MicToggleButtonUI : MonoBehaviour
{
    [SerializeField] private AndroidSpeechRecognizer dictationController;
    [SerializeField] private TMP_Text label;

    private void Reset()
    {
        label = GetComponentInChildren<TMP_Text>();
        dictationController = FindFirstObjectByType<AndroidSpeechRecognizer>();
    }

    private void Awake()
    {
        if (label == null)
        {
            label = GetComponentInChildren<TMP_Text>();
        }

        if (dictationController == null)
        {
            dictationController = FindFirstObjectByType<AndroidSpeechRecognizer>();
        }
    }

    private void OnEnable()
    {
        if (dictationController != null)
        {
            dictationController.OnMicrophoneStateChanged += UpdateLabel;
            UpdateLabel(dictationController.IsMicrophoneEnabled);
        }
    }

    private void OnDisable()
    {
        if (dictationController != null)
        {
            dictationController.OnMicrophoneStateChanged -= UpdateLabel;
        }
    }

    private void UpdateLabel(bool microphoneEnabled)
    {
        if (label != null)
        {
            label.text = microphoneEnabled ? "Microfon: Pornit" : "Microfon: Oprit";
        }
    }
}
