using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Yarn.Unity;

public class SceneBackgroundPresenter : MonoBehaviour
{
    private const string BackgroundResourcesPath = "Backgrounds/";
    private const string AnimatedBackgroundResourcesPath = "AnimatedBackgrounds/";

    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private RawImage backgroundVideoImage;
    [SerializeField] private VideoPlayer backgroundVideoPlayer;
    [SerializeField] private RenderTexture backgroundVideoTexture;
    [SerializeField] private float fadeDuration = 0.25f;
    [SerializeField] private float videoPrepareTimeout = 2f;

    private bool commandRegistered;
    private CanvasGroup backgroundCanvasGroup;
    private Image transitionImage;
    private CanvasGroup transitionCanvasGroup;
    private CanvasGroup backgroundVideoCanvasGroup;
    private RawImage transitionVideoImage;
    private VideoPlayer transitionVideoPlayer;
    private RenderTexture transitionVideoTexture;
    private CanvasGroup transitionVideoCanvasGroup;
    private RenderTexture runtimeTransitionVideoTexture;
    private int backgroundChangeVersion;
    private int backgroundLayerBaseIndex = -1;

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

        if (backgroundVideoImage != null)
        {
            backgroundVideoCanvasGroup = EnsureCanvasGroup(backgroundVideoImage);
            backgroundVideoCanvasGroup.alpha = 0f;
            backgroundVideoImage.gameObject.SetActive(false);
            EnsureTransitionVideoBackground();
        }

