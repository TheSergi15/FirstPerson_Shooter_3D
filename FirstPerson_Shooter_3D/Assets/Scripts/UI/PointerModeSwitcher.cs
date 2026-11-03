
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class PointerModeSwitcher : MonoBehaviour
{
    [Header("Cursores")]
    [SerializeField] private Texture unarmedCursor;
    [SerializeField] private Texture armedCursor;

    private RawImage pointerImage;

    private void Awake()
    {
        pointerImage = GetComponent<RawImage>();

        SetArmed(false);
    }

    public void SetArmed(bool armed)
    {
        pointerImage.texture = armed
            ? armedCursor
            : unarmedCursor;
    }
}