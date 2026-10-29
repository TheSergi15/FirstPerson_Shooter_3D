using System.Collections;
using UnityEngine;

public class RailManager : MonoBehaviour
{
    // Singleton para acceder fácilmente desde otros scripts
    public static RailManager Instance;

    [Header("Configuración de Ruta")]
    [Tooltip("Arrastra aquí tu objeto Player1")]
    public Transform playerParent;
    [Tooltip("Puntos a los que se moverá el jugador en orden")]
    public Transform[] waypoints;
    public float moveSpeed = 5f;

    [Header("Progreso de Zona actual")]
    [Tooltip("Cuántos enemigos deben morir para avanzar al siguiente punto")]
    public int enemiesToKillForNextZone = 3;
    private int currentKills = 0;
    private int currentWaypointIndex = 0;

    private void Awake()
    {
        // Configuramos el Singleton
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // Esta función será llamada por los enemigos al morir
    public void AddKill()
    {
        currentKills++;
        Debug.Log("Progreso de zona: " + currentKills + " / " + enemiesToKillForNextZone);

        // Verificamos si ya eliminamos a los enemigos requeridos
        if (currentKills >= enemiesToKillForNextZone)
        {
            AdvanceToNextZone();
        }
    }

    void AdvanceToNextZone()
    {
        currentKills = 0; // Reiniciamos el contador de bajas

        if (currentWaypointIndex < waypoints.Length)
        {
            Debug.Log("¡Zona limpia! Moviendo al jugador...");
            StartCoroutine(MoveToWaypoint(waypoints[currentWaypointIndex]));
            currentWaypointIndex++;

            // Opcional: Aquí podrías cambiar 'enemiesToKillForNextZone' 
            // dependiendo de la nueva zona a la que llegues.
        }
        else
        {
            Debug.Log("¡Has llegado al final del nivel!");
        }
    }

    IEnumerator MoveToWaypoint(Transform targetWaypoint)
    {
        // Mueve al jugador mientras no haya llegado a la posición destino
        while (Vector3.Distance(playerParent.position, targetWaypoint.position) > 0.1f)
        {
            playerParent.position = Vector3.MoveTowards(playerParent.position, targetWaypoint.position, moveSpeed * Time.deltaTime);
            playerParent.rotation = Quaternion.Lerp(playerParent.rotation, targetWaypoint.rotation, (moveSpeed * 0.5f) * Time.deltaTime);
            yield return null;
        }

        // --- NUEVO: Activamos el spawner de la zona al llegar ---
        ZoneSpawner spawner = targetWaypoint.GetComponent<ZoneSpawner>();
        if (spawner != null)
        {
            Debug.Log("¡Llegamos al waypoint! Generando enemigos...");
            spawner.SpawnEnemies();
        }
    }
}