using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Yarn.Unity;

public class SfxController : MonoBehaviour
{
    private const string SfxResourcesPath = "SFX/";

    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private AudioSource oneShotAudioSource;
    [SerializeField] private float defaultFadeDuration = 0.2f;
    [SerializeField] private float defaultVolume = 1f;

    private readonly Dictionary<string, LoopState> loopStates = new Dictionary<string, LoopState>();
    private bool commandRegistered;

    private sealed class LoopState
    {
        public AudioSource AudioSource;
        public int FadeVersion;
        public float CommandVolume = 1f;
    }

    private void Awake()
    {
        if (dialogueRunner == null)
        {
            dialogueRunner = GetComponent<DialogueRunner>();
        }

        if (oneShotAudioSource == null)
        {
            oneShotAudioSource = gameObject.AddComponent<AudioSource>();
        }

        oneShotAudioSource.playOnAwake = false;
        oneShotAudioSource.loop = false;
    }

    private void OnValidate()
    {
        defaultFadeDuration = Mathf.Max(0f, defaultFadeDuration);
        defaultVolume = Mathf.Clamp01(defaultVolume);

        foreach (LoopState state in loopStates.Values)
        {
            if (state.AudioSource != null && state.AudioSource.isPlaying)
            {
                state.AudioSource.volume = GetEffectiveVolume(state.CommandVolume);
            }
        }
    }

    private void OnEnable()
    {
        RegisterCommand();
    }

    private void OnDisable()
    {
        UnregisterCommand();
        StopAllLoopsImmediately();
    }

    private void RegisterCommand()
    {
        if (commandRegistered)
        {
            return;
        }

        if (dialogueRunner == null)
        {
            dialogueRunner = GetComponent<DialogueRunner>();
        }

        if (dialogueRunner == null)
        {
            Debug.LogWarning("SfxController requires a DialogueRunner reference.");
            return;
        }

        dialogueRunner.AddCommandHandler<string[]>("sfx", HandleSfxCommandAsync);
        commandRegistered = true;
    }

    private void UnregisterCommand()
    {
        if (!commandRegistered || dialogueRunner == null)
        {
            return;
        }

        dialogueRunner.RemoveCommandHandler("sfx");
        commandRegistered = false;
    }

    private async YarnTask HandleSfxCommandAsync(params string[] args)
    {
        if (args == null || args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
        {
            Debug.LogWarning("Command <<sfx>> requires an action: play, loop, stop, or stop_all.");
            return;
        }

        string action = args[0].Trim().ToLowerInvariant();
        switch (action)
        {
            case "play":
                PlayOneShot(args);
                break;
            case "loop":
                await StartLoopAsync(args);
                break;
            case "stop":
                await StopLoopAsync(args);
                break;
            case "stop_all":
                await StopAllLoopsAsync(args);
                break;
            default:
                Debug.LogWarning($"Unknown SFX action \"{args[0]}\". Use play, loop, stop, or stop_all.");
                break;
        }
    }

    private void PlayOneShot(string[] args)
    {
        if (oneShotAudioSource == null)
        {
            Debug.LogWarning("SfxController requires an AudioSource.");
            return;
        }

        if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
        {
            Debug.LogWarning("Command <<sfx play>> requires a clip name.");
            return;
        }

        string clipName = args[1].Trim();
        float commandVolume = Mathf.Clamp01(ParseFloat(args, 2, 1f));
        float volume = GetEffectiveVolume(commandVolume);
        AudioClip clip = LoadClip(clipName);
        if (clip == null)
        {
            return;
        }

        oneShotAudioSource.PlayOneShot(clip, volume);
    }

    private async YarnTask StartLoopAsync(string[] args)
    {
        if (args.Length < 3 || string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[2]))
        {
            Debug.LogWarning("Command <<sfx loop>> requires a key and a clip name.");
            return;
        }

        string key = args[1].Trim();
        string clipName = args[2].Trim();
        float fadeDuration = ParseFloat(args, 3, defaultFadeDuration);
        float commandVolume = Mathf.Clamp01(ParseFloat(args, 4, 1f));
        float targetVolume = GetEffectiveVolume(commandVolume);

        AudioClip clip = LoadClip(clipName);
        if (clip == null)
        {
            return;
        }

        LoopState state = GetOrCreateLoopState(key);
        int currentFadeVersion = ++state.FadeVersion;
        state.CommandVolume = commandVolume;

        if (state.AudioSource.clip != clip)
        {
            state.AudioSource.Stop();
            state.AudioSource.clip = clip;
            state.AudioSource.volume = 0f;
        }

        state.AudioSource.loop = true;
        state.AudioSource.playOnAwake = false;
        if (!state.AudioSource.isPlaying)
        {
            state.AudioSource.Play();
        }

        await FadeVolumeAsync(state, targetVolume, fadeDuration, currentFadeVersion);
    }

