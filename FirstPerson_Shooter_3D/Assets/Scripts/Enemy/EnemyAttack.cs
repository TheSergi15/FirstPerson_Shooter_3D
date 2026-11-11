using System.Collections;
using UnityEngine;

public class EnemyAttack : MonoBehaviour
{
    [Header("Atributos de Ataque")]
    public float attackDamage = 10f;
    [Tooltip("Tiempo en segundos que tarda en disparar o atacar")]
    public float timeBetweenAttacks = 3f;

    private EnemyMover mover;

    void Start()
    {
        mover = GetComponent<EnemyMover>();
        StartCoroutine(AttackRoutine());
    }

    IEnumerator AttackRoutine()
    {
        // Mientras el enemigo exista, ejecutará este bucle
        while (true)
        {
            yield return new WaitForSeconds(timeBetweenAttacks);

            // Opcional: Solo ataca si ya llegó a su punto B (Destino)
            // Si quieres que ataquen mientras corren, borra esta condición
            if (mover != null && mover.HasReachedDestination())
            {
                ExecuteAttack();
            }
            else if (mover == null)
            {
                ExecuteAttack();
            }
        }
    }

    void ExecuteAttack()
    {
        if (PlayerHealth.Instance != null)
        {
            Debug.Log($"¡{gameObject.name} te ha atacado!");
            PlayerHealth.Instance.TakeDamage(attackDamage);

            // Más adelante, aquí conectarás el trigger de tu Animator:
            // animator.SetTrigger("Attack");
        }
    }
}