using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Diagnostic utility: logs every change of EventSystem.currentSelectedGameObject
/// and the active input module, plus TMP_InputField's select/deselect/end-edit callbacks.
/// Disabled by default so it does not consume CPU cycles or spam logs in production builds.
/// </summary>
public class SelectionDebugLogger : MonoBehaviour
{
    [Header("Debug Settings")]
    [Tooltip("Enable to log selection changes each frame. Keep disabled in production.")]
    [SerializeField] private bool enableLogging = false;

    [SerializeField] private TMP_InputField watchedField;

    private GameObject _lastSelected;
    private string _lastModule;
    private int _frame;

    private void Awake()
    {
        if (watchedField == null)
        {
            watchedField = GetComponent<TMP_InputField>();
        }
    }

    private void OnEnable()
    {
        if (watchedField != null)
        {
            watchedField.onSelect.AddListener(OnFieldSelect);
            watchedField.onDeselect.AddListener(OnFieldDeselect);
            watchedField.onEndEdit.AddListener(OnFieldEndEdit);
        }
    }

    private void OnDisable()
    {
        if (watchedField != null)
        {
            watchedField.onSelect.RemoveListener(OnFieldSelect);
            watchedField.onDeselect.RemoveListener(OnFieldDeselect);
            watchedField.onEndEdit.RemoveListener(OnFieldEndEdit);
        }
    }

    private void OnFieldSelect(string text)
    {
        if (!enableLogging) return;
        Debug.Log($"[SelDebug] frame={Time.frameCount} TMP_InputField.onSelect fired, text=\"{text}\"");
    }

    private void OnFieldDeselect(string text)
    {
        if (!enableLogging) return;
        Debug.Log($"[SelDebug] frame={Time.frameCount} TMP_InputField.onDeselect fired, text=\"{text}\"");
    }

    private void OnFieldEndEdit(string text)
    {
        if (!enableLogging) return;
        Debug.Log($"[SelDebug] frame={Time.frameCount} TMP_InputField.onEndEdit fired, text=\"{text}\"");
    }

    private void Update()
    {
        if (!enableLogging) return;

        _frame++;

        var currentModule = EventSystem.current != null && EventSystem.current.currentInputModule != null
            ? EventSystem.current.currentInputModule.GetType().Name
            : "null";
        if (currentModule != _lastModule)
        {
            Debug.Log($"[SelDebug] frame={Time.frameCount} active input module changed: {_lastModule} -> {currentModule}");
            _lastModule = currentModule;
        }

        var current = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (current != _lastSelected)
        {
            string prevName = _lastSelected != null ? _lastSelected.name : "null";
            string curName = current != null ? current.name : "null";
            Debug.Log($"[SelDebug] frame={Time.frameCount} currentSelectedGameObject changed: {prevName} -> {curName}");
            _lastSelected = current;
        }
    }
}
