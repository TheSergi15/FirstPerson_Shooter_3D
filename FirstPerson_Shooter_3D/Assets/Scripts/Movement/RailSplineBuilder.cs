using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Construye el Spline de los raíles.
///  · Automático: lee los módulos del modelo (hijos de 'Modules Root') y crea un punto por módulo.
///  · Manual: usa los puntos que coloques a mano (o los hijos de este objeto).
/// Uso: menú ⋮ del componente > "Construir spline desde los módulos (automático)" o "...desde los puntos (manual)".
/// </summary>
[RequireComponent(typeof(SplineContainer))]
public class RailSplineBuilder : MonoBehaviour
{
    public enum ModulePointMode { BoundsCenter, Pivot }

    [Header("Modo automático: módulos del modelo de los raíles")]
    [Tooltip("Objeto padre que contiene los módulos de vía como hijos directos")]
    [SerializeField] private Transform modulesRoot;
    [Tooltip("BoundsCenter: centro del módulo. Pivot: la posición de su pivote (úsalo si los pivotes están en el centro de la vía)")]
    [SerializeField] private ModulePointMode pointMode = ModulePointMode.BoundsCenter;
    [Tooltip("Pone cada punto a la altura de la parte superior del módulo")]
    [SerializeField] private bool snapToTop = false;
    [Tooltip("Ordena los módulos por cercanía empezando por el primer hijo. Desmarcado = orden de la Hierarchy")]
    [SerializeField] private bool reorderByProximity = false;

    [Header("Modo manual: puntos colocados a mano")]
    [Tooltip("Vacío = se usan los hijos de este objeto")]
    [SerializeField] private Transform[] points;

    [Header("Spline")]
    [Tooltip("AutoSmooth suaviza las curvas solo. Linear hace tramos rectos")]
    [SerializeField] private TangentMode tangentMode = TangentMode.AutoSmooth;
    [SerializeField] private bool drawGizmos = true;

    // ───────── Automático ─────────
    [ContextMenu("Construir spline desde los módulos (automático)")]
    public void BuildFromModules()
    {
        if (modulesRoot == null)
        {
            Debug.LogWarning("[RailSplineBuilder] Asigna 'Modules Root' (el padre de los módulos de vía).", this);
            return;
        }
        Apply(GetModulePoints());
    }

    private List<Vector3> GetModulePoints()
    {
        List<Vector3> result = new List<Vector3>();
        if (modulesRoot == null) return result;

        foreach (Transform module in modulesRoot)
            if (module.gameObject.activeInHierarchy)
                result.Add(ModulePoint(module));

        return reorderByProximity ? OrderByProximity(result) : result;
    }

    private Vector3 ModulePoint(Transform module)
    {
        Renderer[] renderers = module.GetComponentsInChildren<Renderer>();
        if (pointMode == ModulePointMode.Pivot || renderers.Length == 0)
            return module.position;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);

        Vector3 c = b.center;
        if (snapToTop) c.y = b.max.y;
        return c;
    }

    private static List<Vector3> OrderByProximity(List<Vector3> pts)
    {
        List<Vector3> pending = new List<Vector3>(pts);
        List<Vector3> ordered = new List<Vector3>();
        if (pending.Count == 0) return ordered;

        Vector3 current = pending[0];
        pending.RemoveAt(0);
        ordered.Add(current);

        while (pending.Count > 0)
        {
            int best = 0;
            float bestSq = float.MaxValue;
            for (int i = 0; i < pending.Count; i++)
            {
                float sq = (pending[i] - current).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = i; }
            }
            current = pending[best];
            pending.RemoveAt(best);
            ordered.Add(current);
        }
        return ordered;
    }

    // ───────── Manual ─────────
    [ContextMenu("Construir spline desde los puntos (manual)")]
    public void BuildFromPoints()
    {
        Apply(GetManualPoints());
    }

    private List<Vector3> GetManualPoints()
    {
        List<Vector3> result = new List<Vector3>();

        if (points != null && points.Length > 0)
        {
            foreach (Transform p in points)
                if (p != null) result.Add(p.position);
        }
        else
        {
            foreach (Transform child in transform)
                result.Add(child.position);
        }
        return result;
    }

    // ───────── Común ─────────
    private void Apply(List<Vector3> worldPoints)
    {
        if (worldPoints.Count < 2)
        {
            Debug.LogWarning("[RailSplineBuilder] Hacen falta al menos 2 puntos.", this);
            return;
        }

        SplineContainer container = GetComponent<SplineContainer>();

#if UNITY_EDITOR
        Undo.RecordObject(container, "Construir spline de raíles");
#endif

        Spline spline = container.Spline;
        spline.Clear();

        foreach (Vector3 p in worldPoints)
            spline.Add(new BezierKnot(container.transform.InverseTransformPoint(p)), tangentMode);

        spline.Closed = false;

#if UNITY_EDITOR
        EditorUtility.SetDirty(container);
#endif
        Debug.Log($"[RailSplineBuilder] Spline construido con {spline.Count} puntos.", this);
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        List<Vector3> pts = modulesRoot != null ? GetModulePoints() : GetManualPoints();
        Gizmos.color = Color.green;

        for (int i = 0; i < pts.Count; i++)
        {
            Gizmos.DrawWireSphere(pts[i], 0.2f);
            if (i > 0) Gizmos.DrawLine(pts[i - 1], pts[i]);
        }
    }
}