using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestor único de sonido. Sobrevive entre escenas.
/// Uso desde cualquier script:
///   AudioManager.Sfx("shoot_pistol");
///   AudioManager.Music("music_level1");
///   AudioManager.LoopStart("rail_loop");  AudioManager.LoopStop("rail_loop");
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Biblioteca de sonidos")]
    public SoundLibrary library;

    [Header("Volúmenes (se guardan en PlayerPrefs)")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 0.7f;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    private const int SfxVoices = 12;

    private readonly AudioSource[] musicSources = new AudioSource[2];
    private int activeMusic;
    private string currentMusicId;
    private float currentMusicBase = 1f;
    private Coroutine musicFade;

    private AudioSource[] sfxSources;
    private int nextSfx;

    private readonly Dictionary<string, AudioSource> loops = new Dictionary<string, AudioSource>();
    private readonly Dictionary<string, Coroutine> loopFades = new Dictionary<string, Coroutine>();

    private float MusicTarget => currentMusicBase * musicVolume * masterVolume;

    // ───────────── Ciclo de vida ─────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        masterVolume = PlayerPrefs.GetFloat("vol_master", masterVolume);
        musicVolume = PlayerPrefs.GetFloat("vol_music", musicVolume);
        sfxVolume = PlayerPrefs.GetFloat("vol_sfx", sfxVolume);

        for (int i = 0; i < musicSources.Length; i++)
            musicSources[i] = CreateSource("Music_" + i, true);

        sfxSources = new AudioSource[SfxVoices];
        for (int i = 0; i < SfxVoices; i++)
            sfxSources[i] = CreateSource("Sfx_" + i, false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private AudioSource CreateSource(string sourceName, bool loop)
    {
        var go = new GameObject(sourceName);
        go.transform.SetParent(transform);

        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = loop;
        s.spatialBlend = 0f;
        return s;
    }

    private SoundLibrary.Sound Get(string id)
    {
        if (library == null)
        {
            Debug.LogWarning("[AudioManager] Falta asignar la SoundLibrary.", this);
            return null;
        }

        var snd = library.Get(id);
        if (snd == null)
            Debug.LogWarning($"[AudioManager] No existe el sonido '{id}' en la biblioteca.", this);

        return snd;
    }

    // ───────────── Atajos estáticos (sin comprobar null) ─────────────

    public static void Sfx(string id, float volume = 1f)
    { if (Instance != null) Instance.PlaySfx(id, volume); }

    public static void SfxAt(string id, Vector3 position)
    { if (Instance != null) Instance.PlaySfxAt(id, position); }

    public static void Music(string id, float fade = 1.5f)
    { if (Instance != null) Instance.PlayMusic(id, fade); }

    public static void MusicStop(float fade = 1f)
    { if (Instance != null) Instance.StopMusic(fade); }

    public static void LoopStart(string id)
    { if (Instance != null) Instance.StartLoop(id); }

    public static void LoopStop(string id, float fade = 0.25f)
    { if (Instance != null) Instance.StopLoop(id, fade); }

    // ───────────── Efectos ─────────────

    public void PlaySfx(string id, float volumeMul = 1f)
    {
        var snd = Get(id);
        if (snd == null) return;

        AudioClip clip = snd.Pick();
        if (clip == null) return;

        AudioSource s = sfxSources[nextSfx];
        nextSfx = (nextSfx + 1) % sfxSources.Length;

        s.clip = clip;
        s.volume = snd.volume * sfxVolume * masterVolume * volumeMul;
        s.pitch = Random.Range(snd.pitchRange.x, snd.pitchRange.y);
        s.Play();
    }

    /// <summary>Sonido 3D en una posición del mundo (explosiones, enemigos...).</summary>
    public void PlaySfxAt(string id, Vector3 position)
    {
        var snd = Get(id);
        if (snd == null) return;

        AudioClip clip = snd.Pick();
        if (clip == null) return;

        AudioSource.PlayClipAtPoint(clip, position, snd.volume * sfxVolume * masterVolume);
    }

    // ───────────── Música (con crossfade) ─────────────

    public void PlayMusic(string id, float fade = 1.5f)
    {
        if (id == currentMusicId && musicSources[activeMusic].isPlaying) return;

        var snd = Get(id);
        if (snd == null) return;

        AudioClip clip = snd.Pick();
        if (clip == null) return;

        AudioSource prev = musicSources[activeMusic];
        activeMusic = 1 - activeMusic;
        AudioSource next = musicSources[activeMusic];

        next.clip = clip;
        next.loop = true;
        next.volume = 0f;
        next.Play();

        currentMusicId = id;
        currentMusicBase = snd.volume;

        if (musicFade != null) StopCoroutine(musicFade);
        musicFade = StartCoroutine(Crossfade(prev, next, fade));
    }

    public void StopMusic(float fade = 1f)
    {
        currentMusicId = null;
        if (musicFade != null) StopCoroutine(musicFade);
        musicFade = StartCoroutine(Crossfade(musicSources[activeMusic], null, fade));
    }

    private IEnumerator Crossfade(AudioSource from, AudioSource to, float duration)
    {
        duration = Mathf.Max(0.01f, duration);
        float t = 0f;
        float fromStart = from != null ? from.volume : 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);

            if (to != null) to.volume = Mathf.Lerp(0f, MusicTarget, k);
            if (from != null) from.volume = Mathf.Lerp(fromStart, 0f, k);

            yield return null;
        }

        if (to != null) to.volume = MusicTarget;

        if (from != null)
        {
            from.Stop();
            from.volume = 0f;
        }

        musicFade = null;
    }

    // ───────────── Bucles (sonido del raíl, ambiente...) ─────────────

    public void StartLoop(string id)
    {
        var snd = Get(id);
        if (snd == null) return;

        AudioClip clip = snd.Pick();
        if (clip == null) return;

        if (loopFades.TryGetValue(id, out Coroutine fadeRoutine) && fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            loopFades.Remove(id);
        }

        if (!loops.TryGetValue(id, out AudioSource s) || s == null)
        {
            s = CreateSource("Loop_" + id, true);
            loops[id] = s;
        }

        s.clip = clip;
        s.volume = snd.volume * sfxVolume * masterVolume;
        s.pitch = Random.Range(snd.pitchRange.x, snd.pitchRange.y);

        if (!s.isPlaying) s.Play();
    }

    public void StopLoop(string id, float fade = 0.25f)
    {
        if (!loops.TryGetValue(id, out AudioSource s) || s == null || !s.isPlaying) return;

        if (loopFades.TryGetValue(id, out Coroutine fadeRoutine) && fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        loopFades[id] = StartCoroutine(FadeOutAndStop(s, fade));
    }

    private IEnumerator FadeOutAndStop(AudioSource s, float duration)
    {
        duration = Mathf.Max(0.01f, duration);
        float start = s.volume;
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            s.volume = Mathf.Lerp(start, 0f, t / duration);
            yield return null;
        }

        s.Stop();
    }

    // ───────────── Volúmenes (para los sliders del menú de opciones) ─────────────

    public void SetVolumes(float master, float music, float sfx)
    {
        masterVolume = Mathf.Clamp01(master);
        musicVolume = Mathf.Clamp01(music);
        sfxVolume = Mathf.Clamp01(sfx);

        PlayerPrefs.SetFloat("vol_master", masterVolume);
        PlayerPrefs.SetFloat("vol_music", musicVolume);
        PlayerPrefs.SetFloat("vol_sfx", sfxVolume);

        if (musicFade == null && musicSources[activeMusic].isPlaying)
            musicSources[activeMusic].volume = MusicTarget;
    }

    // Métodos sueltos para conectar directamente a un Slider (On Value Changed)
    public void SetMasterVolume(float v) { SetVolumes(v, musicVolume, sfxVolume); }
    public void SetMusicVolume(float v) { SetVolumes(masterVolume, v, sfxVolume); }
    public void SetSfxVolume(float v) { SetVolumes(masterVolume, musicVolume, v); }
}