using UnityEngine;

/// <summary>
/// Keeps the NavMeshAgent root on the floor while lifting the visual child so its feet
/// sit at the root position. Add this to the enemy root and assign its graphics child.
/// </summary>
[DisallowMultipleComponent]
public class EnemyVisualGrounding : MonoBehaviour
{
    [Header("Visual")]
    [Tooltip("Child containing the model/sprite, Animator and visual colliders. Keep NavMeshAgent and EnemyMover on this object's parent root.")]
    [SerializeField] private Transform visualRoot;

    [Header("Root collider")]
    [Tooltip("For a vertical CapsuleCollider on the root, moves its center up so its bottom aligns with the root position.")]
    [SerializeField] private bool alignRootCapsuleToFeet = true;

    [Tooltip("Aligns the lowest visible point of the visual child with the root's Y position when the enemy is created.")]
    [SerializeField] private bool alignVisualOnAwake = true;

    [Tooltip("Extra visual height above the NavMesh root, in world units.")]
    [SerializeField] private float heightOffset;

    private float appliedHeightOffset;

    public float HeightOffset => heightOffset;

    private void Reset()
    {
        if (visualRoot == null) visualRoot = transform.Find("Visual");
    }

    private void Awake()
    {
        if (visualRoot == null)
        {
            Transform found = transform.Find("Visual");
            if (found != null) visualRoot = found;
        }

        AlignRootCapsule();
        if (alignVisualOnAwake) AlignVisualFeet();
    }

    private void AlignRootCapsule()
    {
        if (!alignRootCapsuleToFeet) return;
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule == null) return;
        if (capsule.direction != 1)
        {
            Debug.LogWarning($"[{nameof(EnemyVisualGrounding)}] {name}: el CapsuleCollider de la raíz no usa el eje Y; no se modificó.", this);
            return;
        }

        Vector3 center = capsule.center;
        center.y = capsule.height * 0.5f;
        capsule.center = center;
    }

    /// <summary>Moves only the visual child, never the NavMeshAgent root.</summary>
    [ContextMenu("Align Visual Feet Now")]
    public void AlignVisualFeet()
    {
        if (visualRoot == null)
        {
            Debug.LogWarning($"[{nameof(EnemyVisualGrounding)}] {name}: asigna Visual Root (el hijo del modelo/sprite).", this);
            return;
        }

        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        bool foundRenderer = false;
        float lowestY = float.PositiveInfinity;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled) continue;
            Bounds bounds = renderer.bounds;
            if (bounds.size.sqrMagnitude < 0.0001f) continue;
            lowestY = Mathf.Min(lowestY, bounds.min.y);
            foundRenderer = true;
        }

        if (!foundRenderer)
        {
            Debug.LogWarning($"[{nameof(EnemyVisualGrounding)}] {name}: no hay Renderer activo dentro de Visual Root.", this);
            return;
        }

        float verticalCorrection = transform.position.y - lowestY;
        float correction = verticalCorrection + heightOffset;
        if (Mathf.Abs(correction) > 0.001f) visualRoot.position += Vector3.up * correction;
        appliedHeightOffset = heightOffset;

        Physics.SyncTransforms();
    }

    /// <summary>Set by ZoneSpawner after instantiation. Moves only the visual child.</summary>
    public void SetHeightOffset(float offset)
    {
        heightOffset = offset;
        if (alignVisualOnAwake) AlignVisualFeet();
        else ApplyHeightOffsetOnly();
    }

    private void ApplyHeightOffsetOnly()
    {
        if (visualRoot == null) return;
        visualRoot.position += Vector3.up * (heightOffset - appliedHeightOffset);
        appliedHeightOffset = heightOffset;
        Physics.SyncTransforms();
    }
}
