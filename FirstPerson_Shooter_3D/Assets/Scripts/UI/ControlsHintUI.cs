using System.Collections;
using TMPro;
using UnityEngine;

// Muestra mensajes de ayuda con fundido y parpadeo
[RequireComponent(typeof(CanvasGroup))]
public class ControlsHintUI : MonoBehaviour
{
    public static ControlsHintUI Instance { get; private set; }

    [SerializeField] private TMP_Text hintText;
    [SerializeField] private float fadeTime = 0.25f;

    [Header("Parpadeo")]
    [SerializeField] private float blinkSpeed = 4f;
    [SerializeField, Range(0f, 1f)] private float minBlinkAlpha = 0.25f;

    private CanvasGroup group;
    private Coroutine routine;
    private bool blinking;

    private void Awake()
    {
        Instance = this;
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
    }

    private void Update()
    {
        if (!blinking) return;
        float wave = (Mathf.Sin(Time.unscaledTime * blinkSpeed) + 1f) * 0.5f;
        hintText.alpha = Mathf.Lerp(minBlinkAlpha, 1f, wave);
    }

    // Resalta una tecla dentro del texto: [E]
    public static string Key(string key)
    {
        return "<b><color=#FFD54F>[" + key + "]</color></b>";
    }

    // duration <= 0: el mensaje se queda hasta llamar a Hide()
    public void Show(string message, float duration = 4f, bool blink = true)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ShowRoutine(message, duration, blink));
    }

    public void Hide()
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(HideRoutine());
    }

    private IEnumerator ShowRoutine(string message, float duration, bool blink)
    {
        hintText.text = message;
        hintText.alpha = 1f;
        blinking = blink;
        yield return Fade(1f);

        if (duration > 0f)
        {
            yield return new WaitForSecondsRealtime(duration);
            yield return Fade(0f);
            blinking = false;
        }
    }

    private IEnumerator HideRoutine()
    {
        yield return Fade(0f);
        blinking = false;
    }

    private IEnumerator Fade(float target)
    {
        float start = group.alpha;
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, target, t / fadeTime);
            yield return null;
        }
        group.alpha = target;
    }
}