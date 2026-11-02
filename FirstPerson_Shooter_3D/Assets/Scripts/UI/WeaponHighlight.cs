using System.Collections.Generic;
using UnityEngine;

public class WeaponHighlight : MonoBehaviour
{
    [ColorUsage(false, true)] public Color highlightColor = new Color(1f, 0.75f, 0.2f);
    public float intensity = 1.5f;
    [Range(0f, 1f)] public float pulse = 0.35f;
    public float fadeSpeed = 6f;

    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    class Entry
    {
        public Material material;
        public Color originalEmission;
        public bool hadKeyword;
    }

    List<Entry> entries;
    float amount;   // 0 = apagado, 1 = brillo completo

    void BuildCache()
    {
        entries = new List<Entry>();

        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;

            foreach (Material m in r.materials)   // copias propias de esta arma
            {
                if (!m.HasProperty(EmissionColorId)) continue;

                bool hasKeyword = m.IsKeywordEnabled("_EMISSION");
                if (!hasKeyword)
                    Debug.LogWarning($"[WeaponHighlight] {name}: el material '{m.name}' no tiene Emission activado; en la build puede no brillar.", this);

                entries.Add(new Entry
                {
                    material = m,
                    originalEmission = m.GetColor(EmissionColorId),
                    hadKeyword = hasKeyword
                });
            }
        }
    }

    public void SetHighlighted(bool on)
    {
        float goal = on ? 1f : 0f;
        if (goal == 0f && amount == 0f) return;

        if (entries == null) BuildCache();

        amount = Mathf.MoveTowards(amount, goal, fadeSpeed * Time.deltaTime);
        Apply();
    }

    public void ResetNow()
    {
        if (entries == null || amount == 0f) return;
        amount = 0f;
        Apply();
    }

    void Apply()
    {
        float p = 1f - pulse + pulse * (0.5f + 0.5f * Mathf.Sin(Time.time * 5f));
        Color glow = highlightColor * (intensity * p * amount);

        foreach (Entry e in entries)
        {
            if (e.material == null) continue;

            if (amount > 0f)
            {
                e.material.EnableKeyword("_EMISSION");
                e.material.SetColor(EmissionColorId, e.originalEmission + glow);
            }
            else
            {
                e.material.SetColor(EmissionColorId, e.originalEmission);
                if (!e.hadKeyword) e.material.DisableKeyword("_EMISSION");
            }
        }
    }

    void OnDestroy()
    {
        if (entries == null) return;
        foreach (Entry e in entries)
            if (e.material != null) Destroy(e.material);
    }
}