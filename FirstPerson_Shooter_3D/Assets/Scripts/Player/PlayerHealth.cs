using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHealth : MonoBehaviour
{
    public static PlayerHealth Instance;

    [Header("Salud")]
    public float maxHealth = 100f;
    private float currentHealth;

    [Header("Corazones")]
    [Tooltip("Objeto vacío del Canvas usado SOLO para los corazones")]
    public RectTransform heartsContainer;
    public Sprite fullHeartSprite;
    [Tooltip("Opcional: sprite del corazón vacío (contorno). Si está vacío, el corazón perdido desaparece")]
    public Sprite emptyHeartSprite;
    public int maxHearts = 5;
    public Vector2 heartSize = new Vector2(64f, 64f);
    public float heartSpacing = 8f;

    [Header("Animación de corazón roto")]
    public float shakeDuration = 0.15f;   // Temblor antes de romperse
    public float shakeStrength = 4f;
    public float fallDuration = 0.9f;     // Lo que tarda en caer y desvanecerse
    public float gravity = 1200f;
    public float splitSpeed = 90f;        // Cuánto se separan las dos mitades
    public float jumpSpeed = 150f;        // Pequeño salto al romperse
    public float spinSpeed = 160f;        // Giro de cada mitad

    [Header("Interfaz de Usuario (UI)")]
    public TextMeshProUGUI healthText;

    [Header("Efectos de Daño y Curación en UI")]
    [Tooltip("Componente DamageFlash de la Image a pantalla completa")]
    public DamageFlash flashEffect;

    private readonly List<Image> hearts = new List<Image>();
    private readonly List<GameObject> activeHalves = new List<GameObject>();
    private int visibleHearts;
    private float healthPerHeart;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            // Antes se destruía el GameObject entero (podía ser el jugador).
            // Ahora solo se quita este componente duplicado y se avisa en la Consola.
            Debug.LogWarning("Hay más de un PlayerHealth en la escena. Duplicado en: " + gameObject.name, gameObject);
            enabled = false;
            Destroy(this);
        }
    }

    void Start()
    {
        currentHealth = maxHealth;
        healthPerHeart = maxHealth / Mathf.Max(1, maxHearts);

        BuildHearts();
        UpdateHealthUI();
    }

    void OnDestroy()
    {
        // Paramos las animaciones y limpiamos las mitades que sigan en pantalla
        StopAllCoroutines();

        foreach (GameObject go in activeHalves)
        {
            if (go != null) Destroy(go);
        }
        activeHalves.Clear();

        if (Instance == this) Instance = null;
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;

        // Flash rojo: más fuerte cuanto mayor es el golpe respecto a la vida máxima
        if (flashEffect != null) flashEffect.OnDamage(amount / maxHealth);

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            UpdateHealthUI();
            Die();
            return;
        }

        UpdateHealthUI();
    }

    public void Heal(float amount)
    {
        currentHealth += amount;

        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }

        if (flashEffect != null) flashEffect.OnHeal(); // Flash verde
        UpdateHealthUI();
        Debug.Log("¡Vida recuperada! Salud actual: " + currentHealth);
    }

    void UpdateHealthUI()
    {
        if (healthText != null)
        {
            healthText.text = "VIDA: " + Mathf.CeilToInt(currentHealth);
        }

        // El efecto usa el porcentaje de vida para el pulso de vida baja
        if (flashEffect != null) flashEffect.SetHealthPercent(currentHealth / maxHealth);

        UpdateHearts();
    }

    // ---------------- CORAZONES ----------------

    // Crea los corazones dentro del contenedor (no hace falta montarlos a mano)
    void BuildHearts()
    {
        if (heartsContainer == null || fullHeartSprite == null) return;

        HorizontalLayoutGroup layout = heartsContainer.GetComponent<HorizontalLayoutGroup>();
        if (layout == null) layout = heartsContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = heartSpacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        hearts.Clear();

        for (int i = 0; i < maxHearts; i++)
        {
            GameObject go = new GameObject("Heart_" + i, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(heartsContainer, false);

            Image img = go.GetComponent<Image>();
            img.sprite = fullHeartSprite;
            img.preserveAspect = true;
            img.raycastTarget = false;

            LayoutElement le = go.GetComponent<LayoutElement>();
            le.preferredWidth = heartSize.x;
            le.preferredHeight = heartSize.y;

            hearts.Add(img);
        }

        visibleHearts = hearts.Count;
    }

    // Compara los corazones que toca mostrar con los que hay y rompe o recupera los que cambien
    void UpdateHearts()
    {
        if (hearts.Count == 0 || heartsContainer == null) return;

        int target = Mathf.CeilToInt(currentHealth / healthPerHeart - 0.0001f);
        target = Mathf.Clamp(target, 0, hearts.Count);

        // Corazones perdidos: se rompen de derecha a izquierda
        for (int i = visibleHearts - 1; i >= target; i--)
        {
            if (hearts[i] != null) StartCoroutine(BreakHeartRoutine(hearts[i]));
        }

        // Corazones recuperados
        for (int i = visibleHearts; i < target; i++)
        {
            if (hearts[i] != null) RestoreHeart(hearts[i]);
        }

        visibleHearts = target;
    }

    void SetHeartLost(Image heart)
    {
        if (heart == null) return;

        if (emptyHeartSprite != null)
        {
            heart.sprite = emptyHeartSprite;
            heart.enabled = true;
        }
        else
        {
            // Desactivamos la imagen pero mantiene su hueco en el layout
            heart.enabled = false;
        }
    }

    void RestoreHeart(Image heart)
    {
        heart.sprite = fullHeartSprite;
        heart.enabled = true;
        StartCoroutine(PopRoutine(heart.rectTransform));
    }

    // Crea una mitad (izquierda o derecha) del corazón, usando el mismo sprite
    Image CreateHalf(Image heart, bool leftHalf)
    {
        GameObject go = new GameObject(leftHalf ? "HeartHalfLeft" : "HeartHalfRight",
            typeof(RectTransform), typeof(Image), typeof(LayoutElement));

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(heartsContainer, false);

        // Copiamos posición y tamaño del corazón original
        RectTransform src = heart.rectTransform;
        rt.anchorMin = src.anchorMin;
        rt.anchorMax = src.anchorMax;
        rt.pivot = src.pivot;
        rt.sizeDelta = src.sizeDelta;
        rt.anchoredPosition = src.anchoredPosition;

        // Que el layout no recoloque la mitad mientras cae
        go.GetComponent<LayoutElement>().ignoreLayout = true;

        Image img = go.GetComponent<Image>();
        img.sprite = fullHeartSprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = leftHalf ? (int)Image.OriginHorizontal.Left : (int)Image.OriginHorizontal.Right;
        img.fillAmount = 0.5f;

        activeHalves.Add(go);
        return img;
    }

    void RemoveHalf(Image half)
    {
        if (half == null) return;

        activeHalves.Remove(half.gameObject);
        Destroy(half.gameObject);
    }

    IEnumerator BreakHeartRoutine(Image heart)
    {
        if (heart == null || heartsContainer == null) yield break;

        Image left = CreateHalf(heart, true);
        Image right = CreateHalf(heart, false);
        RectTransform leftRT = left.rectTransform;
        RectTransform rightRT = right.rectTransform;

        Vector2 startLeft = leftRT.anchoredPosition;
        Vector2 startRight = rightRT.anchoredPosition;

        // Las dos mitades ocupan el lugar del corazón, así que ocultamos el original
        SetHeartLost(heart);

        // 1) Temblor antes de romperse
        float t = 0f;
        while (t < shakeDuration && left != null && right != null)
        {
            t += Time.unscaledDeltaTime;
            Vector2 shake = Random.insideUnitCircle * shakeStrength;
            leftRT.anchoredPosition = startLeft + shake;
            rightRT.anchoredPosition = startRight + shake;
            yield return null;
        }

        // 2) Las mitades se separan, giran, caen y se desvanecen
        Vector2 leftPos = startLeft;
        Vector2 rightPos = startRight;
        Vector2 leftVel = new Vector2(-splitSpeed, jumpSpeed);
        Vector2 rightVel = new Vector2(splitSpeed, jumpSpeed);
        float leftAngle = 0f;
        float rightAngle = 0f;
        t = 0f;

        while (t < fallDuration && left != null && right != null)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;

            leftVel.y -= gravity * dt;
            rightVel.y -= gravity * dt;
            leftPos += leftVel * dt;
            rightPos += rightVel * dt;
            leftAngle += spinSpeed * dt;
            rightAngle -= spinSpeed * dt;

            leftRT.anchoredPosition = leftPos;
            rightRT.anchoredPosition = rightPos;
            leftRT.localRotation = Quaternion.Euler(0f, 0f, leftAngle);
            rightRT.localRotation = Quaternion.Euler(0f, 0f, rightAngle);

            float alpha = Mathf.Clamp01(1f - (t / fallDuration));
            left.color = new Color(1f, 1f, 1f, alpha);
            right.color = new Color(1f, 1f, 1f, alpha);

            yield return null;
        }

        RemoveHalf(left);
        RemoveHalf(right);
    }

    // Pequeño "pop" cuando se recupera un corazón
    IEnumerator PopRoutine(RectTransform rt)
    {
        const float duration = 0.25f;
        float t = 0f;

        while (t < duration && rt != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            rt.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, k);
            yield return null;
        }

        if (rt != null) rt.localScale = Vector3.one;
    }

    void Die()
    {
        Debug.Log("¡HAS MUERTO! Fin del juego.");
    }
}
