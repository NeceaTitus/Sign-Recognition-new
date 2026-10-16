using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Turns the GameObject it sits on into a scrollable message list: every call
/// to AddMessage spawns or recycles a bubble below previous ones and auto-scrolls
/// to the latest entry.
/// Supports both custom Prefabs and runtime procedural fallback, with an Object Pool
/// to avoid GC allocations and frame drops on Quest/mobile.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ChatWindow : MonoBehaviour
{
    [Header("Bubble Prefab (Optional)")]
    [Tooltip("Optional custom prefab for message bubbles containing a TextMeshProUGUI. If null, a procedural bubble will be generated.")]
    [SerializeField] private GameObject messageBubblePrefab;

    [Header("Procedural Bubble Appearance (Used when Prefab is null)")]
    [SerializeField] private Sprite bubbleSprite;
    [SerializeField] private Color bubbleColor = new Color(0.4339623f, 0.41553935f, 0.41553935f, 0.75f);
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Color textColor = new Color(0.19607843f, 0.19607843f, 0.19607843f, 1f);
    [SerializeField] private float fontSize = 22f;

    [Header("Layout")]
    [SerializeField] private float panelPadding = 12f;
    [SerializeField] private float bubbleSpacing = 10f;
    [SerializeField] private float bubbleInnerPaddingX = 14f;
    [SerializeField] private float bubbleInnerPaddingY = 8f;

    [Header("History & Pooling")]
    [Tooltip("Maximum active messages visible at once. Oldest messages are recycled. Set to 0 for unlimited.")]
    [SerializeField] private int maxMessages = 30;

    private RectTransform _content;
    private ScrollRect _scrollRect;
    private readonly List<GameObject> _bubbles = new List<GameObject>();
    private readonly Queue<GameObject> _bubblePool = new Queue<GameObject>();

    private void Awake()
    {
        BuildScrollView();
    }

    public void BuildScrollView()
    {
        if (_content != null)
        {
            return;
        }

        var rootRect = (RectTransform)transform;

        var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        var viewportRect = (RectTransform)viewportGO.transform;
        viewportRect.SetParent(rootRect, false);
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(panelPadding, panelPadding);
        viewportRect.offsetMax = new Vector2(-panelPadding, -panelPadding);

        var contentGO = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        _content = (RectTransform)contentGO.transform;
        _content.SetParent(viewportRect, false);
        _content.anchorMin = new Vector2(0f, 1f);
        _content.anchorMax = new Vector2(1f, 1f);
        _content.pivot = new Vector2(0.5f, 1f);
        _content.anchoredPosition = Vector2.zero;
        _content.sizeDelta = Vector2.zero;

        var contentLayout = contentGO.GetComponent<VerticalLayoutGroup>();
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = false;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childAlignment = TextAnchor.UpperLeft;
        contentLayout.spacing = bubbleSpacing;

        var contentFitter = contentGO.GetComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _scrollRect = gameObject.GetComponent<ScrollRect>();
        if (_scrollRect == null)
        {
            _scrollRect = gameObject.AddComponent<ScrollRect>();
        }
        _scrollRect.viewport = viewportRect;
        _scrollRect.content = _content;
        _scrollRect.horizontal = false;
        _scrollRect.vertical = true;
        _scrollRect.movementType = ScrollRect.MovementType.Clamped;
        _scrollRect.scrollSensitivity = 20f;
    }

    /// <summary>
    /// Appends a new message bubble (reusing a pooled one if available) and scrolls to it.
    /// </summary>
    public void AddMessage(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (_content == null)
        {
            BuildScrollView();
        }

        GameObject bubbleGO;

        if (_bubblePool.Count > 0)
        {
            bubbleGO = _bubblePool.Dequeue();
            bubbleGO.SetActive(true);
            bubbleGO.transform.SetAsLastSibling();

            var tmp = bubbleGO.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = text;
            }
        }
        else
        {
            bubbleGO = CreateBubbleInstance(text);
        }

        _bubbles.Add(bubbleGO);
        TrimHistory();

        StartCoroutine(ScrollToBottomNextFrame());
    }

    private GameObject CreateBubbleInstance(string text)
    {
        if (messageBubblePrefab != null)
        {
            var instance = Instantiate(messageBubblePrefab, _content, false);
            var tmp = instance.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = text;
            }
            return instance;
        }

        var bubbleGO = new GameObject("MessageBubble", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        bubbleGO.transform.SetParent(_content, false);

        var background = bubbleGO.GetComponent<Image>();
        background.sprite = bubbleSprite;
        background.type = Image.Type.Sliced;
        background.color = bubbleColor;
        background.raycastTarget = false;

        var bubbleLayout = bubbleGO.GetComponent<VerticalLayoutGroup>();
        bubbleLayout.padding = new RectOffset(
            Mathf.RoundToInt(bubbleInnerPaddingX),
            Mathf.RoundToInt(bubbleInnerPaddingX),
            Mathf.RoundToInt(bubbleInnerPaddingY),
            Mathf.RoundToInt(bubbleInnerPaddingY));
        bubbleLayout.childControlWidth = true;
        bubbleLayout.childControlHeight = true;
        bubbleLayout.childForceExpandWidth = true;
        bubbleLayout.childForceExpandHeight = false;

        var bubbleFitter = bubbleGO.GetComponent<ContentSizeFitter>();
        bubbleFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        bubbleFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(bubbleGO.transform, false);
        var tmpUGUI = textGO.GetComponent<TextMeshProUGUI>();
        if (font != null)
        {
            tmpUGUI.font = font;
        }
        tmpUGUI.fontSize = fontSize;
        tmpUGUI.color = textColor;
        tmpUGUI.raycastTarget = false;
        tmpUGUI.text = text;

        return bubbleGO;
    }

    private void TrimHistory()
    {
        if (maxMessages <= 0)
        {
            return;
        }

        while (_bubbles.Count > maxMessages)
        {
            var oldest = _bubbles[0];
            _bubbles.RemoveAt(0);
            oldest.SetActive(false);
            _bubblePool.Enqueue(oldest);
        }
    }

    /// <summary>
    /// Clears and recycles all active bubbles into the object pool.
    /// </summary>
    public void ClearMessages()
    {
        for (int i = 0; i < _bubbles.Count; i++)
        {
            var bubble = _bubbles[i];
            if (bubble != null)
            {
                bubble.SetActive(false);
                _bubblePool.Enqueue(bubble);
            }
        }
        _bubbles.Clear();
    }

    private IEnumerator ScrollToBottomNextFrame()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (_scrollRect != null)
        {
            _scrollRect.verticalNormalizedPosition = 0f;
        }
    }
}
