using System.Collections;
using TMPro;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Cartel "TUTORIAL COMPLETADO" con marco. Aparece con fundido + zoom suave
/// y pasa al siguiente nivel con ENTER/ESPACIO o automáticamente tras unos segundos.
/// Deja el objeto DESACTIVADO en la Hierarchy: el TutorialManager lo activa.
/// </summary>

public class TutorialCompletePanel : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform frame;
    [SerializeField] private TMP_Text continueText;
    [SerializeField] private NextLevelLoader loader;
    [SerializeField] private float fadeInTime = 0.7f;
    [SerializeField] private float minWaitBeforeInput = 1.5f;
    [SerializeField] private float autoContinueDelay = 8f;

    private bool ready;
    private float readyAt;

    public void Show()
    {
        gameObject.SetActive(true);
        StartCoroutine(Intro());
    }

    private IEnumerator Intro()
    {
        group.alpha = 0f;
        float t = 0f;
        while (t < fadeInTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / fadeInTime);
            group.alpha = k;
            frame.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, k);
            yield return null;
        }
        group.alpha = 1f;
        frame.localScale = Vector3.one;
        ready = true;
        readyAt = Time.unscaledTime;
        
    }

    private void Update()
    {
        if (!ready) return;

        float elapsed = Time.unscaledTime - readyAt;
        float remaining = autoContinueDelay - elapsed;

        if (continueText != null)
            continueText.text = $"Pulsa ENTER para continuar  ({Mathf.CeilToInt(Mathf.Max(0f, remaining))})";

        Keyboard kb = Keyboard.current;
        bool pressed = kb != null &&
            (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame);

        if ((pressed && elapsed >= minWaitBeforeInput) || remaining <= 0f)
        {
            ready = false;
            loader.LoadNext();
        }
    }
}
