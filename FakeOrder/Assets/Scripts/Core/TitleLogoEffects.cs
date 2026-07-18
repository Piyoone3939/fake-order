using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 完成済みロゴ画像の上にだけ演出を重ねる。ロゴ本体の再描画は行わない。
/// </summary>
public class TitleLogoEffects : MonoBehaviour
{
    private RectTransform logoRoot;
    private RectTransform logoImageRect;
    private CanvasGroup canvasGroup;
    private RectTransform scanLine;
    private RectTransform shine;
    private Image glitchCopy;
    private Vector2 baseLogoPosition;
    private float elapsed;
    private float nextGlitchAt = 2.4f;
    private float glitchRemaining;
    private bool isExiting;

    public void Configure(RectTransform root, Image logoImage)
    {
        logoRoot = root;
        logoImageRect = logoImage.rectTransform;
        canvasGroup = root.GetComponent<CanvasGroup>();
        baseLogoPosition = logoImageRect.anchoredPosition;

        glitchCopy = CreateOverlayLogo("GlitchCopy", logoImage.sprite, new Color(0.05f, 0.65f, 1f, 0.22f));
        glitchCopy.gameObject.SetActive(false);
        scanLine = CreateStrip("ScanLine", new Vector2(0f, 3f), new Color(0.05f, 0.65f, 1f, 0.28f));
        shine = CreateStrip("WhiteShine", new Vector2(72f, 0f), new Color(1f, 1f, 1f, 0.14f));
        shine.localRotation = Quaternion.Euler(0f, 0f, -8f);
        ResetPresentation();
    }

    public void ResetPresentation()
    {
        StopAllCoroutines();
        elapsed = 0f;
        nextGlitchAt = 2.4f;
        glitchRemaining = 0f;
        isExiting = false;
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (logoRoot != null) logoRoot.localScale = Vector3.one;
        if (logoImageRect != null) logoImageRect.anchoredPosition = baseLogoPosition;
        if (glitchCopy != null) glitchCopy.gameObject.SetActive(false);
    }

    public void PlayExit(Action onComplete)
    {
        if (!isExiting)
            StartCoroutine(ExitRoutine(onComplete));
    }

    private void Update()
    {
        if (logoRoot == null || isExiting) return;

        elapsed += Time.unscaledDeltaTime;
        canvasGroup.alpha = Mathf.Clamp01(elapsed / 0.55f);

        float scanProgress = Mathf.Repeat(elapsed / 2.2f, 1f);
        scanLine.anchoredPosition = new Vector2(0f, Mathf.Lerp(-260f, 260f, scanProgress));

        float shineProgress = Mathf.Repeat((elapsed - 0.8f) / 4f, 1f);
        shine.anchoredPosition = new Vector2(Mathf.Lerp(-880f, 880f, shineProgress), 0f);

        if (elapsed >= nextGlitchAt)
        {
            glitchRemaining = 0.09f;
            nextGlitchAt = elapsed + UnityEngine.Random.Range(2.4f, 4.2f);
        }

        if (glitchRemaining > 0f)
        {
            glitchRemaining -= Time.unscaledDeltaTime;
            float shift = UnityEngine.Random.value > 0.5f ? 8f : -8f;
            logoImageRect.anchoredPosition = baseLogoPosition + new Vector2(shift, 0f);
            glitchCopy.rectTransform.anchoredPosition = new Vector2(-shift * 1.5f, 0f);
            glitchCopy.gameObject.SetActive(true);
        }
        else
        {
            logoImageRect.anchoredPosition = baseLogoPosition;
            glitchCopy.gameObject.SetActive(false);
        }
    }

    private IEnumerator ExitRoutine(Action onComplete)
    {
        isExiting = true;
        float duration = 0.18f;
        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            logoRoot.localScale = Vector3.one * Mathf.Lerp(1f, 0.94f, Mathf.Clamp01(time / duration));
            yield return null;
        }
        onComplete?.Invoke();
    }

    private Image CreateOverlayLogo(string objectName, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(logoRoot, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        Stretch(rect);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = color;
        return image;
    }

    private RectTransform CreateStrip(string objectName, Vector2 size, Color color)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(logoRoot, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size.x <= 0f ? 1600f : size.x, size.y <= 0f ? 533f : size.y);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
