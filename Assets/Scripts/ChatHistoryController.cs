using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ChatHistoryController : MonoBehaviour
{
    [Header("History")]
    [SerializeField] private int maxHistoryCount = 6;
    [SerializeField] private Sprite userBoxSprite;
    [SerializeField] private Sprite characterBoxSprite;
    [SerializeField] private Vector2 historyBoxSize = new Vector2(360f, 72f);
    [SerializeField] private Vector2 historyOffset = new Vector2(-32f, -32f);
    [SerializeField] private float historySpacing = 8f;
    [SerializeField] private int historyFontSize = 24;
    [SerializeField] private Color historyTextColor = Color.black;
    [SerializeField] private Vector4 historyTextPadding = new Vector4(30f, 8f, 30f, 8f);

    [Header("Modal")]
    [SerializeField] private Sprite historyModalSprite;
    [SerializeField] private Vector2 modalSize = new Vector2(900f, 520f);
    [SerializeField] private string userDisplayName = "玩家";
    [SerializeField] private string characterDisplayName = "亚德";
    [SerializeField] private int modalNameFontSize = 32;
    [SerializeField] private int modalMessageFontSize = 30;
    [SerializeField] private Color modalTextColor = Color.black;
    [SerializeField] private Vector4 modalNamePadding = new Vector4(120f, 34f, 64f, 0f);
    [SerializeField] private float modalNameHeight = 58f;
    [SerializeField] private Vector4 modalMessagePadding = new Vector4(92f, 110f, 74f, 58f);
    [SerializeField] private Color overlayColor = new Color(0f, 0f, 0f, 0.35f);

    private readonly List<HistoryEntry> entries = new List<HistoryEntry>();
    private RectTransform historyRoot;
    private GameObject modalRoot;
    private RectTransform modalRect;
    private Image modalImage;
    private TMP_Text modalNameText;
    private TMP_Text modalMessageText;

    private struct HistoryEntry
    {
        public GameObject Root;
    }

    private void Awake()
    {
        EnsureHistoryRoot();
        EnsureModalView();
        HideModal();
    }

    public void AddUserMessage(string message)
    {
        AddMessage(message, userBoxSprite, userDisplayName);
    }

    public void AddCharacterMessage(string message)
    {
        AddMessage(message, characterBoxSprite, characterDisplayName);
    }

    private void AddMessage(string message, Sprite boxSprite, string speakerName)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        EnsureHistoryRoot();

        string trimmedMessage = message.Trim();
        GameObject entryRoot = CreateHistoryEntry(trimmedMessage, boxSprite, speakerName);

        entries.Add(new HistoryEntry
        {
            Root = entryRoot
        });

        int clampedMaxCount = Mathf.Max(1, maxHistoryCount);
        while (entries.Count > clampedMaxCount)
        {
            HistoryEntry oldestEntry = entries[0];
            entries.RemoveAt(0);

            if (oldestEntry.Root != null)
            {
                Destroy(oldestEntry.Root);
            }
        }
    }

    private void EnsureHistoryRoot()
    {
        if (historyRoot != null)
        {
            return;
        }

        GameObject root = new GameObject("ChatHistoryRoot", typeof(RectTransform), typeof(VerticalLayoutGroup));
        root.transform.SetParent(transform, false);

        historyRoot = root.GetComponent<RectTransform>();
        historyRoot.anchorMin = new Vector2(1f, 1f);
        historyRoot.anchorMax = new Vector2(1f, 1f);
        historyRoot.pivot = new Vector2(1f, 1f);
        historyRoot.anchoredPosition = historyOffset;
        historyRoot.sizeDelta = new Vector2(historyBoxSize.x, Mathf.Max(1, maxHistoryCount) * (historyBoxSize.y + historySpacing));

        VerticalLayoutGroup layoutGroup = root.GetComponent<VerticalLayoutGroup>();
        layoutGroup.childAlignment = TextAnchor.UpperRight;
        layoutGroup.spacing = historySpacing;
        layoutGroup.childControlWidth = false;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;
    }

    private GameObject CreateHistoryEntry(string message, Sprite boxSprite, string speakerName)
    {
        GameObject entryRoot = new GameObject("ChatHistoryEntry", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        entryRoot.transform.SetParent(historyRoot, false);

        RectTransform entryRect = entryRoot.GetComponent<RectTransform>();
        entryRect.sizeDelta = historyBoxSize;

        LayoutElement layoutElement = entryRoot.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = historyBoxSize.x;
        layoutElement.preferredHeight = historyBoxSize.y;

        Image image = entryRoot.GetComponent<Image>();
        image.sprite = boxSprite;
        image.type = Image.Type.Sliced;
        image.preserveAspect = false;

        Button button = entryRoot.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        button.onClick.AddListener(() => ShowModal(speakerName, message));

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(entryRoot.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(historyTextPadding.x, historyTextPadding.w);
        textRect.offsetMax = new Vector2(-historyTextPadding.z, -historyTextPadding.y);

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = message;
        text.color = historyTextColor;
        text.fontSize = historyFontSize;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.maxVisibleLines = 1;
        text.raycastTarget = false;

        return entryRoot;
    }

    private void EnsureModalView()
    {
        if (modalRoot != null)
        {
            return;
        }

        modalRoot = new GameObject("ChatHistoryModalRoot", typeof(RectTransform));
        modalRoot.transform.SetParent(transform, false);

        RectTransform rootRect = modalRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        rootRect.SetAsLastSibling();

        GameObject overlayObject = new GameObject("DismissOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        overlayObject.transform.SetParent(modalRoot.transform, false);

        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = overlayObject.GetComponent<Image>();
        overlayImage.color = overlayColor;

        Button overlayButton = overlayObject.GetComponent<Button>();
        overlayButton.transition = Selectable.Transition.None;
        overlayButton.onClick.AddListener(HideModal);

        GameObject modalObject = new GameObject("HistoryModal", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        modalObject.transform.SetParent(modalRoot.transform, false);

        modalRect = modalObject.GetComponent<RectTransform>();
        modalRect.anchorMin = new Vector2(0.5f, 0.5f);
        modalRect.anchorMax = new Vector2(0.5f, 0.5f);
        modalRect.pivot = new Vector2(0.5f, 0.5f);
        modalRect.anchoredPosition = Vector2.zero;
        modalRect.sizeDelta = GetModalDisplaySize();

        modalImage = modalObject.GetComponent<Image>();
        modalImage.sprite = historyModalSprite;
        modalImage.type = Image.Type.Simple;
        modalImage.preserveAspect = true;

        Button modalButton = modalObject.GetComponent<Button>();
        modalButton.transition = Selectable.Transition.None;

        GameObject nameObject = new GameObject("NameText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        nameObject.transform.SetParent(modalObject.transform, false);

        RectTransform nameRect = nameObject.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.offsetMin = new Vector2(modalNamePadding.x, -modalNamePadding.y - modalNameHeight);
        nameRect.offsetMax = new Vector2(-modalNamePadding.z, -modalNamePadding.y);

        modalNameText = nameObject.GetComponent<TMP_Text>();
        modalNameText.color = modalTextColor;
        modalNameText.fontSize = modalNameFontSize;
        modalNameText.alignment = TextAlignmentOptions.MidlineLeft;
        modalNameText.enableWordWrapping = false;
        modalNameText.overflowMode = TextOverflowModes.Ellipsis;
        modalNameText.maxVisibleLines = 1;
        modalNameText.raycastTarget = false;

        GameObject messageObject = new GameObject("MessageText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        messageObject.transform.SetParent(modalObject.transform, false);

        RectTransform messageRect = messageObject.GetComponent<RectTransform>();
        messageRect.anchorMin = Vector2.zero;
        messageRect.anchorMax = Vector2.one;
        messageRect.offsetMin = new Vector2(modalMessagePadding.x, modalMessagePadding.w);
        messageRect.offsetMax = new Vector2(-modalMessagePadding.z, -modalMessagePadding.y);

        modalMessageText = messageObject.GetComponent<TMP_Text>();
        modalMessageText.color = modalTextColor;
        modalMessageText.fontSize = modalMessageFontSize;
        modalMessageText.alignment = TextAlignmentOptions.TopLeft;
        modalMessageText.enableWordWrapping = true;
        modalMessageText.overflowMode = TextOverflowModes.Overflow;
        modalMessageText.raycastTarget = false;
    }

    private void ShowModal(string speakerName, string message)
    {
        EnsureModalView();

        modalImage.sprite = historyModalSprite;
        modalRect.sizeDelta = GetModalDisplaySize();
        modalNameText.text = speakerName;
        modalMessageText.text = message;
        modalRoot.transform.SetAsLastSibling();
        modalRoot.SetActive(true);

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void HideModal()
    {
        if (modalRoot != null)
        {
            modalRoot.SetActive(false);
        }
    }

    private Vector2 GetModalDisplaySize()
    {
        if (historyModalSprite == null || historyModalSprite.rect.height <= 0f)
        {
            return modalSize;
        }

        float spriteAspect = historyModalSprite.rect.width / historyModalSprite.rect.height;
        float targetWidth = modalSize.x;
        float targetHeight = targetWidth / spriteAspect;

        if (targetHeight > modalSize.y)
        {
            targetHeight = modalSize.y;
            targetWidth = targetHeight * spriteAspect;
        }

        return new Vector2(targetWidth, targetHeight);
    }
}
