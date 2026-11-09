using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lista de todos los sonidos del juego (música, efectos, UI), cada uno con un id de texto.
/// Se crea desde: Assets > Create > Juego > Biblioteca de sonidos.
/// </summary>
[CreateAssetMenu(fileName = "SoundLibrary", menuName = "Juego/Biblioteca de sonidos")]
public class SoundLibrary : ScriptableObject
{
    [System.Serializable]
    public class Sound
    {
        public string id;

        [Tooltip("Si pones varios clips, se elige uno al azar cada vez (más variedad).")]
        public AudioClip[] clips;

        [Range(0f, 1f)] public float volume = 1f;

        [Tooltip("Rango de pitch aleatorio. (1,1) = sin variación. Ej: (0.95, 1.05)")]
        public Vector2 pitchRange = new Vector2(1f, 1f);

        public AudioClip Pick()
        {
            if (clips == null || clips.Length == 0) return null;
            return clips[Random.Range(0, clips.Length)];
        }
    }

    public List<Sound> sounds = new List<Sound>();

    private Dictionary<string, Sound> lookup;

    public Sound Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        if (lookup == null || lookup.Count != sounds.Count)
        {
            lookup = new Dictionary<string, Sound>();
            foreach (var s in sounds)
            {
                if (s != null && !string.IsNullOrEmpty(s.id))
                    lookup[s.id] = s;
            }
        }

        lookup.TryGetValue(id, out Sound result);
        return result;
    }
}