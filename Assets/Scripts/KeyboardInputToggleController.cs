using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class KeyboardInputToggleController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button keyboardButton;
    [SerializeField] private Button talkButton;
    [SerializeField] private TMP_InputField keyboardInputField;

    [Header("Behavior")]
    [SerializeField] private bool placeInputAtTalkButtonOnAwake = true;
    [SerializeField] private bool clearTextWhenOpened = false;

    [Header("Visual")]
    [SerializeField] private Color inputTextColor = Color.black;
    [SerializeField] private Color placeholderColor = new Color(0f, 0f, 0f, 0.45f);

    [Header("Backend")]
    [SerializeField] private string apiBaseUrl = "http://127.0.0.1:8000";
    [SerializeField] private string chatEndpoint = "/chat";
    [SerializeField] private int requestTimeoutSeconds = 30;
    [SerializeField] private string persistSessionIdKey = "yade_chat_session_id";

    [Header("Yade Reply Bubbles")]
    [SerializeField] private TMP_Text bubble1Text;
    [SerializeField] private TMP_Text bubble2Text;
    [SerializeField] private TMP_Text bubble3Text;
    [SerializeField] private TMP_Text bubble4Text;
    [SerializeField] private int bubbleCharLimit = 120;
    [SerializeField] private float bubbleFadeDuration = 0.2f;

    private static readonly char[] SentenceEndingChars = { '。', '！', '？', '!', '?' };
    private static readonly char[] SoftBreakChars = { '，', ',', '；', ';', '、', ' ' };

    private int lastSubmitFrame = -1;
    private bool isSendingRequest;
    private string sessionId;
    private readonly List<TMP_Text> bubbleTexts = new List<TMP_Text>(4);
    private readonly List<GameObject> bubbleRoots = new List<GameObject>(4);
    private readonly List<CanvasGroup> bubbleCanvasGroups = new List<CanvasGroup>(4);
    private readonly List<Coroutine> bubbleFadeCoroutines = new List<Coroutine>(4);

    [Serializable]
    private class ChatRequestPayload
    {
        public string session_id;
        public string message;
    }

    [Serializable]
    private class ChatResponsePayload
    {
        public string session_id = string.Empty;
        public string reply = string.Empty;
    }

    private void Awake()
    {
        if (placeInputAtTalkButtonOnAwake)
        {
            MatchInputFieldToTalkButtonRect();
        }

        EnsureInputFieldVisuals();

        if (keyboardInputField != null)
        {
            keyboardInputField.gameObject.SetActive(false);
        }

        sessionId = GetOrCreateSessionId();
        Debug.Log($"[KeyboardInputToggleController] Session ID: {sessionId}");
        RebuildBubbleList();
        HideAllBubbleRoots();
    }

    private void OnEnable()
    {
        if (keyboardButton != null)
        {
            keyboardButton.onClick.AddListener(OnClickKeyboardInput);
        }

        if (keyboardInputField != null)
        {
            keyboardInputField.onSubmit.AddListener(OnInputSubmit);
            keyboardInputField.onEndEdit.AddListener(OnInputEndEdit);
        }
    }

    private void OnDisable()
    {
        if (keyboardButton != null)
        {
            keyboardButton.onClick.RemoveListener(OnClickKeyboardInput);
        }

        if (keyboardInputField != null)
        {
            keyboardInputField.onSubmit.RemoveListener(OnInputSubmit);
            keyboardInputField.onEndEdit.RemoveListener(OnInputEndEdit);
        }
    }

    public void OnClickKeyboardInput()
    {
        if (talkButton != null)
        {
            talkButton.gameObject.SetActive(false);
        }

        if (keyboardInputField == null)
        {
            Debug.LogWarning("[KeyboardInputToggleController] Missing keyboardInputField reference.");
            return;
        }

        keyboardInputField.gameObject.SetActive(true);

        if (clearTextWhenOpened)
        {
            keyboardInputField.text = string.Empty;
        }

        keyboardInputField.Select();
        keyboardInputField.ActivateInputField();
    }

    private void OnInputSubmit(string text)
    {
        TrySubmitInput(text);
    }

    private void OnInputEndEdit(string text)
    {
        TrySubmitInput(text);
    }

    private void TrySubmitInput(string text)
    {
        if (Time.frameCount == lastSubmitFrame)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (isSendingRequest)
        {
            return;
        }

        lastSubmitFrame = Time.frameCount;
        string trimmedText = text.Trim();
        Debug.Log($"[KeyboardInputToggleController] Submit. User input: {trimmedText}");
        HideAllBubblesWithFade();
        StartCoroutine(SendChatRequestCoroutine(trimmedText));
    }

    private IEnumerator SendChatRequestCoroutine(string userMessage)
    {
        isSendingRequest = true;

        var payload = new ChatRequestPayload
        {
            session_id = sessionId,
            message = userMessage
        };

        string url = BuildChatUrl();
        string payloadJson = JsonUtility.ToJson(payload);
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payloadJson);

        using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(payloadBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = Mathf.Max(1, requestTimeoutSeconds);
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                var response = JsonUtility.FromJson<ChatResponsePayload>(responseText);
                if (response != null && !string.IsNullOrWhiteSpace(response.reply))
                {
                    Debug.Log($"[Yade Reply] {response.reply}");
                    RenderAssistantReplyToBubbles(response.reply);
                }
                else
                {
                    Debug.LogError($"[KeyboardInputToggleController] Invalid response body: {responseText}");
                }
            }
            else
            {
                string errorBody = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                Debug.LogError(
                    $"[KeyboardInputToggleController] Chat request failed. " +
                    $"url={url}, code={request.responseCode}, error={request.error}, body={errorBody}");
            }
        }

        if (keyboardInputField != null)
        {
            keyboardInputField.SetTextWithoutNotify(string.Empty);
            keyboardInputField.Select();
            keyboardInputField.ActivateInputField();
        }
        isSendingRequest = false;
    }

    private string BuildChatUrl()
    {
        string baseUrl = apiBaseUrl.TrimEnd('/');
        string endpoint = chatEndpoint.StartsWith("/") ? chatEndpoint : $"/{chatEndpoint}";
        return $"{baseUrl}{endpoint}";
    }

    private string GetOrCreateSessionId()
    {
        string stored = PlayerPrefs.GetString(persistSessionIdKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(stored))
        {
            return stored;
        }

        string created = Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString(persistSessionIdKey, created);
        PlayerPrefs.Save();
        return created;
    }

    private void RebuildBubbleList()
    {
        bubbleTexts.Clear();
        bubbleTexts.Add(bubble1Text);
        bubbleTexts.Add(bubble2Text);
        bubbleTexts.Add(bubble3Text);
        bubbleTexts.Add(bubble4Text);

        bubbleRoots.Clear();
        bubbleCanvasGroups.Clear();
        bubbleFadeCoroutines.Clear();
        for (int i = 0; i < bubbleTexts.Count; i++)
        {
            TMP_Text text = bubbleTexts[i];
            if (text == null)
            {
                bubbleRoots.Add(null);
                bubbleCanvasGroups.Add(null);
                bubbleFadeCoroutines.Add(null);
                continue;
            }

            // Prefer hiding the parent bubble container; fallback to the text object itself.
            GameObject root = text.transform.parent != null ? text.transform.parent.gameObject : text.gameObject;
            bubbleRoots.Add(root);

            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = root.AddComponent<CanvasGroup>();
            }

            bubbleCanvasGroups.Add(canvasGroup);
            bubbleFadeCoroutines.Add(null);
        }
    }

    private void RenderAssistantReplyToBubbles(string reply)
    {
        if (bubbleTexts.Count == 0)
        {
            RebuildBubbleList();
        }

        ClearAllBubbles();

        if (string.IsNullOrEmpty(reply))
        {
            return;
        }

        List<string> segments = SplitReplyIntoBubbleSegments(reply, bubbleTexts.Count, Mathf.Max(1, bubbleCharLimit));

        for (int i = 0; i < bubbleTexts.Count; i++)
        {
            TMP_Text targetBubble = bubbleTexts[i];
            if (targetBubble == null)
            {
                Debug.LogWarning($"[KeyboardInputToggleController] Bubble {i + 1} text is not assigned.");
                continue;
            }

            if (i >= segments.Count)
            {
                break;
            }

            targetBubble.text = segments[i];
            SetBubbleVisible(i, true, true);
        }
    }

    private List<string> SplitReplyIntoBubbleSegments(string reply, int bubbleCount, int charLimit)
    {
        var segments = new List<string>();
        if (bubbleCount <= 0 || string.IsNullOrWhiteSpace(reply))
        {
            return segments;
        }

        List<string> semanticUnits = SplitIntoSemanticUnits(reply, charLimit);
        if (semanticUnits.Count == 0)
        {
            return segments;
        }

        for (int i = 0; i < semanticUnits.Count; i++)
        {
            string unit = semanticUnits[i];
            if (string.IsNullOrWhiteSpace(unit))
            {
                continue;
            }

            if (segments.Count == bubbleCount - 1)
            {
                var overflowBuilder = new StringBuilder(unit);
                for (int j = i + 1; j < semanticUnits.Count; j++)
                {
                    if (string.IsNullOrWhiteSpace(semanticUnits[j]))
                    {
                        continue;
                    }

                    AppendSegmentWithSpacing(overflowBuilder, semanticUnits[j]);
                }

                string overflowText = overflowBuilder.ToString().Trim();
                if (!string.IsNullOrEmpty(overflowText))
                {
                    segments.Add(overflowText);
                }
                break;
            }

            if (segments.Count == 0)
            {
                segments.Add(unit);
                continue;
            }

            string current = segments[segments.Count - 1];
            if (ShouldAppendToCurrentSegment(current, unit, charLimit))
            {
                var builder = new StringBuilder(current);
                AppendSegmentWithSpacing(builder, unit);
                segments[segments.Count - 1] = builder.ToString().Trim();
            }
            else
            {
                segments.Add(unit);
            }
        }

        return segments;
    }

    private List<string> SplitIntoSemanticUnits(string reply, int charLimit)
    {
        var units = new List<string>();
        if (string.IsNullOrWhiteSpace(reply))
        {
            return units;
        }

        string normalizedReply = reply.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        if (normalizedReply.Length == 0)
        {
            return units;
        }

        string[] paragraphs = normalizedReply.Split(new[] { "\n\n" }, StringSplitOptions.None);
        for (int paragraphIndex = 0; paragraphIndex < paragraphs.Length; paragraphIndex++)
        {
            string paragraph = paragraphs[paragraphIndex].Trim();
            if (paragraph.Length == 0)
            {
                continue;
            }

            string[] lines = paragraph.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex].Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                List<string> sentenceUnits = SplitLineBySentenceBoundaries(line);
                for (int sentenceIndex = 0; sentenceIndex < sentenceUnits.Count; sentenceIndex++)
                {
                    AddUnitWithOptionalNewline(
                        units,
                        sentenceUnits[sentenceIndex],
                        lineIndex > 0 && sentenceIndex == 0,
                        paragraphIndex > 0 && lineIndex == 0 && sentenceIndex == 0,
                        charLimit);
                }
            }
        }

        return units;
    }

    private List<string> SplitLineBySentenceBoundaries(string line)
    {
        var units = new List<string>();
        if (string.IsNullOrWhiteSpace(line))
        {
            return units;
        }

        var sentenceBuilder = new StringBuilder();
        for (int i = 0; i < line.Length; i++)
        {
            char currentChar = line[i];
            sentenceBuilder.Append(currentChar);

            if (currentChar == '…')
            {
                if (i + 1 < line.Length && line[i + 1] == '…')
                {
                    continue;
                }

                AddTrimmedUnit(units, sentenceBuilder.ToString());
                sentenceBuilder.Length = 0;
                continue;
            }

            if (!IsSentenceEndingChar(currentChar))
            {
                continue;
            }

            while (i + 1 < line.Length)
            {
                char nextChar = line[i + 1];
                if (!IsClosingPunctuation(nextChar))
                {
                    break;
                }

                sentenceBuilder.Append(nextChar);
                i++;
            }

            AddTrimmedUnit(units, sentenceBuilder.ToString());
            sentenceBuilder.Length = 0;
        }

        AddTrimmedUnit(units, sentenceBuilder.ToString());
        return units;
    }

    private void AddUnitWithOptionalNewline(List<string> units, string rawUnit, bool forceSingleNewlineBefore, bool forceParagraphBreakBefore, int charLimit)
    {
        if (string.IsNullOrWhiteSpace(rawUnit))
        {
            return;
        }

        string unit = rawUnit.Trim();
        List<string> splitUnits = SplitOverlongUnit(unit, charLimit);
        for (int i = 0; i < splitUnits.Count; i++)
        {
            string piece = splitUnits[i];
            if (piece.Length == 0)
            {
                continue;
            }

            if (i == 0)
            {
                if (forceParagraphBreakBefore)
                {
                    piece = "\n\n" + piece;
                }
                else if (forceSingleNewlineBefore)
                {
                    piece = "\n" + piece;
                }
            }

            units.Add(piece);
        }
    }

    private List<string> SplitOverlongUnit(string unit, int charLimit)
    {
        var segments = new List<string>();
        if (string.IsNullOrWhiteSpace(unit))
        {
            return segments;
        }

        string remaining = unit.Trim();
        while (remaining.Length > charLimit)
        {
            int splitIndex = FindLastBreakIndex(remaining, charLimit, SoftBreakChars);
            if (splitIndex <= 0)
            {
                splitIndex = charLimit;
            }

            string chunk = remaining.Substring(0, splitIndex).Trim();
            if (chunk.Length == 0)
            {
                splitIndex = Mathf.Min(charLimit, remaining.Length);
                chunk = remaining.Substring(0, splitIndex).Trim();
            }

            if (chunk.Length == 0)
            {
                break;
            }

            segments.Add(chunk);
            remaining = remaining.Substring(splitIndex).TrimStart();
        }

        if (remaining.Length > 0)
        {
            segments.Add(remaining);
        }

        return segments;
    }

    private int FindLastBreakIndex(string value, int maxIndexExclusive, char[] breakChars)
    {
        int searchLength = Mathf.Min(maxIndexExclusive, value.Length);
        for (int i = searchLength - 1; i >= 0; i--)
        {
            if (Array.IndexOf(breakChars, value[i]) >= 0)
            {
                return i + 1;
            }
        }

        return -1;
    }

    private bool ShouldAppendToCurrentSegment(string current, string next, int charLimit)
    {
        if (string.IsNullOrEmpty(current) || string.IsNullOrWhiteSpace(next))
        {
            return false;
        }

        bool hasParagraphBreak = next.StartsWith("\n\n", StringComparison.Ordinal);
        if (hasParagraphBreak)
        {
            return false;
        }

        int combinedLength = current.Length + GetJoiner(current, next).Length + next.TrimStart('\n').Length;
        if (combinedLength <= charLimit)
        {
            return true;
        }

        return false;
    }

    private void AppendSegmentWithSpacing(StringBuilder builder, string next)
    {
        if (builder == null || string.IsNullOrWhiteSpace(next))
        {
            return;
        }

        builder.Append(GetJoiner(builder.ToString(), next));
        builder.Append(next.TrimStart('\n'));
    }

    private string GetJoiner(string current, string next)
    {
        if (string.IsNullOrEmpty(current))
        {
            return string.Empty;
        }

        if (next.StartsWith("\n\n", StringComparison.Ordinal))
        {
            return "\n\n";
        }

        if (next.StartsWith("\n", StringComparison.Ordinal))
        {
            return "\n";
        }

        return string.Empty;
    }

    private void AddTrimmedUnit(List<string> units, string value)
    {
        if (units == null || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        string trimmed = value.Trim();
        if (!string.IsNullOrEmpty(trimmed))
        {
            units.Add(trimmed);
        }
    }

    private bool IsSentenceEndingChar(char value)
    {
        return Array.IndexOf(SentenceEndingChars, value) >= 0;
    }

    private bool IsClosingPunctuation(char value)
    {
        switch (value)
        {
            case '"':
            case '\'':
            case '”':
            case '’':
            case ')':
            case '）':
            case ']':
            case '】':
                return true;
            default:
                return false;
        }
    }

    private void ClearAllBubbles()
    {
        for (int i = 0; i < bubbleTexts.Count; i++)
        {
            if (bubbleTexts[i] != null)
            {
                bubbleTexts[i].text = string.Empty;
            }

            SetBubbleVisible(i, false, false);
        }
    }

    private void HideAllBubbleRoots()
    {
        for (int i = 0; i < bubbleRoots.Count; i++)
        {
            SetBubbleVisible(i, false, false);
        }
    }

    private void HideAllBubblesWithFade()
    {
        for (int i = 0; i < bubbleRoots.Count; i++)
        {
            SetBubbleVisible(i, false, true);
        }
    }

    private void SetBubbleVisible(int index, bool visible, bool animate)
    {
        if (index < 0 || index >= bubbleRoots.Count)
        {
            return;
        }

        GameObject root = bubbleRoots[index];
        if (root != null)
        {
            if (!animate || bubbleFadeDuration <= 0f)
            {
                if (bubbleFadeCoroutines[index] != null)
                {
                    StopCoroutine(bubbleFadeCoroutines[index]);
                    bubbleFadeCoroutines[index] = null;
                }

                if (visible)
                {
                    root.SetActive(true);
                    if (bubbleCanvasGroups[index] != null)
                    {
                        bubbleCanvasGroups[index].alpha = 1f;
                    }
                }
                else
                {
                    if (bubbleCanvasGroups[index] != null)
                    {
                        bubbleCanvasGroups[index].alpha = 0f;
                    }
                    root.SetActive(false);
                }
                return;
            }

            if (bubbleFadeCoroutines[index] != null)
            {
                StopCoroutine(bubbleFadeCoroutines[index]);
                bubbleFadeCoroutines[index] = null;
            }

            CanvasGroup canvasGroup = bubbleCanvasGroups[index];
            if (visible)
            {
                root.SetActive(true);
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 0f;
                }
            }

            bubbleFadeCoroutines[index] = StartCoroutine(FadeBubbleCoroutine(index, visible));
        }
    }

    private IEnumerator FadeBubbleCoroutine(int index, bool visible)
    {
        if (index < 0 || index >= bubbleRoots.Count)
        {
            yield break;
        }

        GameObject root = bubbleRoots[index];
        CanvasGroup canvasGroup = bubbleCanvasGroups[index];

        if (root == null || canvasGroup == null)
        {
            yield break;
        }

        if (visible)
        {
            root.SetActive(true);
        }
        else if (!root.activeSelf)
        {
            bubbleFadeCoroutines[index] = null;
            yield break;
        }

        float startAlpha = visible ? 0f : canvasGroup.alpha;
        float endAlpha = visible ? 1f : 0f;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, bubbleFadeDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = endAlpha;
        if (!visible)
        {
            root.SetActive(false);
        }

        bubbleFadeCoroutines[index] = null;
    }

    private void MatchInputFieldToTalkButtonRect()
    {
        if (talkButton == null || keyboardInputField == null)
        {
            return;
        }

        RectTransform talkRect = talkButton.GetComponent<RectTransform>();
        RectTransform inputRect = keyboardInputField.GetComponent<RectTransform>();

        if (talkRect == null || inputRect == null)
        {
            return;
        }

        inputRect.SetParent(talkRect.parent, worldPositionStays: false);
        inputRect.anchorMin = talkRect.anchorMin;
        inputRect.anchorMax = talkRect.anchorMax;
        inputRect.pivot = talkRect.pivot;
        inputRect.anchoredPosition = talkRect.anchoredPosition;
        inputRect.sizeDelta = talkRect.sizeDelta;
        inputRect.localScale = Vector3.one;
        inputRect.SetSiblingIndex(talkRect.GetSiblingIndex());
    }

    private void EnsureInputFieldVisuals()
    {
        if (keyboardInputField == null)
        {
            return;
        }

        if (keyboardInputField.textComponent == null)
        {
            keyboardInputField.textComponent = keyboardInputField.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (keyboardInputField.textComponent != null)
        {
            keyboardInputField.textComponent.color = inputTextColor;
        }

        if (keyboardInputField.placeholder == null)
        {
            var textComponents = keyboardInputField.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var textComponent in textComponents)
            {
                if (textComponent != keyboardInputField.textComponent)
                {
                    keyboardInputField.placeholder = textComponent;
                    break;
                }
            }
        }

        if (keyboardInputField.placeholder is Graphic graphic)
        {
            graphic.color = placeholderColor;
        }
    }
}
