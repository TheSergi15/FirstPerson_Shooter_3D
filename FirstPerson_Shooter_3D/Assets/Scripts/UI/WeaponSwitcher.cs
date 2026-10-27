using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Cambia entre armas hijas de un mismo objeto (WeaponHolder).
/// Teclas 1-9 y rueda del ratón. Compatible con el Input System nuevo.
/// </summary>
public class WeaponSwitcher : MonoBehaviour
{
    [Serializable]
    public class WeaponEntry
    {
        public string displayName = "Arma";
        public GameObject weapon;      // El GameObject del arma (con tu script Gun)
        public Sprite icon;            // Icono para el HUD (opcional)
        public bool unlocked = true;   // Si false, no se puede seleccionar hasta desbloquearla
    }

    [SerializeField] private WeaponEntry[] weapons;
    [SerializeField] private float scrollCooldown = 0.15f;
    [SerializeField] private bool wrapAround = true;

    /// <summary>Se lanza al cambiar de arma: (índice, entrada).</summary>
    public event Action<int, WeaponEntry> OnWeaponChanged;
    /// <summary>Se lanza cuando se desbloquea una ranura (para refrescar el HUD).</summary>
    public event Action OnSlotsChanged;

    public IReadOnlyList<WeaponEntry> Weapons => weapons;
    public int CurrentIndex { get; private set; } = -1;
    public WeaponEntry Current => CurrentIndex >= 0 ? weapons[CurrentIndex] : null;

    private float nextScrollTime;

    private void Start()
    {
        // Desactivar todas las armas al empezar
        foreach (var w in weapons)
            if (w.weapon != null) w.weapon.SetActive(false);

        int first = FindNext(-1, 1);
        if (first >= 0) Select(first);
        else OnSlotsChanged?.Invoke();
    }

    private void Update()
    {
        // Si usas la granada, bloquea el cambio mientras se apunta:
        // if (GrenadeThrower.IsAiming) return;

        HandleNumberKeys();
        HandleScroll();
    }

    private void HandleNumberKeys()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        int max = Mathf.Min(weapons.Length, 9);
        for (int i = 0; i < max; i++)
        {
            Key key = (Key)((int)Key.Digit1 + i);
            if (kb[key].wasPressedThisFrame) Select(i);
        }
    }

    private void HandleScroll()
    {
        var mouse = Mouse.current;
        if (mouse == null || Time.time < nextScrollTime) return;

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < 0.1f) return;

        // Rueda hacia arriba = arma anterior, hacia abajo = siguiente
        int next = FindNext(CurrentIndex, scroll > 0 ? -1 : 1);
        if (next >= 0) Select(next);
        nextScrollTime = Time.time + scrollCooldown;
    }

    /// <summary>Selecciona el arma en la ranura indicada (si existe y está desbloqueada).</summary>
    public void Select(int index)
    {
        if (index < 0 || index >= weapons.Length) return;
        if (index == CurrentIndex) return;

        var entry = weapons[index];
        if (!entry.unlocked || entry.weapon == null) return;

        if (CurrentIndex >= 0 && weapons[CurrentIndex].weapon != null)
            weapons[CurrentIndex].weapon.SetActive(false);

        entry.weapon.SetActive(true);
        CurrentIndex = index;
        OnWeaponChanged?.Invoke(index, entry);
    }

    /// <summary>Desbloquea una ranura (p. ej. al recoger el arma) y opcionalmente la equipa.</summary>
    public void Unlock(int index, bool equip = true)
    {
        if (index < 0 || index >= weapons.Length) return;

        weapons[index].unlocked = true;
        OnSlotsChanged?.Invoke();
        if (equip) Select(index);
    }

    private int FindNext(int from, int dir)
    {
        int count = weapons.Length;
        if (count == 0) return -1;

        int idx = from;
        for (int i = 0; i < count; i++)
        {
            idx += dir;

            if (wrapAround) idx = (idx + count) % count;
            else if (idx < 0 || idx >= count) return -1;

            if (weapons[idx].unlocked && weapons[idx].weapon != null && idx != CurrentIndex)
                return idx;
        }
        return -1;
    }
}
