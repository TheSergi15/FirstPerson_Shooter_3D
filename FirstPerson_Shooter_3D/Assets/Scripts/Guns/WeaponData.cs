using UnityEngine;

public enum FireMode
{
    SemiAuto,   // un disparo por clic (pistola, escopeta)
    FullAuto,   // dispara mientras mantienes el botón (ametralladora)
    Burst       // una ráfaga de N balas por clic
}

/// <summary>
/// Datos de un arma. Se crea desde el menú: Assets > Create > Juego > Arma (WeaponData).
/// Una vez creada, se arrastra al campo "data" del script Gun.
/// </summary>
[CreateAssetMenu(fileName = "NuevaArma", menuName = "Juego/Arma (WeaponData)")]
public class WeaponData : ScriptableObject
{
    [Header("Identidad (HUD / inventario)")]
    public string displayName = "Arma";
    public Sprite icon;

    [Header("Tipo de disparo")]
    public FireMode fireMode = FireMode.SemiAuto;

    [Tooltip("Disparos por segundo. En modo Burst: ráfagas por segundo.")]
    public float fireRate = 4f;

    [Tooltip("Solo Burst: balas por ráfaga.")]
    public int burstCount = 3;

    [Tooltip("Solo Burst: segundos entre balas de una misma ráfaga.")]
    public float burstInterval = 0.07f;

    [Tooltip("Proyectiles por disparo. 1 = pistola/fusil, 8 = escopeta.")]
    public int pellets = 1;

    [Tooltip("Dispersión: ángulo máximo del cono, en grados (0 = precisión total).")]
    public float spreadAngle = 0f;

    [Header("Daño")]
    [Tooltip("Daño de CADA proyectil.")]
    public float damage = 25f;
    public float range = 100f;

    [Header("Munición")]
    public int magazineSize = 12;
    public int startReserveAmmo = 60;
    public float reloadTime = 1.2f;

    [Header("Sonidos (ids de la SoundLibrary)")]
    public string shootSfx = "shoot_pistol";
    public string reloadSfx = "reload";
    public string emptySfx = "empty_click";

    private void OnValidate()
    {
        fireRate = Mathf.Max(0.05f, fireRate);
        burstCount = Mathf.Max(1, burstCount);
        pellets = Mathf.Max(1, pellets);
        magazineSize = Mathf.Max(1, magazineSize);
        spreadAngle = Mathf.Max(0f, spreadAngle);
    }
}