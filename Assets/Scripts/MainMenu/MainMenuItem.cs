using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Yade.MainMenu
{
    public class MainMenuItem : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        [SerializeField] private bool activeOnStart;
        [SerializeField] private Graphic markerGraphic;
        [SerializeField] private TMP_Text englishText;
        [SerializeField] private TMP_Text chineseText;

        [Header("Colors")]
        [SerializeField] private Color markerNormalColor = new Color(0.533f, 0.533f, 0.722f, 0.35f);
        [SerializeField] private Color markerActiveColor = new Color(0.784f, 0.753f, 0.878f, 1f);
        [SerializeField] private Color englishNormalColor = new Color(0.659f, 0.659f, 0.816f, 0.4f);
        [SerializeField] private Color englishActiveColor = new Color(0.941f, 0.918f, 0.878f, 1f);
        [SerializeField] private Color chineseNormalColor = new Color(0.659f, 0.659f, 0.816f, 0.2f);
        [SerializeField] private Color chineseActiveColor = new Color(0.659f, 0.659f, 0.816f, 0.45f);

        [Header("Motion")]
        [SerializeField] private float activeMarkerScale = 1.35f;
        [SerializeField] private float normalMarkerScale = 1f;
        [SerializeField] private float transitionSpeed = 10f;

        private bool targetActive;

        private void Start()
        {
            SetActiveVisual(activeOnStart, true);
        }

        private void Update()
        {
            float t = 1f - Mathf.Exp(-transitionSpeed * Time.unscaledDeltaTime);

            if (markerGraphic != null)
            {
                markerGraphic.color = Color.Lerp(markerGraphic.color, targetActive ? markerActiveColor : markerNormalColor, t);
                float targetScale = targetActive ? activeMarkerScale : normalMarkerScale;
                markerGraphic.rectTransform.localScale = Vector3.Lerp(markerGraphic.rectTransform.localScale, Vector3.one * targetScale, t);
            }

            if (englishText != null)
            {
                englishText.color = Color.Lerp(englishText.color, targetActive ? englishActiveColor : englishNormalColor, t);
            }

            if (chineseText != null)
            {
                chineseText.color = Color.Lerp(chineseText.color, targetActive ? chineseActiveColor : chineseNormalColor, t);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Activate(true);
        }

        public void OnSelect(BaseEventData eventData)
        {
            Activate(false);
        }

        public void Activate()
        {
            Activate(true);
        }

        private void Activate(bool updateEventSystemSelection)
        {
            MainMenuItem[] siblings = transform.parent != null
                ? transform.parent.GetComponentsInChildren<MainMenuItem>(true)
                : FindObjectsByType<MainMenuItem>(FindObjectsSortMode.None);

            foreach (MainMenuItem item in siblings)
            {
                item.SetActiveVisual(item == this, false);
            }

            if (updateEventSystemSelection
                && EventSystem.current != null
                && EventSystem.current.currentSelectedGameObject != gameObject)
            {
                EventSystem.current.SetSelectedGameObject(gameObject);
            }
        }

        public void SetActiveVisual(bool isActive)
        {
            SetActiveVisual(isActive, false);
        }

        private void SetActiveVisual(bool isActive, bool immediate)
        {
            targetActive = isActive;

            if (!immediate)
            {
                return;
            }

            if (markerGraphic != null)
            {
                markerGraphic.color = isActive ? markerActiveColor : markerNormalColor;
                markerGraphic.rectTransform.localScale = Vector3.one * (isActive ? activeMarkerScale : normalMarkerScale);
            }

            if (englishText != null)
            {
                englishText.color = isActive ? englishActiveColor : englishNormalColor;
            }

            if (chineseText != null)
            {
                chineseText.color = isActive ? chineseActiveColor : chineseNormalColor;
            }
        }
    }
}
