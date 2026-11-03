using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Pasa al siguiente nivel con fundido a negro.
/// Añade la escena a File > Build Profiles (Build Settings) o no cargará.
/// </summary>
public class NextLevelLoader : MonoBehaviour
{
    [Tooltip("Nombre de la escena. Si lo dejas vacío carga la siguiente por índice de build.")]
    [SerializeField] private string nextSceneName = "Level1";
    [Tooltip("Panel negro a pantalla completa con CanvasGroup (opcional)")]
    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField] private float fadeDuration = 1f;

    private bool loading;

    public void LoadNext()
    {
        if (loading) return;
        loading = true;
        StartCoroutine(LoadRoutine());
    }

    private IEnumerator LoadRoutine()
    {
        if (fadeGroup != null)
        {
            fadeGroup.gameObject.SetActive(true);
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                fadeGroup.alpha = Mathf.Clamp01(t / fadeDuration);
                yield return null;
            }
            fadeGroup.alpha = 1f;
        }

        Time.timeScale = 1f;

        AsyncOperation op = string.IsNullOrEmpty(nextSceneName)
            ? SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex + 1)
            : SceneManager.LoadSceneAsync(nextSceneName);

        while (op != null && !op.isDone) yield return null;
    }
}
