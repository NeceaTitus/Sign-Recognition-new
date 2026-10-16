using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls a UI Button to toggle the microphone on/off.
/// Synchronizes both the icon (Image sprite) and the text label (TMP_Text)
/// with the speech recognizer state.
/// </summary>
[RequireComponent(typeof(Button))]
public class MicrophoneToggleButton : MonoBehaviour
{
    [Header("Speech Service Reference")]
    [Tooltip("Reference to the speech recognizer. If null, will search for one in the scene.")]
    [SerializeField] private AndroidSpeechRecognizer dictationController;

    [Header("Icon Settings")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Sprite micOnSprite;
    [SerializeField] private Sprite micOffSprite;

    [Header("Label Settings (Optional)")]
    [Tooltip("Optional text label to update with microphone state (e.g. MicToggleLabel).")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private string micOnText = "Microfon: Pornit";
    [SerializeField] private string micOffText = "Microfon: Oprit";

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();

        if (iconImage == null)
        {
            iconImage = GetComponentInChildren<Image>();
        }

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
        if (_button != null)
        {
            _button.onClick.AddListener(HandleClick);
        }

        if (dictationController != null)
        {
            dictationController.OnMicrophoneStateChanged += UpdateVisuals;
            UpdateVisuals(dictationController.IsMicrophoneEnabled);
        }
        else
        {
            Debug.LogWarning("[MicrophoneToggleButton] Niciun AndroidSpeechRecognizer atasat sau gasit in scena.");
        }
    }

    private void OnDisable()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(HandleClick);
        }

        if (dictationController != null)
        {
            dictationController.OnMicrophoneStateChanged -= UpdateVisuals;
        }
    }

    private void HandleClick()
    {
        if (dictationController != null)
        {
            dictationController.ToggleMicrophone();
        }
    }

    private void UpdateVisuals(bool micEnabled)
    {
        if (iconImage != null)
        {
            iconImage.sprite = micEnabled ? micOnSprite : micOffSprite;
        }

        if (label != null)
        {
            label.text = micEnabled ? micOnText : micOffText;
        }
    }
}
