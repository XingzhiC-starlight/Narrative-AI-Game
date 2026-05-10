using TMPro;
using System;
using System.Collections;
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
    [SerializeField] private bool hideInputOnAwake = false;
    [SerializeField] private bool focusInputOnAwake = true;

    [Header("Visual")]
    [SerializeField] private Color inputTextColor = Color.black;
    [SerializeField] private Color placeholderColor = new Color(0f, 0f, 0f, 0.45f);

    [Header("Backend")]
    [SerializeField] private string apiBaseUrl = "http://127.0.0.1:8000";
    [SerializeField] private string chatEndpoint = "/chat";
    [SerializeField] private int requestTimeoutSeconds = 30;
    [SerializeField] private string persistSessionIdKey = "yade_chat_session_id";

    [Header("Yade Reply")]
    [SerializeField] private TMP_Text replyText;
    [SerializeField] private float bubbleFadeDuration = 0.2f;

    private int lastSubmitFrame = -1;
    private bool isSendingRequest;
    private string sessionId;
    private GameObject replyBubbleRoot;
    private CanvasGroup replyBubbleCanvasGroup;
    private Coroutine replyBubbleFadeCoroutine;

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
            keyboardInputField.gameObject.SetActive(!hideInputOnAwake);
            if (!hideInputOnAwake && focusInputOnAwake)
            {
                keyboardInputField.Select();
                keyboardInputField.ActivateInputField();
            }
        }

        sessionId = GetOrCreateSessionId();
        Debug.Log($"[KeyboardInputToggleController] Session ID: {sessionId}");
        RebuildReplyBubble();
        HideReplyBubble(false);
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
        HideReplyBubble(true);
        StartCoroutine(SendChatRequestCoroutine(trimmedText));
    }

    private IEnumerator SendChatRequestCoroutine(string userMessage)
    {
        isSendingRequest = true;

        if (keyboardInputField != null)
        {
            keyboardInputField.interactable = false;
        }

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
                    RenderAssistantReply(response.reply);
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
            keyboardInputField.interactable = true;
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

    private void RebuildReplyBubble()
    {
        replyBubbleRoot = null;
        replyBubbleCanvasGroup = null;

        if (replyText == null)
        {
            return;
        }

        replyBubbleRoot = replyText.transform.parent != null ? replyText.transform.parent.gameObject : replyText.gameObject;
        replyBubbleCanvasGroup = replyBubbleRoot.GetComponent<CanvasGroup>();
        if (replyBubbleCanvasGroup == null)
        {
            replyBubbleCanvasGroup = replyBubbleRoot.AddComponent<CanvasGroup>();
        }
    }

    private void RenderAssistantReply(string reply)
    {
        if (replyText == null)
        {
            Debug.LogWarning("[KeyboardInputToggleController] Missing replyText reference.");
            return;
        }

        if (replyBubbleRoot == null || replyBubbleCanvasGroup == null)
        {
            RebuildReplyBubble();
        }

        replyText.text = reply.Trim();
        SetReplyBubbleVisible(true, true);
    }

    private void HideReplyBubble(bool animate)
    {
        if (replyText != null)
        {
            replyText.text = string.Empty;
        }

        SetReplyBubbleVisible(false, animate);
    }

    private void SetReplyBubbleVisible(bool visible, bool animate)
    {
        if (replyBubbleRoot == null)
        {
            return;
        }

        if (!animate || bubbleFadeDuration <= 0f || replyBubbleCanvasGroup == null)
        {
            if (replyBubbleFadeCoroutine != null)
            {
                StopCoroutine(replyBubbleFadeCoroutine);
                replyBubbleFadeCoroutine = null;
            }

            replyBubbleRoot.SetActive(visible);
            if (replyBubbleCanvasGroup != null)
            {
                replyBubbleCanvasGroup.alpha = visible ? 1f : 0f;
            }
            return;
        }

        if (replyBubbleFadeCoroutine != null)
        {
            StopCoroutine(replyBubbleFadeCoroutine);
            replyBubbleFadeCoroutine = null;
        }

        if (visible)
        {
            replyBubbleRoot.SetActive(true);
            replyBubbleCanvasGroup.alpha = 0f;
        }
        else if (!replyBubbleRoot.activeSelf)
        {
            replyBubbleCanvasGroup.alpha = 0f;
            return;
        }

        replyBubbleFadeCoroutine = StartCoroutine(FadeReplyBubbleCoroutine(visible));
    }

    private IEnumerator FadeReplyBubbleCoroutine(bool visible)
    {
        if (replyBubbleRoot == null || replyBubbleCanvasGroup == null)
        {
            yield break;
        }

        float startAlpha = visible ? 0f : replyBubbleCanvasGroup.alpha;
        float endAlpha = visible ? 1f : 0f;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, bubbleFadeDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            replyBubbleCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            yield return null;
        }

        replyBubbleCanvasGroup.alpha = endAlpha;
        if (!visible)
        {
            replyBubbleRoot.SetActive(false);
        }

        replyBubbleFadeCoroutine = null;
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
