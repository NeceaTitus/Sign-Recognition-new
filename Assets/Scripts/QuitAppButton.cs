using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Wires a UI Button to quit the application entirely (e.g. to exit the app on Quest).
/// </summary>
[RequireComponent(typeof(Button))]
public class QuitAppButton : MonoBehaviour
{
    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        _button.onClick.AddListener(HandleClick);
    }

    private void OnDisable()
    {
        _button.onClick.RemoveListener(HandleClick);
    }

    private void HandleClick()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
