using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ponlo UNA vez en un Canvas: añade UISound a todos los botones que contenga (incluso los desactivados).
/// </summary>
public class UISoundAutoHook : MonoBehaviour
{
    public string hoverId = "ui_hover";
    public string clickId = "ui_click";

    private void Start()
    {
        foreach (Button b in GetComponentsInChildren<Button>(true))
        {
            if (b.GetComponent<UISound>() != null) continue;

            var s = b.gameObject.AddComponent<UISound>();
            s.hoverId = hoverId;
            s.clickId = clickId;
        }
    }
}