using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Yade.MainMenu
{
    public class MainMenuController : MonoBehaviour
    {
        private const string StorySceneName = "StoryScene";

        [Header("Navigation")]
        [SerializeField] private string sceneToLoad = StorySceneName;
        [SerializeField] private Button newJourneyButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button galleryButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button exitButton;

        [Header("Transition")]
        [SerializeField] private Color transitionColor = Color.black;
        [SerializeField] private float fadeOutDuration = 0.45f;
        [SerializeField] private float fadeInDuration = 0.45f;
        [SerializeField] private float postLoadHoldDuration = 0.35f;

        private UnityAction continueAction;
        private UnityAction galleryAction;
        private UnityAction settingsAction;
        private bool isLoading;
        private CanvasGroup transitionCanvasGroup;

        private void Awake()
        {
            continueAction = () => LogPlaceholder("Continue");
            galleryAction = () => LogPlaceholder("Gallery");
            settingsAction = () => LogPlaceholder("Settings");

            if (newJourneyButton != null)
            {
                newJourneyButton.onClick.AddListener(StartNewJourney);
            }

            if (continueButton != null)
            {
                continueButton.onClick.AddListener(continueAction);
            }

            if (galleryButton != null)
            {
                galleryButton.onClick.AddListener(galleryAction);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(settingsAction);
            }

            if (exitButton != null)
            {
                exitButton.onClick.AddListener(ExitGame);
            }

        }

        private void Start()
        {
            if (newJourneyButton != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(newJourneyButton.gameObject);
            }
        }

        private void OnDestroy()
        {
            if (newJourneyButton != null)
            {
                newJourneyButton.onClick.RemoveListener(StartNewJourney);
            }

            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(continueAction);
            }

            if (galleryButton != null)
            {
                galleryButton.onClick.RemoveListener(galleryAction);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveListener(settingsAction);
            }

            if (exitButton != null)
            {
                exitButton.onClick.RemoveListener(ExitGame);
            }
        }

        public void StartNewJourney()
        {
            if (isLoading)
            {
                return;
            }

            isLoading = true;
            SetMenuInteractable(false);
            SceneTransitionOverlay overlay = CreateTransitionOverlay();
            overlay.Begin(sceneToLoad, fadeOutDuration, fadeInDuration, postLoadHoldDuration, OnTransitionFailed);
        }

        public void ExitGame()
        {
#if UNITY_EDITOR
            Debug.Log("[MainMenu] Exit clicked. In a build, this will close the game.");
#else
            Application.Quit();
#endif
        }

        private static void LogPlaceholder(string menuName)
        {
            Debug.Log($"[MainMenu] {menuName} clicked. This menu is not implemented yet.");
        }

        private void SetMenuInteractable(bool interactable)
        {
            SetButtonInteractable(newJourneyButton, interactable);
            SetButtonInteractable(continueButton, interactable);
            SetButtonInteractable(galleryButton, interactable);
            SetButtonInteractable(settingsButton, interactable);
            SetButtonInteractable(exitButton, interactable);
        }

        private static void SetButtonInteractable(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        private void OnTransitionFailed()
        {
            SetMenuInteractable(true);
            isLoading = false;
        }

        private SceneTransitionOverlay CreateTransitionOverlay()
        {
            GameObject overlay = new GameObject(
                "MainMenuTransitionOverlay",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(CanvasGroup),
                typeof(GraphicRaycaster));
            DontDestroyOnLoad(overlay);

            Canvas canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            CanvasScaler scaler = overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            transitionCanvasGroup = overlay.GetComponent<CanvasGroup>();
            transitionCanvasGroup.alpha = 0f;
            transitionCanvasGroup.blocksRaycasts = true;
            transitionCanvasGroup.interactable = true;

            GameObject imageObject = new GameObject("FadeImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(overlay.transform, false);

            RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            Image image = imageObject.GetComponent<Image>();
            image.color = transitionColor;
            image.raycastTarget = true;

            SceneTransitionOverlay transitionOverlay = overlay.AddComponent<SceneTransitionOverlay>();
            transitionOverlay.Initialize(transitionCanvasGroup);
            return transitionOverlay;
        }
    }

    public class SceneTransitionOverlay : MonoBehaviour
    {
        private CanvasGroup canvasGroup;
        private System.Action onFailed;

        public void Initialize(CanvasGroup transitionCanvasGroup)
        {
            canvasGroup = transitionCanvasGroup;
        }

        public void Begin(
            string sceneToLoad,
            float fadeOutDuration,
            float fadeInDuration,
            float postLoadHoldDuration,
            System.Action transitionFailed)
        {
            onFailed = transitionFailed;
            StartCoroutine(TransitionRoutine(sceneToLoad, fadeOutDuration, fadeInDuration, postLoadHoldDuration));
        }

        private IEnumerator TransitionRoutine(
            string sceneToLoad,
            float fadeOutDuration,
            float fadeInDuration,
            float postLoadHoldDuration)
        {
            yield return Fade(0f, 1f, fadeOutDuration);

            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneToLoad);
            if (loadOperation == null)
            {
                Debug.LogError($"[MainMenu] Could not start loading scene \"{sceneToLoad}\".");
                yield return Fade(1f, 0f, fadeInDuration);
                onFailed?.Invoke();
                Destroy(gameObject);
                yield break;
            }

            while (!loadOperation.isDone)
            {
                yield return null;
            }

            yield return null;
            yield return null;
            yield return Hold(postLoadHoldDuration);
            yield return Fade(1f, 0f, fadeInDuration);
            Destroy(gameObject);
        }

        private static IEnumerator Hold(float duration)
        {
            duration = Mathf.Max(0f, duration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private IEnumerator Fade(float fromAlpha, float toAlpha, float duration)
        {
            if (canvasGroup == null)
            {
                yield break;
            }

            duration = Mathf.Max(0f, duration);
            if (duration <= 0f)
            {
                canvasGroup.alpha = toAlpha;
                yield break;
            }

            canvasGroup.alpha = fromAlpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                canvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            canvasGroup.alpha = toAlpha;
        }
    }
}
