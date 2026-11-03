using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Inventario de armas con cambio por teclas 1-9 y rueda del ratón.
/// El arma base (pistola) se consigue en el tutorial: desde que se recoge, siempre está
/// en el inventario (también en los demás niveles) y no se puede soltar.
/// También controla el cambio entre cursor normal y cursor de arma.
/// </summary>
public class WeaponSwitcher : MonoBehaviour
{
    [Serializable]
    public class WeaponEntry
    {
        public string displayName = "Arma";
        public GameObject weapon;
        public Sprite icon;
        public Sprite ammoIcon;
        public Sprite magazineIcon;
        public bool unlocked = true;
    }

    [Header("Inventario")]
    [SerializeField] private WeaponEntry[] weapons;

    [Tooltip("Índice (empezando en 0) del arma base: la pistola.")]
    [SerializeField] private int baseWeaponIndex = 0;

    [SerializeField] private float scrollCooldown = 0.15f;
    [SerializeField] private bool wrapAround = true;

    [Header("Arma base y progreso")]
    [Tooltip("Márcalo SOLO en la escena del tutorial: el arma base empieza en el suelo, sin que el jugador la tenga.")]
    [SerializeField] private bool startWithoutBaseWeapon = false;

    [Tooltip("Solo pruebas: simula que ya has pasado el tutorial (útil al abrir un nivel directamente).")]
    [SerializeField] private bool unlockedForTesting = false;

    [Header("Cambio de cursor")]
    [SerializeField] private PointerModeSwitcher pointerModeSwitcher;

    public event Action<int, WeaponEntry> OnWeaponChanged;
    public event Action<int> OnWeaponUnlocked;
    public event Action OnSlotsChanged;

    public IReadOnlyList<WeaponEntry> Weapons => weapons;

    public int CurrentIndex { get; private set; } = -1;

    public WeaponEntry Current =>
        CurrentIndex >= 0 ? weapons[CurrentIndex] : null;

    public bool HasWeapon => CurrentIndex >= 0;

    public int BaseWeaponIndex => baseWeaponIndex;

    private float nextScrollTime;

    /// <summary>
    /// true solo si es el arma base Y el jugador ya la tiene.
    /// Mientras la pistola está en el suelo del tutorial se puede recoger como cualquier arma;
    /// una vez recogida pasa a ser la base (no se puede soltar).
    /// </summary>
    public bool IsBase(int index)
    {
        return weapons != null
            && index == baseWeaponIndex
            && index >= 0 && index < weapons.Length
            && weapons[index].unlocked;
    }

    private void Awake()
    {
        // El slot estático sobrevive al cambio de escena: se reinicia al empezar cada nivel
        PickUpController.slotFull = false;

        if (weapons == null || weapons.Length == 0) return;

        baseWeaponIndex = Mathf.Clamp(
            baseWeaponIndex, 0, weapons.Length - 1
        );

        // El arma base solo se posee si ya se consiguió en el tutorial (o en modo pruebas)
        bool baseOwned = !startWithoutBaseWeapon
                         && (PlayerProgress.BaseWeaponUnlocked || unlockedForTesting);
        weapons[baseWeaponIndex].unlocked = baseOwned;

        // Desactiva las armas que ya están en el inventario.
        foreach (var w in weapons)
        {
            if (w.unlocked && w.weapon != null)
                w.weapon.SetActive(false);
        }

        // Se empieza con el arma base en la mano (si se tiene).
        Select(baseWeaponIndex);

        if (CurrentIndex < 0)
        {
            int first = FindNext(-1, 1);

            if (first >= 0)
                Select(first);
        }
        // Si no hay ninguna arma, CurrentIndex queda en -1 (manos vacías)

        OnSlotsChanged?.Invoke();
    }

    private void Start()
    {
        // Reaplicamos el estado del cursor después de todos los Awake.
        // Así evitamos conflictos con el orden de inicialización de Unity.
        UpdatePointerMode();

        if (pointerModeSwitcher == null)
        {
            Debug.LogWarning(
                "WeaponSwitcher: falta asignar PointerModeSwitcher en el Inspector.",
                this
            );
        }
    }

    private void Update()
    {
        HandleNumberKeys();
        HandleScroll();
    }

