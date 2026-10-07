using UnityEngine;

public class ZoneSpawner : MonoBehaviour
{
    // Usamos una clase serializable para agrupar los datos en el Inspector
    [System.Serializable]
    public class SpawnData
    {
        public GameObject enemyPrefab;
        public Transform spawnPoint;       // El "x lugar" donde respawnea
        public Transform destinationPoint; // El "x lugar" hacia donde se traslada
    }

    [Header("Configuración de la Oleada")]
    public SpawnData[] enemiesToSpawn;

    public void SpawnEnemies()
    {
        foreach (var data in enemiesToSpawn)
        {
            // 1. Instanciamos al enemigo en el punto de aparición
            GameObject enemy = Instantiate(data.enemyPrefab, data.spawnPoint.position, data.spawnPoint.rotation);

            // 2. Buscamos su script de movimiento y le pasamos el destino
            EnemyMover mover = enemy.GetComponent<EnemyMover>();
            if (mover != null)
            {
                mover.SetDestination(data.destinationPoint);
            }
        }
    }
}