        CacheBackgroundLayerBaseIndex();
        MaintainBackgroundLayerOrder();
    }

    private void OnEnable()
    {
        RegisterCommand();
    }

    private void OnDisable()
    {
        UnregisterCommand();
    }

    private void OnDestroy()
    {
        if (runtimeTransitionVideoTexture != null)
        {
            runtimeTransitionVideoTexture.Release();
            Destroy(runtimeTransitionVideoTexture);
        }
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
        backgroundVideoCanvasGroup = backgroundVideoImage != null ? EnsureCanvasGroup(backgroundVideoImage) : null;
        EnsureTransitionVideoBackground();
        MaintainBackgroundLayerOrder();

        if (string.Equals(normalizedName, "none", StringComparison.OrdinalIgnoreCase))
        {
            backgroundChangeVersion++;
            await SetBackgroundVisibilityAsync(false);
            StopVideoBackground();
            backgroundImage.sprite = null;
            transitionImage.sprite = null;
            return;
        }

        VideoClip videoClip = Resources.Load<VideoClip>(AnimatedBackgroundResourcesPath + normalizedName);
        if (videoClip != null && HasVideoBackgroundReferences(normalizedName))
        {
            await SetVideoBackgroundAsync(videoClip);
            return;
        }

        Sprite sprite = Resources.Load<Sprite>(BackgroundResourcesPath + normalizedName);
        if (sprite == null)
        {
            Debug.LogWarning($"Background \"{normalizedName}\" was not found at Resources/{AnimatedBackgroundResourcesPath}{normalizedName} or Resources/{BackgroundResourcesPath}{normalizedName}.");
            return;
        }

        await SetSpriteBackgroundAsync(sprite);
    }

    private async YarnTask SetSpriteBackgroundAsync(Sprite sprite)
    {
        int changeVersion = ++backgroundChangeVersion;
        float duration = Mathf.Max(0f, fadeDuration);
        bool hasVisibleBackground = backgroundImage.sprite != null && backgroundImage.gameObject.activeSelf && backgroundCanvasGroup.alpha > 0f;
        bool hasVisibleVideo = backgroundVideoImage != null && backgroundVideoImage.gameObject.activeSelf && backgroundVideoCanvasGroup != null && backgroundVideoCanvasGroup.alpha > 0f;

        if (duration <= 0f)
        {
            StopVideoBackground();
            backgroundImage.sprite = sprite;
            backgroundImage.gameObject.SetActive(true);
            backgroundCanvasGroup.alpha = 1f;
            transitionCanvasGroup.alpha = 0f;
            transitionImage.gameObject.SetActive(false);
            HideVideoBackground();
            return;
        }

        if (hasVisibleVideo)
        {
            transitionImage.sprite = sprite;
            transitionImage.gameObject.SetActive(true);
            transitionCanvasGroup.alpha = 0f;

            await YarnTask.WhenAll(
                Effects.FadeAlphaAsync(backgroundVideoCanvasGroup, backgroundVideoCanvasGroup.alpha, 0f, duration, CancellationToken.None),
                Effects.FadeAlphaAsync(transitionCanvasGroup, 0f, 1f, duration, CancellationToken.None)
            );

            if (changeVersion != backgroundChangeVersion)
            {
                return;
            }

            StopVideoBackground();
            backgroundImage.sprite = sprite;
            backgroundImage.gameObject.SetActive(true);
            backgroundCanvasGroup.alpha = 1f;
            transitionCanvasGroup.alpha = 0f;
            transitionImage.sprite = null;
            transitionImage.gameObject.SetActive(false);
            HideVideoBackground();
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

        if (changeVersion != backgroundChangeVersion)
        {
            return;
        }

        backgroundImage.sprite = sprite;
        backgroundCanvasGroup.alpha = 1f;
        transitionCanvasGroup.alpha = 0f;
        transitionImage.sprite = null;
        transitionImage.gameObject.SetActive(false);
    }

    private async YarnTask SetVideoBackgroundAsync(VideoClip videoClip)
    {
        int changeVersion = ++backgroundChangeVersion;
        float duration = Mathf.Max(0f, fadeDuration);
        bool hasVisibleImage = backgroundImage.gameObject.activeSelf && backgroundCanvasGroup.alpha > 0f;
        bool hasVisibleVideo = backgroundVideoImage.gameObject.activeSelf && backgroundVideoCanvasGroup.alpha > 0f;

        if (duration > 0f && hasVisibleVideo && EnsureTransitionVideoBackground())
        {
            await CrossfadeVideoBackgroundAsync(videoClip, changeVersion, duration);
            return;
        }

        backgroundVideoImage.texture = backgroundVideoTexture;
        backgroundVideoImage.gameObject.SetActive(true);
        backgroundVideoCanvasGroup.alpha = 0f;

        ConfigureVideoPlayer(backgroundVideoPlayer, videoClip, backgroundVideoTexture);

        await PrepareVideoAsync(backgroundVideoPlayer, changeVersion);

        if (changeVersion != backgroundChangeVersion)
        {
            return;
        }

        backgroundVideoPlayer.Play();

        if (duration <= 0f)
        {
            backgroundVideoCanvasGroup.alpha = 1f;
            HideSpriteBackground();
            return;
        }

        if (hasVisibleImage)
        {
            await YarnTask.WhenAll(
                Effects.FadeAlphaAsync(backgroundCanvasGroup, backgroundCanvasGroup.alpha, 0f, duration, CancellationToken.None),
                Effects.FadeAlphaAsync(backgroundVideoCanvasGroup, backgroundVideoCanvasGroup.alpha, 1f, duration, CancellationToken.None)
            );
        }
        else
        {
            await Effects.FadeAlphaAsync(backgroundVideoCanvasGroup, 0f, 1f, duration, CancellationToken.None);
        }

        if (changeVersion != backgroundChangeVersion)
        {
            return;
        }

        backgroundVideoCanvasGroup.alpha = 1f;
        HideSpriteBackground();
    }

    private async YarnTask CrossfadeVideoBackgroundAsync(VideoClip videoClip, int changeVersion, float duration)
    {
        transitionVideoImage.texture = transitionVideoTexture;
        transitionVideoImage.gameObject.SetActive(true);
        MaintainBackgroundLayerOrder();
        transitionVideoCanvasGroup.alpha = 0f;

        ConfigureVideoPlayer(transitionVideoPlayer, videoClip, transitionVideoTexture);
        await PrepareVideoAsync(transitionVideoPlayer, changeVersion);

        if (changeVersion != backgroundChangeVersion)
        {
            return;
        }

        transitionVideoPlayer.Play();

        await YarnTask.WhenAll(
            Effects.FadeAlphaAsync(backgroundVideoCanvasGroup, backgroundVideoCanvasGroup.alpha, 0f, duration, CancellationToken.None),
            Effects.FadeAlphaAsync(transitionVideoCanvasGroup, 0f, 1f, duration, CancellationToken.None)
        );

        if (changeVersion != backgroundChangeVersion)
        {
            return;
        }

        backgroundVideoPlayer.Stop();
        SwapVideoBackgroundBuffers();
        backgroundVideoCanvasGroup.alpha = 1f;
        backgroundVideoImage.gameObject.SetActive(true);
        transitionVideoCanvasGroup.alpha = 0f;
        transitionVideoImage.gameObject.SetActive(false);
        HideSpriteBackground();
    }

    private async YarnTask PrepareVideoAsync(VideoPlayer videoPlayer, int changeVersion)
    {
        videoPlayer.Prepare();
        float elapsed = 0f;
        float timeout = Mathf.Max(0f, videoPrepareTimeout);

        while (changeVersion == backgroundChangeVersion && !videoPlayer.isPrepared && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            await YarnTask.Yield();
        }
    }

    private async YarnTask SetBackgroundVisibilityAsync(bool visible)
    {
        float targetAlpha = visible ? 1f : 0f;
        float duration = Mathf.Max(0f, fadeDuration);
        bool hasVisibleVideo = backgroundVideoCanvasGroup != null && backgroundVideoImage != null && backgroundVideoImage.gameObject.activeSelf && backgroundVideoCanvasGroup.alpha > 0f;

        if (visible && !backgroundImage.gameObject.activeSelf)
        {
            backgroundImage.gameObject.SetActive(true);
        }

        if (duration <= 0f)
        {
            backgroundCanvasGroup.alpha = targetAlpha;
            if (backgroundVideoCanvasGroup != null)
            {
                backgroundVideoCanvasGroup.alpha = targetAlpha;
            }
        }
        else if (!visible && hasVisibleVideo)
        {
            await YarnTask.WhenAll(
                Effects.FadeAlphaAsync(backgroundCanvasGroup, backgroundCanvasGroup.alpha, targetAlpha, duration, CancellationToken.None),
                Effects.FadeAlphaAsync(backgroundVideoCanvasGroup, backgroundVideoCanvasGroup.alpha, targetAlpha, duration, CancellationToken.None)
            );
        }
        else if (Mathf.Approximately(backgroundCanvasGroup.alpha, targetAlpha))
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
            HideVideoBackground();
            StopVideoBackground();
        }
    }

    private bool HasVideoBackgroundReferences(string backgroundName)
    {
        bool hasReferences = backgroundVideoImage != null && backgroundVideoPlayer != null && backgroundVideoTexture != null;
        if (!hasReferences)
        {
            Debug.LogWarning($"Animated background \"{backgroundName}\" was found, but SceneBackgroundPresenter is missing Background Video Image, Background Video Player, or Background Video Texture. Falling back to static background if one exists.");
        }

        return hasReferences;
    }

    private bool EnsureTransitionVideoBackground()
    {
        if (backgroundVideoImage == null || backgroundVideoPlayer == null || backgroundVideoTexture == null)
        {
            return false;
        }

        if (transitionVideoImage == null)
        {
            Transform parent = backgroundVideoImage.transform.parent;
            string transitionName = backgroundVideoImage.gameObject.name + "_Transition";
            Transform existing = parent.Find(transitionName);

            if (existing != null)
            {
                transitionVideoImage = existing.GetComponent<RawImage>();
                transitionVideoPlayer = existing.GetComponent<VideoPlayer>();
            }
            else
            {
                var transitionObject = new GameObject(transitionName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(VideoPlayer));
                transitionObject.transform.SetParent(parent, false);
                transitionVideoImage = transitionObject.GetComponent<RawImage>();
                transitionVideoPlayer = transitionObject.GetComponent<VideoPlayer>();
            }

            CopyRectTransform(backgroundVideoImage.rectTransform, transitionVideoImage.rectTransform);
            transitionVideoImage.color = backgroundVideoImage.color;
            transitionVideoImage.material = backgroundVideoImage.material;
            transitionVideoImage.raycastTarget = false;
        }

        if (transitionVideoPlayer == null)
        {
            transitionVideoPlayer = transitionVideoImage.gameObject.AddComponent<VideoPlayer>();
        }

        if (transitionVideoTexture == null)
        {
            transitionVideoTexture = CreateTransitionRenderTexture(backgroundVideoTexture);
            runtimeTransitionVideoTexture = transitionVideoTexture;
        }

        transitionVideoCanvasGroup = EnsureCanvasGroup(transitionVideoImage);
        transitionVideoCanvasGroup.alpha = 0f;
        transitionVideoImage.texture = transitionVideoTexture;
        transitionVideoImage.gameObject.SetActive(false);
        MaintainBackgroundLayerOrder();
        return true;
    }

    private static RenderTexture CreateTransitionRenderTexture(RenderTexture source)
    {
        var renderTexture = new RenderTexture(source.width, source.height, source.depth, source.graphicsFormat)
        {
            name = source.name + "_Transition",
            antiAliasing = source.antiAliasing,
            filterMode = source.filterMode,
            wrapMode = source.wrapMode,
            useMipMap = source.useMipMap,
            autoGenerateMips = source.autoGenerateMips
        };
        renderTexture.Create();
        return renderTexture;
    }

    private static void ConfigureVideoPlayer(VideoPlayer videoPlayer, VideoClip videoClip, RenderTexture targetTexture)
    {
        videoPlayer.Stop();
        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.clip = videoClip;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = targetTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPlayer.isLooping = true;
        videoPlayer.playOnAwake = false;
    }

    private void SwapVideoBackgroundBuffers()
    {
        (backgroundVideoImage, transitionVideoImage) = (transitionVideoImage, backgroundVideoImage);
        (backgroundVideoPlayer, transitionVideoPlayer) = (transitionVideoPlayer, backgroundVideoPlayer);
        (backgroundVideoTexture, transitionVideoTexture) = (transitionVideoTexture, backgroundVideoTexture);
        (backgroundVideoCanvasGroup, transitionVideoCanvasGroup) = (transitionVideoCanvasGroup, backgroundVideoCanvasGroup);
        MaintainBackgroundLayerOrder();
    }

    private void CacheBackgroundLayerBaseIndex()
    {
        if (backgroundLayerBaseIndex >= 0)
        {
            return;
        }

        backgroundLayerBaseIndex = int.MaxValue;

        if (backgroundImage != null)
        {
            backgroundLayerBaseIndex = Mathf.Min(backgroundLayerBaseIndex, backgroundImage.transform.GetSiblingIndex());
        }

        if (backgroundVideoImage != null)
        {
            backgroundLayerBaseIndex = Mathf.Min(backgroundLayerBaseIndex, backgroundVideoImage.transform.GetSiblingIndex());
        }

        if (backgroundLayerBaseIndex == int.MaxValue)
        {
            backgroundLayerBaseIndex = 0;
        }
    }

    private void MaintainBackgroundLayerOrder()
    {
        CacheBackgroundLayerBaseIndex();

        Transform parent = backgroundImage != null
            ? backgroundImage.transform.parent
            : backgroundVideoImage != null
                ? backgroundVideoImage.transform.parent
                : null;

        if (parent == null)
        {
            return;
        }

        int siblingIndex = backgroundLayerBaseIndex;

        if (backgroundImage != null && backgroundImage.transform.parent == parent)
        {
            backgroundImage.transform.SetSiblingIndex(siblingIndex++);
        }

        if (backgroundVideoImage != null && backgroundVideoImage.transform.parent == parent)
        {
            backgroundVideoImage.transform.SetSiblingIndex(siblingIndex++);
        }

        if (transitionImage != null && transitionImage.transform.parent == parent)
        {
            transitionImage.transform.SetSiblingIndex(siblingIndex++);
        }

        if (transitionVideoImage != null && transitionVideoImage.transform.parent == parent)
        {
            transitionVideoImage.transform.SetSiblingIndex(siblingIndex);
        }
    }

    private void HideSpriteBackground()
    {
        backgroundCanvasGroup.alpha = 0f;
        transitionCanvasGroup.alpha = 0f;
        backgroundImage.sprite = null;
        transitionImage.sprite = null;
        backgroundImage.gameObject.SetActive(false);
        transitionImage.gameObject.SetActive(false);
    }

    private void HideVideoBackground()
    {
        if (backgroundVideoImage == null || backgroundVideoCanvasGroup == null)
        {
            return;
        }

        backgroundVideoCanvasGroup.alpha = 0f;
        backgroundVideoImage.gameObject.SetActive(false);

        if (transitionVideoCanvasGroup != null)
        {
            transitionVideoCanvasGroup.alpha = 0f;
        }

        if (transitionVideoImage != null)
        {
            transitionVideoImage.gameObject.SetActive(false);
        }
    }

    private void StopVideoBackground()
    {
        if (backgroundVideoPlayer != null)
        {
            backgroundVideoPlayer.Stop();
            backgroundVideoPlayer.clip = null;
        }

        if (transitionVideoPlayer != null)
        {
            transitionVideoPlayer.Stop();
            transitionVideoPlayer.clip = null;
        }
    }

    private static CanvasGroup EnsureCanvasGroup(Graphic graphic)
    {
        var canvasGroup = graphic.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = graphic.gameObject.AddComponent<CanvasGroup>();
        }

        return canvasGroup;
    }

    private static void CopyRectTransform(RectTransform source, RectTransform target)
    {
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
        target.pivot = source.pivot;
        target.localScale = source.localScale;
        target.localRotation = source.localRotation;
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

        CopyRectTransform(sourceImage.rectTransform, transitionTransform);

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