    /// <summary>
    /// Cambia entre cursor normal y cursor de arma
    /// según exista un arma equipada.
    /// </summary>
    private void UpdatePointerMode()
    {
        if (pointerModeSwitcher == null)
            return;

        bool armed =
            weapons != null &&
            CurrentIndex >= 0 &&
            CurrentIndex < weapons.Length &&
            weapons[CurrentIndex] != null &&
            weapons[CurrentIndex].unlocked &&
            weapons[CurrentIndex].weapon != null;

        pointerModeSwitcher.SetArmed(armed);
    }

    private void HandleNumberKeys()
    {
        var kb = Keyboard.current;

        if (kb == null || weapons == null)
            return;

        int max = Mathf.Min(weapons.Length, 9);

        for (int i = 0; i < max; i++)
        {
            Key key = (Key)((int)Key.Digit1 + i);

            if (kb[key].wasPressedThisFrame)
                Select(i);
        }
    }

    private void HandleScroll()
    {
        var mouse = Mouse.current;

        if (mouse == null || weapons == null ||
            Time.time < nextScrollTime)
            return;

        float scroll = mouse.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) < 0.1f)
            return;

        int next = FindNext(CurrentIndex, scroll > 0 ? -1 : 1);

        if (next >= 0)
            Select(next);

        nextScrollTime = Time.time + scrollCooldown;
    }

    /// <summary>
    /// Busca el índice de un arma en la lista.
    /// </summary>
    public int IndexOf(GameObject weapon)
    {
        if (weapons == null) return -1;

        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i].weapon == weapon)
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Equipa el arma de la ranura indicada.
    /// </summary>
    public void Select(int index)
    {
        if (weapons == null ||
            index < 0 || index >= weapons.Length)
            return;

        if (index == CurrentIndex)
        {
            UpdatePointerMode();
            return;
        }

        var entry = weapons[index];

        if (entry == null || !entry.unlocked || entry.weapon == null)
            return;

        // Desactiva el arma anterior.
        if (CurrentIndex >= 0 &&
            CurrentIndex < weapons.Length &&
            weapons[CurrentIndex].weapon != null)
        {
            weapons[CurrentIndex].weapon.SetActive(false);
        }

        // Activa la nueva arma.
        entry.weapon.SetActive(true);

        CurrentIndex = index;

        // Al equipar un arma, activa el cursor de arma.
        UpdatePointerMode();

        OnWeaponChanged?.Invoke(index, entry);
    }

    /// <summary>
    /// Desbloquea un arma al recogerla.
    /// </summary>
    public void Unlock(int index, bool equip = true)
    {
        if (weapons == null ||
            index < 0 || index >= weapons.Length)
            return;

        bool wasUnlocked = weapons[index].unlocked;

        weapons[index].unlocked = true;

        // Al conseguir el arma base por primera vez, se recuerda para los demás niveles
        if (index == baseWeaponIndex)
            PlayerProgress.BaseWeaponUnlocked = true;

        if (!wasUnlocked)
            OnWeaponUnlocked?.Invoke(index);

        OnSlotsChanged?.Invoke();

        if (equip)
            Select(index);
    }

    /// <summary>
    /// Saca un arma del inventario al soltarla.
    /// Si era la equipada, vuelve a la pistola base.
    /// </summary>
    public void Lock(int index)
    {
        if (weapons == null ||
            index < 0 || index >= weapons.Length)
            return;

        // La pistola base (una vez conseguida) nunca se puede soltar.
        if (IsBase(index))
            return;

        weapons[index].unlocked = false;

        if (index == CurrentIndex)
        {
            // El arma soltada no se desactiva: permanece en el mundo.
            CurrentIndex = -1;

            // Primero intentamos volver a la pistola.
            Select(baseWeaponIndex);

            // Si no se pudo equipar, buscamos otra arma disponible.
            if (CurrentIndex < 0)
            {
                int next = FindNext(index, 1);

                if (next >= 0)
                    Select(next);
            }

            // Actualiza el cursor según el arma resultante.
            // Si no hay ninguna equipada, vuelve al cursor normal.
            UpdatePointerMode();
        }

        OnSlotsChanged?.Invoke();
    }

    private int FindNext(int from, int dir)
    {
        if (weapons == null || weapons.Length == 0)
            return -1;

        int count = weapons.Length;
        int idx = from;

        for (int i = 0; i < count; i++)
        {
            idx += dir;

            if (wrapAround)
                idx = (idx + count) % count;
            else if (idx < 0 || idx >= count)
                return -1;

            if (weapons[idx] != null &&
                weapons[idx].unlocked &&
                weapons[idx].weapon != null &&
                idx != CurrentIndex)
            {
                return idx;
            }
        }

        return -1;
    }
}