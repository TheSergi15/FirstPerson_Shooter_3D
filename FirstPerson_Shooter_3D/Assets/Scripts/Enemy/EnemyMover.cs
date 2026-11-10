using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NavMesh version of the project's EnemyMover. Put this and the NavMeshAgent on the
/// navigation root. Keep the HD sprite, Animator, and visual pivots as children.
/// Do not move this transform directly while a NavMeshAgent controls it.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyMover : MonoBehaviour
{
    [Header("Movimiento")]
    [Min(0f)] public float moveSpeed = 6f;

    [Header("Destino (Para enemigos precolocados)")]
    [Tooltip("Arrastra aquí el Punto B. Déjalo vacío si usa Spawner.")]
    public Transform targetDestination;

    [Header("NavMesh")]
    [SerializeField] private NavMeshAgent agent;
    [Min(0.01f)] public float stoppingDistance = 0.35f;
    [Min(0.01f)] public float destinationSampleDistance = 1f;

    private void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        ApplyAgentSettings();
    }

    private void OnEnable()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        ApplyAgentSettings();
    }

    private Vector3 lastRequestedDestination;
    private bool hasRequestedDestination;

    private void Start()
    {
        // Supports enemies placed in the scene with a destination already assigned.
        if (targetDestination != null) UpdateAgentDestination();
    }

    private void ApplyAgentSettings()
    {
        if (agent == null) return;
        agent.speed = moveSpeed;
        agent.stoppingDistance = stoppingDistance;
        agent.updatePosition = true;
        // For 2D HD sprites, rotate the visual child separately if needed.
        agent.updateRotation = true;
    }

    // Kept compatible with ZoneSpawner and preplaced-enemy setup.
    public void SetDestination(Transform destination)
    {
        targetDestination = destination;
        UpdateAgentDestination();
    }

    public bool HasReachedDestination()
    {
        if (targetDestination == null) return true;
        if (agent == null || !agent.enabled || !agent.isOnNavMesh || agent.pathPending) return false;
        if (agent.pathStatus == NavMeshPathStatus.PathInvalid) return false;
        return agent.remainingDistance <= Mathf.Max(stoppingDistance, agent.stoppingDistance) + 0.05f;
    }

    private void Update()
    {
        if (agent == null) return;
        // Keep Inspector changes to speed effective during play.
        if (!Mathf.Approximately(agent.speed, moveSpeed)) agent.speed = moveSpeed;

        if (targetDestination == null || !agent.enabled || !agent.isOnNavMesh) return;

        // Re-evaluate moving targets, but avoid rebuilding the path every frame.
        if (!hasRequestedDestination ||
            (lastRequestedDestination - targetDestination.position).sqrMagnitude > 0.04f)
            UpdateAgentDestination();
    }

    private void UpdateAgentDestination()
    {
        if (targetDestination == null || agent == null || !agent.enabled) return;
        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning($"[{nameof(EnemyMover)}] {name}: NavMeshAgent no está sobre un NavMesh; no se mueve.", this);
            return;
        }

        if (NavMesh.SamplePosition(targetDestination.position, out NavMeshHit hit,
                Mathf.Max(destinationSampleDistance, agent.radius), agent.areaMask))
        {
            lastRequestedDestination = targetDestination.position;
            hasRequestedDestination = true;
            if (!agent.SetDestination(hit.position))
                Debug.LogWarning($"[{nameof(EnemyMover)}] {name}: no se pudo calcular la ruta al destino.", this);
        }
        else
        {
            lastRequestedDestination = targetDestination.position;
            hasRequestedDestination = true;
            agent.ResetPath();
            Debug.LogWarning($"[{nameof(EnemyMover)}] {name}: no hay NavMesh cerca del destino.", this);
        }
    }
}