    private async YarnTask StopLoopAsync(string[] args)
    {
        if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
        {
            Debug.LogWarning("Command <<sfx stop>> requires a loop key.");
            return;
        }

        string key = args[1].Trim();
        float fadeDuration = ParseFloat(args, 2, defaultFadeDuration);

        if (!loopStates.TryGetValue(key, out LoopState state))
        {
            return;
        }

        int currentFadeVersion = ++state.FadeVersion;
        await FadeVolumeAsync(state, 0f, fadeDuration, currentFadeVersion);

        if (currentFadeVersion != state.FadeVersion)
        {
            return;
        }

        state.AudioSource.Stop();
        state.AudioSource.clip = null;
    }

    private async YarnTask StopAllLoopsAsync(string[] args)
    {
        float fadeDuration = ParseFloat(args, 1, defaultFadeDuration);
        var tasks = new List<YarnTask>();

        foreach (LoopState state in loopStates.Values)
        {
            int currentFadeVersion = ++state.FadeVersion;
            tasks.Add(StopLoopStateAsync(state, fadeDuration, currentFadeVersion));
        }

        if (tasks.Count > 0)
        {
            await YarnTask.WhenAll(tasks.ToArray());
        }
    }

    private async YarnTask StopLoopStateAsync(LoopState state, float fadeDuration, int currentFadeVersion)
    {
        await FadeVolumeAsync(state, 0f, fadeDuration, currentFadeVersion);

        if (currentFadeVersion != state.FadeVersion)
        {
            return;
        }

        state.AudioSource.Stop();
        state.AudioSource.clip = null;
    }

    private LoopState GetOrCreateLoopState(string key)
    {
        if (loopStates.TryGetValue(key, out LoopState state))
        {
            return state;
        }

        AudioSource audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;

        state = new LoopState
        {
            AudioSource = audioSource,
            FadeVersion = 0,
        };
        loopStates.Add(key, state);
        return state;
    }

    private AudioClip LoadClip(string clipName)
    {
        AudioClip clip = Resources.Load<AudioClip>(SfxResourcesPath + clipName);
        if (clip == null)
        {
            Debug.LogWarning($"SFX \"{clipName}\" was not found at Resources/{SfxResourcesPath}{clipName}.");
        }

        return clip;
    }

    private float GetEffectiveVolume(float commandVolume)
    {
        return Mathf.Clamp01(commandVolume) * Mathf.Clamp01(defaultVolume);
    }

    private async YarnTask FadeVolumeAsync(LoopState state, float targetVolume, float duration, int currentFadeVersion)
    {
        targetVolume = Mathf.Clamp01(targetVolume);
        duration = Mathf.Max(0f, duration);
        float startVolume = state.AudioSource.volume;

        if (duration <= 0f || Mathf.Approximately(startVolume, targetVolume))
        {
            state.AudioSource.volume = targetVolume;
            return;
        }

        float elapsed = 0f;
        while (currentFadeVersion == state.FadeVersion && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            state.AudioSource.volume = Mathf.Lerp(startVolume, targetVolume, Mathf.SmoothStep(0f, 1f, t));
            await YarnTask.Yield();
        }

        if (currentFadeVersion == state.FadeVersion)
        {
            state.AudioSource.volume = targetVolume;
        }
    }

    private void StopAllLoopsImmediately()
    {
        foreach (LoopState state in loopStates.Values)
        {
            state.FadeVersion++;
            if (state.AudioSource == null)
            {
                continue;
            }

            state.AudioSource.Stop();
            state.AudioSource.clip = null;
        }
    }

    private static float ParseFloat(string[] args, int index, float fallback)
    {
        if (args.Length <= index || string.IsNullOrWhiteSpace(args[index]))
        {
            return fallback;
        }

        if (float.TryParse(args[index], NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
        {
            return value;
        }

        Debug.LogWarning($"Could not parse SFX number \"{args[index]}\"; using {fallback.ToString(CultureInfo.InvariantCulture)}.");
        return fallback;
    }
}
