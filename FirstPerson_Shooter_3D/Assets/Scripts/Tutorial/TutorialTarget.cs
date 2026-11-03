using UnityEngine;

/// <summary>
/// Se pone en el arma, el pickup de curación y la granada del tutorial.
/// El TutorialManager muestra el aviso correspondiente cuando el puntero apunta a este objeto.
/// El objeto necesita un Collider.
/// </summary>
public class TutorialTarget : MonoBehaviour
{
    public TutorialTargetType type;
    [Tooltip("Distancia máxima desde la cámara para que salte el aviso")]
    public float maxAimDistance = 10f;
}
