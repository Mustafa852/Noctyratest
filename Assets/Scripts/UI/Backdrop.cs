using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Noctyra
{
    /// <summary>Gece/gündüz geçişli, yıldızlı ve aylı arka plan. Tamamen prosedürel, görsel dosyası gerektirmez.</summary>
    public class Backdrop : MonoBehaviour
    {
        static readonly Color NightBg = new Color(0.035f, 0.030f, 0.090f);
        static readonly Color DayBg = new Color(0.20f, 0.085f, 0.14f);
        static readonly Color NightHorizon = new Color(0.26f, 0.14f, 0.46f, 0.75f);
        static readonly Color DayHorizon = new Color(0.88f, 0.28f, 0.20f, 0.70f);
        static readonly Color MoonColor = new Color(0.93f, 0.93f, 1f);
        static readonly Color SunColor = new Color(1f, 0.62f, 0.25f);

        Image bg, horizon, moon, sun, moonGlow, sunGlow;
        readonly List<Image> stars = new List<Image>();
        readonly List<float> phases = new List<float>();
        float starLevel = 1f;
        float blend; // 0 = gece, 1 = gündüz

        public void Build(Transform parent)
        {
            var root = UIKit.NewRect("Backdrop", parent);
            UIKit.Stretch(root);
            bg = root.gameObject.AddComponent<Image>();
            bg.raycastTarget = false;

            for (int i = 0; i < 70; i++)
            {
                var r = UIKit.NewRect("Star", root);
                float x = Random.value;
                float y = Mathf.Lerp(0.35f, 1f, Random.value);
                r.anchorMin = r.anchorMax = new Vector2(x, y);
                r.sizeDelta = Vector2.one * Random.Range(2f, 5f);
                var img = r.gameObject.AddComponent<Image>();
                img.raycastTarget = false;
                stars.Add(img);
                phases.Add(Random.value * 6.28f);
            }

            var hr = UIKit.NewRect("Horizon", root);
            UIKit.Stretch(hr);
            horizon = hr.gameObject.AddComponent<Image>();
            horizon.sprite = UIKit.VerticalGradient();
            horizon.raycastTarget = false;

            var glowSprite = UIKit.Glow(256);
            moonGlow = MakeOrb(root, "MoonGlow", glowSprite, 620f);
            sunGlow = MakeOrb(root, "SunGlow", glowSprite, 720f);
            moon = MakeOrb(root, "Moon", UIKit.Disc(256, true), 200f);
            sun = MakeOrb(root, "Sun", UIKit.Disc(256, false), 200f);

            Apply(0f);
        }

        Image MakeOrb(Transform parent, string name, Sprite sprite, float size)
        {
            var r = UIKit.NewRect(name, parent);
            r.anchorMin = r.anchorMax = new Vector2(0.86f, 0.82f);
            r.sizeDelta = new Vector2(size, size);
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        void Apply(float k)
        {
            blend = k;
            bg.color = Color.Lerp(NightBg, DayBg, k);
            horizon.color = Color.Lerp(NightHorizon, DayHorizon, k);
            starLevel = 1f - k;

            SetAlpha(moon, MoonColor, 1f - k);
            SetAlpha(moonGlow, MoonColor, (1f - k) * 0.35f);
            SetAlpha(sun, SunColor, k);
            SetAlpha(sunGlow, SunColor, k * 0.45f);
        }

        static void SetAlpha(Image img, Color c, float a)
        {
            c.a = a;
            img.color = c;
        }

        public void SetNight(bool night, bool instant)
        {
            float target = night ? 0f : 1f;
            StopAllCoroutines();
            if (instant) Apply(target);
            else StartCoroutine(Fade(target));
        }

        IEnumerator Fade(float to)
        {
            float from = blend;
            const float duration = 1.6f;
            for (float e = 0f; e < duration; e += Time.unscaledDeltaTime)
            {
                Apply(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, e / duration)));
                yield return null;
            }
            Apply(to);
        }

        void Update()
        {
            if (stars.Count == 0 || starLevel <= 0.01f || Time.frameCount % 3 != 0) return;
            for (int i = 0; i < stars.Count; i++)
            {
                float tw = 0.5f + 0.5f * Mathf.Sin(Time.time * 1.3f + phases[i]);
                stars[i].color = new Color(1f, 1f, 1f, starLevel * (0.30f + 0.70f * tw));
            }
        }
    }
}
