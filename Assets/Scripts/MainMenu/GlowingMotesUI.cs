using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Yade.MainMenu
{
    [RequireComponent(typeof(RectTransform))]
    public class GlowingMotesUI : MonoBehaviour
    {
        [Header("Counts")]
        [SerializeField] private int fallingMoteCount = 70;
        [SerializeField] private int pulseMoteCount = 12;

        [Header("Falling Motes")]
        [SerializeField] private float minMoteSize = 3f;
        [SerializeField] private float maxMoteSize = 11f;
        [SerializeField] private float minFallDuration = 7f;
        [SerializeField] private float maxFallDuration = 18f;
        [SerializeField] private float horizontalDrift = 35f;
        [SerializeField] private float minOpacity = 0.45f;
        [SerializeField] private float maxOpacity = 0.95f;

        [Header("Pulse Glows")]
        [SerializeField] private float minPulseSize = 20f;
        [SerializeField] private float maxPulseSize = 60f;
        [SerializeField] private float minPulseDuration = 6f;
        [SerializeField] private float maxPulseDuration = 14f;

        [Header("Palette")]
        [SerializeField] private Color[] colors =
        {
            new Color(1f, 1f, 0.988f, 0.9f),
            new Color(0.784f, 0.753f, 0.878f, 0.8f),
            new Color(0.545f, 0.741f, 0.91f, 0.6f),
            new Color(0.659f, 0.659f, 0.816f, 0.7f),
            new Color(1f, 0.973f, 0.906f, 0.85f),
            new Color(0.659f, 0.831f, 0.541f, 0.5f)
        };

        private readonly List<Mote> motes = new List<Mote>();
        private RectTransform rectTransform;
        private Sprite moteSprite;

        private sealed class Mote
        {
            public RectTransform Rect;
            public Image Image;
            public bool IsPulse;
            public float Elapsed;
            public float Duration;
            public float Size;
            public float StartX;
            public float DriftA;
            public float DriftB;
            public float DriftC;
            public float Opacity;
            public Color BaseColor;
        }

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            moteSprite = CreateRadialSprite();
        }

        private void OnEnable()
        {
            Rebuild();
        }

        private void OnDisable()
        {
            ClearMotes();
        }

        private void Update()
        {
            Vector2 size = rectTransform.rect.size;
            if (size.x <= 0f || size.y <= 0f)
            {
                return;
            }

            for (int i = 0; i < motes.Count; i++)
            {
                Mote mote = motes[i];
                mote.Elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(mote.Elapsed / mote.Duration);

                if (mote.IsPulse)
                {
                    UpdatePulseMote(mote, t);
                }
                else
                {
                    UpdateFallingMote(mote, t, size);
                }

                if (mote.Elapsed >= mote.Duration)
                {
                    ResetMote(mote, size, !mote.IsPulse);
                }
            }
        }

        public void Rebuild()
        {
            ClearMotes();

            Vector2 size = rectTransform.rect.size;
            if (size.x <= 0f || size.y <= 0f)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    Rect pixelRect = canvas.pixelRect;
                    size = new Vector2(pixelRect.width, pixelRect.height);
                }
            }

            for (int i = 0; i < fallingMoteCount; i++)
            {
                Mote mote = CreateMote("Mote", false);
                ResetMote(mote, size, true);
                mote.Elapsed = Random.Range(0f, mote.Duration);
            }

            for (int i = 0; i < pulseMoteCount; i++)
            {
                Mote mote = CreateMote("PulseMote", true);
                ResetMote(mote, size, false);
                mote.Elapsed = Random.Range(0f, mote.Duration);
            }
        }

        private Mote CreateMote(string objectName, bool isPulse)
        {
            GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.transform.SetParent(transform, false);

            Image image = child.GetComponent<Image>();
            image.sprite = moteSprite;
            image.raycastTarget = false;

            Mote mote = new Mote
            {
                Rect = child.GetComponent<RectTransform>(),
                Image = image,
                IsPulse = isPulse
            };

            motes.Add(mote);
            return mote;
        }

        private void ResetMote(Mote mote, Vector2 layerSize, bool falling)
        {
            if (falling)
            {
                mote.Size = Random.Range(minMoteSize, maxMoteSize);
                mote.Duration = Random.Range(minFallDuration, maxFallDuration);
                mote.StartX = Random.Range(-layerSize.x * 0.55f, layerSize.x * 0.55f);
                mote.DriftA = Random.Range(-horizontalDrift, horizontalDrift);
                mote.DriftB = Random.Range(-horizontalDrift, horizontalDrift);
                mote.DriftC = Random.Range(-horizontalDrift, horizontalDrift);
                mote.Opacity = Random.Range(minOpacity, maxOpacity);
                mote.BaseColor = PickColor();
                mote.Elapsed = 0f;
                mote.Rect.sizeDelta = Vector2.one * mote.Size;
            }
            else
            {
                mote.Size = Random.Range(minPulseSize, maxPulseSize);
                mote.Duration = Random.Range(minPulseDuration, maxPulseDuration);
                mote.StartX = Random.Range(-layerSize.x * 0.5f, layerSize.x * 0.5f);
                mote.DriftA = Random.Range(-layerSize.y * 0.35f, layerSize.y * 0.35f);
                mote.Opacity = Random.Range(0.12f, 0.3f);
                mote.BaseColor = new Color(0.784f, 0.753f, 0.878f, 1f);
                mote.Elapsed = 0f;
                mote.Rect.sizeDelta = Vector2.one * mote.Size;
                mote.Rect.anchoredPosition = new Vector2(mote.StartX, mote.DriftA);
            }
        }

        private void UpdateFallingMote(Mote mote, float t, Vector2 layerSize)
        {
            float y = Mathf.Lerp(layerSize.y * 0.55f + mote.Size, -layerSize.y * 0.55f - mote.Size, t);
            float x = mote.StartX
                + Mathf.Sin(t * Mathf.PI * 2f) * mote.DriftA
                + Mathf.Sin(t * Mathf.PI * 4f + 1.7f) * mote.DriftB
                + Mathf.Sin(t * Mathf.PI * 6f + 0.8f) * mote.DriftC * 0.35f;

            float alpha = FadeInOut(t, 0.05f, 0.95f) * mote.Opacity;
            float scale = Mathf.Lerp(1f, 0.6f, Mathf.SmoothStep(0.75f, 1f, t));

            mote.Rect.anchoredPosition = new Vector2(x, y);
            mote.Rect.localScale = Vector3.one * scale;
            mote.Image.color = WithAlpha(mote.BaseColor, alpha);
        }

        private void UpdatePulseMote(Mote mote, float t)
        {
            float wave = Mathf.Sin(t * Mathf.PI);
            float scale = Mathf.Lerp(0.7f, 1.15f, wave);
            float alpha = wave * mote.Opacity;

            mote.Rect.localScale = Vector3.one * scale;
            mote.Image.color = WithAlpha(mote.BaseColor, alpha);
        }

        private Color PickColor()
        {
            if (colors == null || colors.Length == 0)
            {
                return Color.white;
            }

            return colors[Random.Range(0, colors.Length)];
        }

        private void ClearMotes()
        {
            for (int i = motes.Count - 1; i >= 0; i--)
            {
                if (motes[i].Rect != null)
                {
                    Destroy(motes[i].Rect.gameObject);
                }
            }

            motes.Clear();
        }

        private static float FadeInOut(float t, float fadeInEnd, float fadeOutStart)
        {
            if (t < fadeInEnd)
            {
                return Mathf.Clamp01(t / fadeInEnd);
            }

            if (t > fadeOutStart)
            {
                return Mathf.Clamp01((1f - t) / (1f - fadeOutStart));
            }

            return 1f;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a *= Mathf.Clamp01(alpha);
            return color;
        }

        private static Sprite CreateRadialSprite()
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Generated Main Menu Mote",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha = Mathf.Clamp01(1f - distance);
                    alpha = Mathf.Pow(alpha, 2.2f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
