using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;

public class SceneBackgroundPresenter : MonoBehaviour
{
    private const string BackgroundResourcesPath = "Backgrounds/";

    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private float fadeDuration = 0.25f;

    private bool commandRegistered;
    private CanvasGroup backgroundCanvasGroup;
    private Image transitionImage;
    private CanvasGroup transitionCanvasGroup;

    private void Awake()
    {
        if (dialogueRunner == null)
        {
            dialogueRunner = GetComponent<DialogueRunner>();
        }

        if (backgroundImage != null)
        {
            backgroundCanvasGroup = EnsureCanvasGroup(backgroundImage);
            transitionImage = EnsureTransitionImage(backgroundImage);
            transitionCanvasGroup = EnsureCanvasGroup(transitionImage);
        }
    }

    private void OnEnable()
    {
        RegisterCommand();
    }

    private void OnDisable()
    {
        UnregisterCommand();
    }

    private void RegisterCommand()
    {
        if (commandRegistered)
        {
            return;
        }

        if (dialogueRunner == null)
        {
            dialogueRunner = GetComponent<DialogueRunner>();
        }

        if (dialogueRunner == null)
        {
            Debug.LogWarning("SceneBackgroundPresenter requires a DialogueRunner reference.");
            return;
        }

        dialogueRunner.AddCommandHandler("background", (Func<string, YarnTask>)SetBackgroundAsync);
        commandRegistered = true;
    }

    private void UnregisterCommand()
    {
        if (!commandRegistered || dialogueRunner == null)
        {
            return;
        }

        dialogueRunner.RemoveCommandHandler("background");
        commandRegistered = false;
    }

    private async YarnTask SetBackgroundAsync(string backgroundName)
    {
        if (backgroundImage == null)
        {
            Debug.LogWarning("SceneBackgroundPresenter requires a background Image reference.");
            return;
        }

        if (string.IsNullOrWhiteSpace(backgroundName))
        {
            Debug.LogWarning("Command <<background>> requires a background name.");
            return;
        }

        string normalizedName = backgroundName.Trim();
        backgroundCanvasGroup = EnsureCanvasGroup(backgroundImage);
        transitionImage = EnsureTransitionImage(backgroundImage);
        transitionCanvasGroup = EnsureCanvasGroup(transitionImage);

        if (string.Equals(normalizedName, "none", StringComparison.OrdinalIgnoreCase))
        {
            await SetBackgroundVisibilityAsync(false);
            backgroundImage.sprite = null;
            transitionImage.sprite = null;
            return;
        }

        Sprite sprite = Resources.Load<Sprite>(BackgroundResourcesPath + normalizedName);
        if (sprite == null)
        {
            Debug.LogWarning($"Background \"{normalizedName}\" was not found at Resources/{BackgroundResourcesPath}{normalizedName}.");
            return;
        }

        float duration = Mathf.Max(0f, fadeDuration);
        bool hasVisibleBackground = backgroundImage.sprite != null && backgroundImage.gameObject.activeSelf && backgroundCanvasGroup.alpha > 0f;

        if (duration <= 0f)
        {
            backgroundImage.sprite = sprite;
            backgroundImage.gameObject.SetActive(true);
            backgroundCanvasGroup.alpha = 1f;
            transitionCanvasGroup.alpha = 0f;
            transitionImage.gameObject.SetActive(false);
            return;
        }

        if (!hasVisibleBackground)
        {
            backgroundImage.sprite = sprite;
            backgroundImage.gameObject.SetActive(true);
            backgroundCanvasGroup.alpha = 0f;
            await Effects.FadeAlphaAsync(backgroundCanvasGroup, 0f, 1f, duration, CancellationToken.None);
            return;
        }

        transitionImage.sprite = sprite;
        transitionImage.gameObject.SetActive(true);
        transitionCanvasGroup.alpha = 0f;

        await YarnTask.WhenAll(
            Effects.FadeAlphaAsync(backgroundCanvasGroup, backgroundCanvasGroup.alpha, 0f, duration, CancellationToken.None),
            Effects.FadeAlphaAsync(transitionCanvasGroup, 0f, 1f, duration, CancellationToken.None)
        );

        backgroundImage.sprite = sprite;
        backgroundCanvasGroup.alpha = 1f;
        transitionCanvasGroup.alpha = 0f;
        transitionImage.sprite = null;
        transitionImage.gameObject.SetActive(false);
    }

    private async YarnTask SetBackgroundVisibilityAsync(bool visible)
    {
        float targetAlpha = visible ? 1f : 0f;
        float duration = Mathf.Max(0f, fadeDuration);

        if (visible && !backgroundImage.gameObject.activeSelf)
        {
            backgroundImage.gameObject.SetActive(true);
        }

        if (duration <= 0f || Mathf.Approximately(backgroundCanvasGroup.alpha, targetAlpha))
        {
            backgroundCanvasGroup.alpha = targetAlpha;
        }
        else
        {
            await Effects.FadeAlphaAsync(backgroundCanvasGroup, backgroundCanvasGroup.alpha, targetAlpha, duration, CancellationToken.None);
        }

        if (!visible)
        {
            backgroundCanvasGroup.alpha = 0f;
            transitionCanvasGroup.alpha = 0f;
            backgroundImage.gameObject.SetActive(false);
            transitionImage.gameObject.SetActive(false);
        }
    }

    private static CanvasGroup EnsureCanvasGroup(Image image)
    {
        var canvasGroup = image.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = image.gameObject.AddComponent<CanvasGroup>();
        }

        return canvasGroup;
    }

    private static Image EnsureTransitionImage(Image sourceImage)
    {
        Transform parent = sourceImage.transform.parent;
        string transitionName = sourceImage.gameObject.name + "_Transition";
        Transform existing = parent.Find(transitionName);
        if (existing != null)
        {
            return existing.GetComponent<Image>();
        }

        var transitionObject = new GameObject(transitionName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var transitionTransform = transitionObject.GetComponent<RectTransform>();
        transitionTransform.SetParent(parent, false);
        transitionTransform.SetSiblingIndex(sourceImage.transform.GetSiblingIndex() + 1);

        RectTransform sourceRect = sourceImage.rectTransform;
        transitionTransform.anchorMin = sourceRect.anchorMin;
        transitionTransform.anchorMax = sourceRect.anchorMax;
        transitionTransform.anchoredPosition = sourceRect.anchoredPosition;
        transitionTransform.sizeDelta = sourceRect.sizeDelta;
        transitionTransform.pivot = sourceRect.pivot;
        transitionTransform.localScale = sourceRect.localScale;
        transitionTransform.localRotation = sourceRect.localRotation;

        var image = transitionObject.GetComponent<Image>();
        image.color = sourceImage.color;
        image.material = sourceImage.material;
        image.type = sourceImage.type;
        image.preserveAspect = sourceImage.preserveAspect;
        image.raycastTarget = false;
        transitionObject.SetActive(false);
        return image;
    }
}
