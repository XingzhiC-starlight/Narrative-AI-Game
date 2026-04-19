using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;

[Serializable]
public struct SpeakerPortraitBinding
{
    public string speakerName;
    public Image portraitImage;
}

public class CharacterPortraitPresenter : DialoguePresenterBase
{
    private const string PortraitResourcesPath = "Characters/";
    private const int MaxVisiblePortraits = 2;
    private const float KnightPortraitScale = 1.2f;

    private struct PortraitLayoutState
    {
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 anchoredPosition;
        public Vector3 localScale;
    }

    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private GameObject portraitContainer;
    [SerializeField] private List<SpeakerPortraitBinding> portraits = new List<SpeakerPortraitBinding>();
    [SerializeField] private Color highlightColor = Color.white;
    [SerializeField] private Color dimColor = new Color(0.45f, 0.45f, 0.45f, 1f);
    [SerializeField] private float fadeDuration = 0.25f;
    [SerializeField] private bool showOnDialogueStart = true;
    [SerializeField] private bool hideOnDialogueComplete = true;

    private readonly Dictionary<Image, CanvasGroup> canvasGroups = new Dictionary<Image, CanvasGroup>();
    private readonly Dictionary<Image, PortraitLayoutState> initialLayout = new Dictionary<Image, PortraitLayoutState>();
    private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>(StringComparer.Ordinal);
    private readonly List<string> currentPortraitNames = new List<string>(MaxVisiblePortraits);

    private string lastKnownSpeakerName;
    private bool portraitCommandRegistered;

    private void Awake()
    {
        if (dialogueRunner == null)
        {
            dialogueRunner = GetComponent<DialogueRunner>();
        }

        RebuildSlots();
    }

    private void OnEnable()
    {
        RegisterPortraitCommand();
    }

    private void OnDisable()
    {
        UnregisterPortraitCommand();
    }

    public override async YarnTask OnDialogueStartedAsync()
    {
        RebuildSlots();
        currentPortraitNames.Clear();
        lastKnownSpeakerName = null;

        if (showOnDialogueStart)
        {
            foreach (var binding in portraits)
            {
                if (currentPortraitNames.Count >= MaxVisiblePortraits)
                {
                    break;
                }

                if (!string.IsNullOrWhiteSpace(binding.speakerName))
                {
                    currentPortraitNames.Add(binding.speakerName.Trim());
                }
            }
        }

        await ApplyDisplayStateAsync();
        SetLineHighlight(null);
    }

    public override YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
    {
        if (!string.IsNullOrWhiteSpace(line.CharacterName))
        {
            lastKnownSpeakerName = line.CharacterName;
        }

        SetLineHighlight(line.CharacterName);
        return YarnTask.CompletedTask;
    }

    public override async YarnTask OnDialogueCompleteAsync()
    {
        SetLineHighlight(null);

        if (hideOnDialogueComplete)
        {
            currentPortraitNames.Clear();
            await ApplyDisplayStateAsync();
        }
    }

    private void RegisterPortraitCommand()
    {
        if (portraitCommandRegistered)
        {
            return;
        }

        if (dialogueRunner == null)
        {
            dialogueRunner = GetComponent<DialogueRunner>();
        }

        if (dialogueRunner == null)
        {
            return;
        }

        dialogueRunner.AddCommandHandler<string[]>("portrait", SetPortraitCommandAsync);
        portraitCommandRegistered = true;
    }

    private void UnregisterPortraitCommand()
    {
        if (!portraitCommandRegistered || dialogueRunner == null)
        {
            return;
        }

        dialogueRunner.RemoveCommandHandler("portrait");
        portraitCommandRegistered = false;
    }

