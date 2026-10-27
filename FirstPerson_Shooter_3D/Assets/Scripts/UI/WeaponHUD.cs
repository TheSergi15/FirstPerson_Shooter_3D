using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Muestra en el HUD las ranuras de armas, resalta la seleccionada
/// y enseña el nombre del arma unos segundos al cambiar.
/// </summary>
public class WeaponHUD : MonoBehaviour
{
    [Serializable]
    public class SlotUI
    {
        public Image background;   // Fondo de la ranura (se resalta)
        public Image icon;         // Icono del arma (opcional)
        public TMP_Text keyLabel;  // Número de tecla (opcional)
    }

    [Header("Referencias")]
    [SerializeField] private WeaponSwitcher switcher;
    [SerializeField] private SlotUI[] slots;
    [SerializeField] private TMP_Text weaponNameText;
    [SerializeField] private CanvasGroup nameGroup;

    [Header("Estilo")]
    [SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color lockedColor = new Color(1f, 1f, 1f, 0.1f);
    [SerializeField] private float selectedScale = 1.2f;

    [Header("Nombre del arma")]
    [SerializeField] private float nameVisibleTime = 1.5f;
    [SerializeField] private float nameFadeTime = 0.4f;

    private Coroutine nameRoutine;

    private void OnEnable()
    {
        if (switcher == null) return;
        switcher.OnWeaponChanged += HandleWeaponChanged;
        switcher.OnSlotsChanged += Refresh;
    }

    private void OnDisable()
    {
        if (switcher == null) return;
        switcher.OnWeaponChanged -= HandleWeaponChanged;
        switcher.OnSlotsChanged -= Refresh;
    }

    private void Start()
    {
        if (nameGroup != null) nameGroup.alpha = 0f;
        Refresh();
    }

    private void HandleWeaponChanged(int index, WeaponSwitcher.WeaponEntry entry)
    {
        Refresh();

        if (weaponNameText == null) return;
        weaponNameText.text = entry.displayName;

        if (nameGroup != null)
        {
            if (nameRoutine != null) StopCoroutine(nameRoutine);
            nameRoutine = StartCoroutine(ShowName());
        }
    }

    private void Refresh()
    {
        var weapons = switcher.Weapons;

        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot.background == null) continue;

            // Ocultar ranuras que no tienen arma asignada
            bool exists = i < weapons.Count;
            slot.background.gameObject.SetActive(exists);
            if (!exists) continue;

            var entry = weapons[i];
            bool selected = i == switcher.CurrentIndex;

            slot.background.color = selected ? selectedColor : (entry.unlocked ? normalColor : lockedColor);
            slot.background.transform.localScale = Vector3.one * (selected ? selectedScale : 1f);

            if (slot.icon != null)
            {
                slot.icon.sprite = entry.icon;
                slot.icon.enabled = entry.icon != null;
                slot.icon.color = entry.unlocked ? Color.white : lockedColor;
            }

            if (slot.keyLabel != null)
                slot.keyLabel.text = (i + 1).ToString();
        }
    }

    private IEnumerator ShowName()
    {
        nameGroup.alpha = 1f;
        yield return new WaitForSeconds(nameVisibleTime);

        float t = 0f;
        while (t < nameFadeTime)
        {
            t += Time.deltaTime;
            nameGroup.alpha = 1f - t / nameFadeTime;
            yield return null;
        }
        nameGroup.alpha = 0f;
    }
}
