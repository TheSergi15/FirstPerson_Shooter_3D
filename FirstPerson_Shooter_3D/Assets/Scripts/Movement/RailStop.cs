using UnityEngine;
using UnityEngine.Events;

/// <summary>Cómo se sale de una parada de la ruta.</summary>
public enum RailAdvanceCondition
{
    KillEnemies,   // al matar el número de enemigos indicado (el comportamiento clásico)
    Manual,        // solo cuando otro script lo ordena (RailManager.GoToNextWaypoint) -> tutorial, cinemáticas...
    AfterDelay     // automáticamente tras unos segundos
}

/// <summary>
/// Ajustes de UNA parada. Se pone en el mismo objeto que el waypoint (junto a ZoneSpawner si lo hay).
/// Si un waypoint no lleva este componente, usa los valores por defecto del RailManager.
/// </summary>
[DisallowMultipleComponent]
public class RailStop : MonoBehaviour
{
    [Header("Cómo se sale de esta parada")]
    public RailAdvanceCondition leaveCondition = RailAdvanceCondition.KillEnemies;
    [Min(1)] public int enemiesToKill = 3;
    [Tooltip("Solo con 'After Delay': segundos que espera antes de avanzar")]
    public float autoAdvanceDelay = 2f;

    [Header("Movimiento HACIA esta parada")]
    public bool overrideSpeed = false;
    public float moveSpeed = 5f;

    [Header("Al llegar")]
    [Tooltip("Segundos de espera tras llegar antes de generar enemigos (ZoneSpawner)")]
    public float spawnDelay = 0f;
    public UnityEvent onArrive;
    [Tooltip("Se lanza justo cuando el jugador sale de esta parada")]
    public UnityEvent onLeave;
}
