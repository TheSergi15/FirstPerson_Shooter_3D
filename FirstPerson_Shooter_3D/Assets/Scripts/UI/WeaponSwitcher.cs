using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Inventario de armas con cambio por teclas 1-9 y rueda del ratón (Input System nuevo).
/// "unlocked" = el arma está en el inventario del jugador.
/// Las armas con unlocked = false se quedan en el mundo y no se tocan.
/// El arma base (la pistola) siempre está en el inventario y no se puede soltar.
/// </summary>
public class WeaponSwitcher : MonoBehaviour
{
    [Serializable]
    public class WeaponEntry
    {
        public string displayName = "Arma";
        public GameObject weapon;      // El GameObject del arma (con Gun y PickUpController)
        public Sprite icon;            // Imagen para el inventario en pantalla
        public Sprite ammoIcon;        // Icono de bala para el HUD de munición (vacío = bala genérica)
        public Sprite magazineIcon;    // Icono de cargador para el HUD de munición (vacío = cargador genérico)
        public bool unlocked = true;   // true = ya la lleva el jugador; false = está en el suelo
    }

    [SerializeField] private WeaponEntry[] weapons;
    [Tooltip("Índice (empezando en 0) del arma base: la pistola. Siempre está en el inventario y no se puede soltar.")]
    [SerializeField] private int baseWeaponIndex = 0;
    [SerializeField] private float scrollCooldown = 0.15f;
    [SerializeField] private bool wrapAround = true;

    /// <summary>Se lanza al cambiar de arma: (índice, entrada).</summary>
    public event Action<int, WeaponEntry> OnWeaponChanged;
    /// <summary>Se lanza cuando una arma entra en el inventario (al recogerla).</summary>
    public event Action<int> OnWeaponUnlocked;
    /// <summary>Se lanza cuando cambia qué ranuras están disponibles (recoger, soltar).</summary>
    public event Action OnSlotsChanged;

    public IReadOnlyList<WeaponEntry> Weapons => weapons;
    public int CurrentIndex { get; private set; } = -1;
    public WeaponEntry Current => CurrentIndex >= 0 ? weapons[CurrentIndex] : null;
    public bool HasWeapon => CurrentIndex >= 0;
    public int BaseWeaponIndex => baseWeaponIndex;

    private float nextScrollTime;

    public bool IsBase(int index) => index == baseWeaponIndex;

    // Awake (no Start) para que esté listo antes de que los PickUpController hagan su Start
    private void Awake()
    {
        if (weapons == null || weapons.Length == 0) return;

        baseWeaponIndex = Mathf.Clamp(baseWeaponIndex, 0, weapons.Length - 1);
        weapons[baseWeaponIndex].unlocked = true;   // el arma base siempre está en el inventario

        // Solo se guardan las armas que ya están en el inventario; las del suelo no se tocan
        foreach (var w in weapons)
            if (w.unlocked && w.weapon != null) w.weapon.SetActive(false);

        // Se empieza con el arma base en la mano
        Select(baseWeaponIndex);
        if (CurrentIndex < 0)
        {
            int first = FindNext(-1, 1);
            if (first >= 0) Select(first);
        }

        OnSlotsChanged?.Invoke();
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

    /// <summary>Índice de un arma en la lista, o -1 si no está.</summary>
    public int IndexOf(GameObject weapon)
    {
        for (int i = 0; i < weapons.Length; i++)
            if (weapons[i].weapon == weapon) return i;
        return -1;
    }

    /// <summary>Equipa el arma de la ranura indicada (si está en el inventario). Guarda la anterior.</summary>
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

    /// <summary>Mete el arma en el inventario (al recogerla) y opcionalmente la equipa.</summary>
    public void Unlock(int index, bool equip = true)
    {
        if (index < 0 || index >= weapons.Length) return;

        bool wasUnlocked = weapons[index].unlocked;
        weapons[index].unlocked = true;

        if (!wasUnlocked) OnWeaponUnlocked?.Invoke(index);
        OnSlotsChanged?.Invoke();
        if (equip) Select(index);
    }

    /// <summary>
    /// Saca el arma del inventario (al soltarla). NO la desactiva: se queda en el mundo.
    /// Si era la equipada, se vuelve al arma base. El arma base nunca se puede sacar.
    /// </summary>
    public void Lock(int index)
    {
        if (index < 0 || index >= weapons.Length) return;
        if (index == baseWeaponIndex) return;

        weapons[index].unlocked = false;

        if (index == CurrentIndex)
        {
            CurrentIndex = -1;   // el arma soltada no se desactiva
            Select(baseWeaponIndex);

            if (CurrentIndex < 0)
            {
                int next = FindNext(index, 1);
                if (next >= 0) Select(next);
            }
        }

        OnSlotsChanged?.Invoke();
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
