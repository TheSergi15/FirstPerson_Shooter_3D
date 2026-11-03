using UnityEngine;

/// <summary>
/// Progreso que viaja entre escenas (los estáticos sobreviven al cambio de nivel).
/// </summary>
public class PlayerProgress : MonoBehaviour
{
   /// <summary>true cuando el jugador ya ha cogido el arma en el tutorial: pasa a ser su arma base </summary>
   public static bool BaseWeaponUnlocked {  get; set; }

    //Se reinicia al entrar en Play (aunque el Domain Reload esté desactivado)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init() => BaseWeaponUnlocked = false;
}