    private async YarnTask SetPortraitCommandAsync(params string[] speakerNames)
    {
        var requestedNames = new List<string>(MaxVisiblePortraits);

        if (speakerNames != null)
        {
            foreach (string speakerName in speakerNames)
            {
                if (string.IsNullOrWhiteSpace(speakerName))
                {
                    continue;
                }

                if (requestedNames.Count >= MaxVisiblePortraits)
                {
                    Debug.LogWarning("Command <<portrait>> supports at most two portrait names; extra names were ignored.");
                    break;
                }

                requestedNames.Add(speakerName.Trim());
            }
        }

        if (requestedNames.Count == 0)
        {
            Debug.LogWarning("Command <<portrait>> requires 'none' or one/two portrait names.");
            return;
        }

        if (string.Equals(requestedNames[0], "none", StringComparison.OrdinalIgnoreCase))
        {
            if (requestedNames.Count > 1)
            {
                Debug.LogWarning("Command <<portrait none>> does not accept additional portrait names.");
                return;
            }

            currentPortraitNames.Clear();
            await ApplyDisplayStateAsync();
            SetLineHighlight(lastKnownSpeakerName);
            return;
        }

        foreach (string requestedName in requestedNames)
        {
            if (LoadPortraitSprite(requestedName) == null)
            {
                Debug.LogWarning($"Portrait \"{requestedName}\" was not found at Resources/{PortraitResourcesPath}{requestedName}.");
                return;
            }
        }

        currentPortraitNames.Clear();
        currentPortraitNames.AddRange(requestedNames);

        await ApplyDisplayStateAsync();
        SetLineHighlight(lastKnownSpeakerName);
    }

    private void RebuildSlots()
    {
        canvasGroups.Clear();
        initialLayout.Clear();

        foreach (var binding in portraits)
        {
            if (binding.portraitImage == null)
            {
                continue;
            }

            canvasGroups[binding.portraitImage] = EnsureCanvasGroup(binding.portraitImage);
            initialLayout[binding.portraitImage] = CaptureLayout(binding.portraitImage.rectTransform);
        }
    }

    private async YarnTask ApplyDisplayStateAsync()
    {
        bool showContainer = currentPortraitNames.Count > 0;

        if (portraitContainer != null && showContainer)
        {
            portraitContainer.SetActive(true);
        }

        if (showContainer)
        {
            for (int i = 0; i < portraits.Count; i++)
            {
                Image image = portraits[i].portraitImage;
                if (image == null)
                {
                    continue;
                }

                RestoreLayout(image.rectTransform);
            }

            if (currentPortraitNames.Count == 1 && portraits.Count > 0 && portraits[0].portraitImage != null)
            {
                ApplyCenteredLayout(portraits[0].portraitImage.rectTransform);
            }
        }

        var fadeTasks = new List<YarnTask>();
        for (int i = 0; i < portraits.Count; i++)
        {
            Image image = portraits[i].portraitImage;
            if (image == null)
            {
                continue;
            }

            bool shouldShow = i < currentPortraitNames.Count;
            if (shouldShow)
            {
                image.sprite = LoadPortraitSprite(currentPortraitNames[i]);
                ApplyPortraitScale(image.rectTransform, currentPortraitNames[i]);
            }

            fadeTasks.Add(FadePortraitVisibilityAsync(image, shouldShow));
        }

        if (fadeTasks.Count > 0)
        {
            await YarnTask.WhenAll(fadeTasks);
        }

        if (portraitContainer != null && !showContainer)
        {
            portraitContainer.SetActive(false);
        }

        if (!showContainer)
        {
            for (int i = 0; i < portraits.Count; i++)
            {
                Image image = portraits[i].portraitImage;
                if (image == null)
                {
                    continue;
                }

                RestoreLayout(image.rectTransform);
            }
        }
    }

    private void SetLineHighlight(string speakerName)
    {
        bool hasSpeaker = !string.IsNullOrWhiteSpace(speakerName);

        for (int i = 0; i < portraits.Count; i++)
        {
            Image image = portraits[i].portraitImage;
            if (image == null || i >= currentPortraitNames.Count)
            {
                continue;
            }

            image.color = hasSpeaker && currentPortraitNames[i] == speakerName ? highlightColor : dimColor;
        }
    }

