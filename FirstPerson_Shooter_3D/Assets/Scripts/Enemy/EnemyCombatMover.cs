using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Mueve al enemigo hasta su posición de tiro con el NavMeshAgent, se DETIENE al llegar,
/// se queda mirando al jugador y ATACA cada cierto tiempo. No persigue al jugador.
/// Ponlo en el prefab del enemigo (junto al NavMeshAgent) y QUITA el EnemyMover anterior,
/// para que no haya dos scripts dando órdenes al mismo agente.
/// El ZoneSpawner lo detecta solo y le pasa el destino.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyCombatMover : MonoBehaviour
{
    [Header("Llegada")]
    [Tooltip("Distancia al destino a la que se considera que ha llegado y se detiene.")]
    [Min(0.05f)] public float arriveDistance = 0.35f;

    [Tooltip("Si queda parado este tiempo cerca del destino (otro enemigo ocupa el sitio...), se da por llegado.")]
    public float stuckTimeout = 1.5f;

    [Header("Al llegar")]
    public bool faceTargetWhenStopped = true;
    public float turnSpeed = 8f;

    [Tooltip("A quién mira al llegar. Vacío = el jugador del RailManager (o la cámara principal).")]
    public Transform lookTarget;

    [Header("Ataque")]
    [Tooltip("Si está desactivado, el enemigo llega y se queda quieto sin atacar.")]
    public bool canAttack = true;

    [Tooltip("Daño que hace cada ataque al jugador.")]
    public int damage = 10;

    [Tooltip("Segundos entre un ataque y el siguiente.")]
    [Min(0.1f)] public float attackCooldown = 2f;

    [Tooltip("Segundos que espera tras llegar antes del primer ataque.")]
    public float firstAttackDelay = 1f;

    [Tooltip("Segundos entre que empieza la animación de ataque y que se aplica el daño.")]
    public float attackWindup = 0.3f;

    [Tooltip("Distancia máxima al objetivo para poder atacar. 0 = sin límite.")]
    public float attackRange = 0f;

    [Tooltip("Probabilidad de acertar (0 = nunca, 1 = siempre).")]
    [Range(0f, 1f)] public float accuracy = 0.8f;

    [Header("Animator (opcional)")]
    public Animator animator;

    [Tooltip("Parámetro Float del Animator que recibe la velocidad. Vacío = no se toca el Animator.")]
    public string speedParameter = "";
    public float animatorDamp = 0.1f;

    [Tooltip("Parámetro Trigger del Animator que se lanza al atacar. Vacío = no se toca el Animator.")]
    public string attackTrigger = "";

    /// <summary>Se dispara cuando el enemigo llega a su posición y se detiene.</summary>
    public event Action<EnemyCombatMover> Arrived;

    /// <summary>Se dispara cuando empieza un ataque (útil para sonido, partículas, fogonazo...).</summary>
    public event Action<EnemyCombatMover> AttackStarted;

    /// <summary>Se dispara cuando el ataque se resuelve. El bool indica si ha acertado.</summary>
    public event Action<EnemyCombatMover, bool> AttackResolved;

    public bool HasArrived => arrived;
    public bool HasDestination => hasDestination;
    public bool IsAttacking => attacking;

    private NavMeshAgent agent;
    private Vector3 destination;
    private bool hasDestination;
    private bool arrived;
    private float stuckTimer;
    private bool animatorReady;
    private bool attackTriggerReady;
    private Transform resolvedTarget;

    private bool attacking;
    private float nextAttackTime;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (animator != null)
        {
            animatorReady = HasParameter(speedParameter, AnimatorControllerParameterType.Float);
            attackTriggerReady = HasParameter(attackTrigger, AnimatorControllerParameterType.Trigger);
        }
    }

    private bool HasParameter(string paramName, AnimatorControllerParameterType type)
    {
        if (string.IsNullOrEmpty(paramName)) return false;

        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.name == paramName && p.type == type) return true;
        }

        Debug.LogWarning($"[EnemyCombatMover] {name}: el Animator no tiene un parámetro {type} llamado '{paramName}'.", this);
        return false;
    }

    private void OnDisable()
    {
        // Si el enemigo se desactiva a mitad de un ataque, la corrutina se cancela sola.
        attacking = false;
    }

    public void SetDestination(Transform target)
    {
        if (target != null) SetDestination(target.position);
    }

    public void SetDestination(Vector3 position)
    {
        destination = position;
        hasDestination = true;
        arrived = false;
        stuckTimer = 0f;

        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            Debug.LogWarning($"[EnemyCombatMover] {name}: el NavMeshAgent no está activo sobre el NavMesh; no puede moverse.", this);
            return;
        }

        agent.isStopped = false;
        agent.updateRotation = true;
        agent.stoppingDistance = 0f;   // la llegada la controla arriveDistance
        agent.SetDestination(position);
    }

    private void Update()
    {
        UpdateAnimator();

        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        if (arrived)
        {
            if (faceTargetWhenStopped) FaceTarget();
            TryAttack();
            return;
        }

        if (!hasDestination || agent.pathPending) return;

        // ¿Ha llegado?
        float flat = FlatDistanceToDestination();
        if (agent.remainingDistance <= arriveDistance || flat <= arriveDistance)
        {
            Arrive();
            return;
        }

        // Atascado cerca del destino
        if (agent.velocity.sqrMagnitude < 0.01f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= stuckTimeout && flat <= arriveDistance * 4f) Arrive();
        }
        else
        {
            stuckTimer = 0f;
        }
    }

    private void Arrive()
    {
        arrived = true;

        agent.isStopped = true;
        agent.ResetPath();
        agent.velocity = Vector3.zero;
        agent.updateRotation = false;   // desde ahora gira solo hacia el objetivo

        nextAttackTime = Time.time + firstAttackDelay;

        Arrived?.Invoke(this);
    }

    // ───────────────────────── ATAQUE ─────────────────────────

    private void TryAttack()
    {
        if (!canAttack || attacking) return;
        if (Time.time < nextAttackTime) return;
        if (!IsTargetInRange()) return;

        Attack();
    }

    /// <summary>Lanza un ataque ahora mismo (respeta que no haya otro en curso). Se puede llamar desde fuera.</summary>
    public void Attack()
    {
        if (attacking || !isActiveAndEnabled) return;
        StartCoroutine(AttackRoutine());
    }

    /// <summary>Activa o desactiva el ataque (por ejemplo, al morir el jugador).</summary>
    public void SetCanAttack(bool value)
    {
        canAttack = value;
    }

    private IEnumerator AttackRoutine()
    {
        attacking = true;

        if (attackTriggerReady) animator.SetTrigger(attackTrigger);
        AttackStarted?.Invoke(this);

        if (attackWindup > 0f) yield return new WaitForSeconds(attackWindup);

        bool hit = UnityEngine.Random.value <= accuracy;
        if (hit) DealDamage();

        AttackResolved?.Invoke(this, hit);

        nextAttackTime = Time.time + attackCooldown;
        attacking = false;
    }

    private void DealDamage()
    {
        // Ajusta esta línea al nombre real del método de tu PlayerHealth si es distinto.
        if (PlayerHealth.Instance != null)
            PlayerHealth.Instance.TakeDamage(damage);
    }

    private bool IsTargetInRange()
    {
        if (attackRange <= 0f) return true;

        Transform target = ResolveTarget();
        if (target == null) return false;

        return Vector3.Distance(transform.position, target.position) <= attackRange;
    }

    // ───────────────────────── RESTO ─────────────────────────

    private float FlatDistanceToDestination()
    {
        Vector3 d = destination - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    private void FaceTarget()
    {
        Transform target = ResolveTarget();
        if (target == null) return;

        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion look = Quaternion.LookRotation(dir);
        float k = 1f - Mathf.Exp(-turnSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, k);
    }

    private Transform ResolveTarget()
    {
        if (lookTarget != null) return lookTarget;

        if (resolvedTarget == null)
        {
            if (RailManager.Instance != null && RailManager.Instance.playerParent != null)
                resolvedTarget = RailManager.Instance.playerParent;
            else if (Camera.main != null)
                resolvedTarget = Camera.main.transform;
        }
        return resolvedTarget;
    }

    private void UpdateAnimator()
    {
        if (!animatorReady) return;

        float speed = (agent != null && agent.enabled && agent.isOnNavMesh && !arrived)
            ? agent.velocity.magnitude
            : 0f;

        animator.SetFloat(speedParameter, speed, animatorDamp, Time.deltaTime);
    }
}