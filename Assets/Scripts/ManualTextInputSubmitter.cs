using TMPro;
using UnityEngine;

/// <summary>
/// Forwards manually typed text into the same chat window the speech
/// pipelines use, so typed messages show up as bubbles.
/// Submits on Enter (TMP_InputField onSubmit) and provides a public
/// SubmitCurrentText() method for UI Send buttons.
/// </summary>
[RequireComponent(typeof(TMP_InputField))]
public class ManualTextInputSubmitter : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private ChatWindow chatWindow;

    private void Reset()
    {
        inputField = GetComponent<TMP_InputField>();
    }

    private void Awake()
    {
        if (inputField == null)
        {
            inputField = GetComponent<TMP_InputField>();
        }
    }

    private void OnEnable()
    {
        if (inputField != null)
        {
            inputField.onSubmit.AddListener(HandleSubmit);
        }
    }

    private void OnDisable()
    {
        if (inputField != null)
        {
            inputField.onSubmit.RemoveListener(HandleSubmit);
        }
    }

    /// <summary>
    /// Can be wired to a UI Submit/Send button's onClick event.
    /// </summary>
    public void SubmitCurrentText()
    {
        if (inputField != null)
        {
            HandleSubmit(inputField.text);
        }
    }

    private void HandleSubmit(string text)
    {
        text = text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (chatWindow != null)
        {
            chatWindow.AddMessage(text);
        }

        if (inputField != null)
        {
            inputField.text = string.Empty;
            inputField.ActivateInputField();
        }
    }
}