    private Sprite LoadPortraitSprite(string portraitName)
    {
        if (spriteCache.TryGetValue(portraitName, out Sprite cachedSprite))
        {
            return cachedSprite;
        }

        string resourcePath = PortraitResourcesPath + portraitName;
        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
        Sprite sprite = SelectSprite(portraitName, sprites);

        if (sprite == null)
        {
            sprite = Resources.Load<Sprite>(resourcePath);
        }

        if (sprite != null)
        {
            spriteCache[portraitName] = sprite;
        }

        return sprite;
    }

    private static Sprite SelectSprite(string portraitName, Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
        {
            return null;
        }

        string firstSliceName = portraitName + "_0";
        foreach (Sprite sprite in sprites)
        {
            if (sprite != null && string.Equals(sprite.name, portraitName, StringComparison.Ordinal))
            {
                return sprite;
            }
        }

        foreach (Sprite sprite in sprites)
        {
            if (sprite != null && string.Equals(sprite.name, firstSliceName, StringComparison.Ordinal))
            {
                return sprite;
            }
        }

        Sprite largestSprite = null;
        float largestArea = -1f;
        foreach (Sprite sprite in sprites)
        {
            if (sprite == null)
            {
                continue;
            }

            float area = sprite.rect.width * sprite.rect.height;
            if (area > largestArea)
            {
                largestSprite = sprite;
                largestArea = area;
            }
        }

        return largestSprite;
    }

    private async YarnTask FadePortraitVisibilityAsync(Image image, bool visible)
    {
        var canvasGroup = EnsureCanvasGroup(image);
        float targetAlpha = visible ? 1f : 0f;

        if (visible && !image.gameObject.activeSelf)
        {
            image.gameObject.SetActive(true);
        }

        float duration = Mathf.Max(0f, fadeDuration);
        if (duration <= 0f || Mathf.Approximately(canvasGroup.alpha, targetAlpha))
        {
            canvasGroup.alpha = targetAlpha;
        }
        else
        {
            await Effects.FadeAlphaAsync(canvasGroup, canvasGroup.alpha, targetAlpha, duration, CancellationToken.None);
        }

        if (!visible)
        {
            image.gameObject.SetActive(false);
        }
    }

    private CanvasGroup EnsureCanvasGroup(Image image)
    {
        if (canvasGroups.TryGetValue(image, out var existing) && existing != null)
        {
            return existing;
        }

        var canvasGroup = image.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = image.gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroups[image] = canvasGroup;
        return canvasGroup;
    }

    private static PortraitLayoutState CaptureLayout(RectTransform rectTransform)
    {
        return new PortraitLayoutState
        {
            anchorMin = rectTransform.anchorMin,
            anchorMax = rectTransform.anchorMax,
            pivot = rectTransform.pivot,
            anchoredPosition = rectTransform.anchoredPosition,
            localScale = rectTransform.localScale,
        };
    }

    private void RestoreLayout(RectTransform rectTransform)
    {
        foreach (var pair in initialLayout)
        {
            if (pair.Key.rectTransform != rectTransform)
            {
                continue;
            }

            rectTransform.anchorMin = pair.Value.anchorMin;
            rectTransform.anchorMax = pair.Value.anchorMax;
            rectTransform.pivot = pair.Value.pivot;
            rectTransform.anchoredPosition = pair.Value.anchoredPosition;
            rectTransform.localScale = pair.Value.localScale;
            return;
        }
    }

    private static void ApplyPortraitScale(RectTransform rectTransform, string portraitName)
    {
        float scale = string.Equals(portraitName, "骑士", StringComparison.Ordinal) ? KnightPortraitScale : 1f;
        rectTransform.localScale = new Vector3(scale, scale, 1f);
    }

    private static void ApplyCenteredLayout(RectTransform rectTransform)
    {
        rectTransform.anchorMin = new Vector2(0.5f, 0f);
        rectTransform.anchorMax = new Vector2(0.5f, 0f);
        rectTransform.pivot = new Vector2(0.5f, 0f);
        rectTransform.anchoredPosition = new Vector2(0f, rectTransform.anchoredPosition.y);
    }
}
