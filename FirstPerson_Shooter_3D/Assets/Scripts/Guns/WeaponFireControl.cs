using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Controla CUÁNDO se dispara (cadencia, semiauto / automático / ráfaga) y la dispersión.
/// El script Gun sigue decidiendo QUÉ pasa al disparar (munición, raycast, daño, sonido).
/// </summary>
public class WeaponFireControl : MonoBehaviour
{
    public WeaponData data;

    /// <summary>
    /// Gun lo asigna. Debe devolver true si el disparo se realizó
    /// (hay munición, no está recargando...). Si devuelve false se reintenta tras una pausa corta.
    /// </summary>
    public Func<bool> ShootCallback;

    private const float InputBuffer = 0.12f;   // perdona clics justo antes de poder disparar
    private const float FailCooldown = 0.2f;   // pausa tras un disparo fallido (sin balas...)

    private bool triggerHeld;
    private float pressedAt = -10f;
    private bool bursting;
    private float nextShotTime;

    public void PressTrigger()
    {
        triggerHeld = true;
        pressedAt = Time.time;
    }

    public void ReleaseTrigger()
    {
        triggerHeld = false;
    }

    private void OnDisable()
    {
        triggerHeld = false;
        pressedAt = -10f;
        bursting = false;
        StopAllCoroutines();
    }

    private void Update()
    {
        if (data == null || ShootCallback == null) return;
        if (bursting || Time.time < nextShotTime) return;

        bool pressed = Time.time - pressedAt <= InputBuffer;

        switch (data.fireMode)
        {
            case FireMode.SemiAuto:
                if (pressed) TryFire();
                break;

            case FireMode.FullAuto:
                if (triggerHeld) TryFire();
                break;

            case FireMode.Burst:
                if (pressed) StartCoroutine(BurstRoutine());
                break;
        }
    }

    private float Cooldown => 1f / Mathf.Max(0.05f, data.fireRate);

    private void TryFire()
    {
        pressedAt = -10f;
        bool ok = ShootCallback();
        nextShotTime = Time.time + (ok ? Cooldown : FailCooldown);
    }

    private IEnumerator BurstRoutine()
    {
        bursting = true;
        pressedAt = -10f;

        int count = Mathf.Max(1, data.burstCount);
        bool anyShot = false;

        for (int i = 0; i < count; i++)
        {
            if (!ShootCallback()) break;
            anyShot = true;

            if (i < count - 1)
                yield return new WaitForSeconds(data.burstInterval);
        }

        nextShotTime = Time.time + (anyShot ? Cooldown : FailCooldown);
        bursting = false;
    }

    /// <summary>
    /// Devuelve la dirección de UN proyectil aplicando la dispersión del arma.
    /// Llámalo una vez por cada proyectil (en una escopeta, 'pellets' veces).
    /// </summary>
    public Vector3 GetShotDirection(Vector3 aimDirection)
    {
        if (data == null || data.spreadAngle <= 0f) return aimDirection;
        if (aimDirection.sqrMagnitude < 0.0001f) return aimDirection;

        Vector2 r = UnityEngine.Random.insideUnitCircle
                    * Mathf.Tan(data.spreadAngle * Mathf.Deg2Rad);

        Quaternion rot = Quaternion.LookRotation(aimDirection);
        return (rot * new Vector3(r.x, r.y, 1f)).normalized;
    }
}