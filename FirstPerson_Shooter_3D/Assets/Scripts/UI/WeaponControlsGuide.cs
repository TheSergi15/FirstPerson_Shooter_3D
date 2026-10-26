using System.Collections;
using UnityEngine;

public class WeaponControlsGuide : MonoBehaviour
{
    [System.Serializable]
    public class Step
    {
        public string key;
        [TextArea(1, 2)] public string message; //usa {0} donde va la tecla
        public float duration = 3.5f;
    }

    [SerializeField]
    private Step[] steps =
    {
        new Step { key = "Click izquierdo", message = "Pulsa {0} para disparar"},
        new Step { key = "Click derecho", message = "Pulsa {0} para recargar"},
        new Step { key = "Q",              message = "Pulsa {0} para soltar o lanzar el arma"},
    };


    [SerializeField] private bool onlyFirstTime = true;

    private bool alreadyShown;
    private Coroutine routine;

    public void PlayControls()
    {
        if (onlyFirstTime && alreadyShown) return;
        alreadyShown = true;

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Play());
    }

    public void StopGuide()
    {
        if (routine == null) return;
        StopCoroutine(routine);
        routine = null;
        ControlsHintUI.Instance.Hide();
    }

    private IEnumerator Play()
    { 
       foreach (Step step in steps)
       {
            string text = string.Format(step.message, ControlsHintUI.Key(step.key));
            ControlsHintUI.Instance.Show(text, step.duration, true);
            yield return new WaitForSecondsRealtime(step.duration + 0.6f);

       }

        routine = null;
    }
    
}
