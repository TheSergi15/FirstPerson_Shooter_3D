using UnityEngine;

public class EnemyMover : MonoBehaviour
{
    [Header("Movimiento")]
    public float moveSpeed = 6f;

    [Header("Destino (Para enemigos precolocados)")]
    [Tooltip("Arrastra aquí el Punto B al que correrá el enemigo. Déjalo vacío si usa Spawner.")]
    public Transform targetDestination;

    // Esta función recibe el destino desde el Spawner (para las oleadas dinámicas)
    public void SetDestination(Transform destination)
    {
        targetDestination = destination;
    }

    public bool HasReachedDestination()
    {
        if (targetDestination == null) return true;

        // Devuelve verdadero si está a menos de 0.5 unidades de su destino
        return Vector3.Distance(transform.position, targetDestination.position) < 0.5f;
    }

    void Update()
    {
        // Si tiene un destino asignado (ya sea manual o por Spawner), camina hacia él
        if (targetDestination != null)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetDestination.position, moveSpeed * Time.deltaTime);

            // Hace que el enemigo mire hacia donde corre
            Vector3 direction = (targetDestination.position - transform.position).normalized;
            if (direction != Vector3.zero)
            {
                Quaternion lookRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 10f);
            }
        }
    }
}