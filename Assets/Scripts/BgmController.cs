using System;
using System.Globalization;
using UnityEngine;
using Yarn.Unity;

public class BgmController : MonoBehaviour
{
    private const string BgmResourcesPath = "BGM/";

    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private float defaultFadeDuration = 0.5f;
    [SerializeField] private float defaultVolume = 1f;

    private bool commandRegistered;
    private int fadeVersion;

    private void Awake()
    {
        if (dialogueRunner == null)
        {
            dialogueRunner = GetComponent<DialogueRunner>();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = true;
    }

    private void OnEnable()
    {
        RegisterCommand();
    }

    private void OnDisable()
    {
        UnregisterCommand();
        fadeVersion++;
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
            Debug.LogWarning("BgmController requires a DialogueRunner reference.");
            return;
        }

        dialogueRunner.AddCommandHandler<string[]>("bgm", HandleBgmCommandAsync);
        commandRegistered = true;
    }

    private void UnregisterCommand()
    {
        if (!commandRegistered || dialogueRunner == null)
        {
            return;
        }

        dialogueRunner.RemoveCommandHandler("bgm");
        commandRegistered = false;
    }

    private async YarnTask HandleBgmCommandAsync(params string[] args)
    {
        if (audioSource == null)
        {
            Debug.LogWarning("BgmController requires an AudioSource.");
            return;
        }

        if (args == null || args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
        {
            Debug.LogWarning("Command <<bgm>> requires an action: play, volume, or stop.");
            return;
        }

        string action = args[0].Trim().ToLowerInvariant();
        switch (action)
        {
            case "play":
                await PlayAsync(args);
                break;
            case "volume":
                await SetVolumeAsync(args);
                break;
            case "stop":
                await StopAsync(args);
                break;
            default:
                Debug.LogWarning($"Unknown BGM action \"{args[0]}\". Use play, volume, or stop.");
                break;
        }
    }

    private async YarnTask PlayAsync(string[] args)
    {
        if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
        {
            Debug.LogWarning("Command <<bgm play>> requires a clip name.");
            return;
        }

        string clipName = args[1].Trim();
        float fadeDuration = ParseFloat(args, 2, defaultFadeDuration);
        float targetVolume = Mathf.Clamp01(ParseFloat(args, 3, defaultVolume));

        AudioClip clip = Resources.Load<AudioClip>(BgmResourcesPath + clipName);
        if (clip == null)
        {
            Debug.LogWarning($"BGM \"{clipName}\" was not found at Resources/{BgmResourcesPath}{clipName}.");
            return;
        }

        if (audioSource.clip != clip)
        {
            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.volume = 0f;
        }

        audioSource.loop = true;
        if (!audioSource.isPlaying)
        {
            audioSource.Play();
        }

        await FadeVolumeAsync(targetVolume, fadeDuration, ++fadeVersion);
    }

    private async YarnTask SetVolumeAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Debug.LogWarning("Command <<bgm volume>> requires a target volume.");
            return;
        }

        float targetVolume = Mathf.Clamp01(ParseFloat(args, 1, audioSource.volume));
        float fadeDuration = ParseFloat(args, 2, defaultFadeDuration);
        await FadeVolumeAsync(targetVolume, fadeDuration, ++fadeVersion);
    }

    private async YarnTask StopAsync(string[] args)
    {
        float fadeDuration = ParseFloat(args, 1, defaultFadeDuration);
        int currentFadeVersion = ++fadeVersion;

        await FadeVolumeAsync(0f, fadeDuration, currentFadeVersion);

        if (currentFadeVersion != fadeVersion)
        {
            return;
        }

        audioSource.Stop();
        audioSource.clip = null;
    }

    private async YarnTask FadeVolumeAsync(float targetVolume, float duration, int currentFadeVersion)
    {
        targetVolume = Mathf.Clamp01(targetVolume);
        duration = Mathf.Max(0f, duration);
        float startVolume = audioSource.volume;

        if (duration <= 0f || Mathf.Approximately(startVolume, targetVolume))
        {
            audioSource.volume = targetVolume;
            return;
        }

        float elapsed = 0f;
        while (currentFadeVersion == fadeVersion && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            audioSource.volume = Mathf.Lerp(startVolume, targetVolume, Mathf.SmoothStep(0f, 1f, t));
            await YarnTask.Yield();
        }

        if (currentFadeVersion == fadeVersion)
        {
            audioSource.volume = targetVolume;
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

        Debug.LogWarning($"Could not parse BGM number \"{args[index]}\"; using {fallback.ToString(CultureInfo.InvariantCulture)}.");
        return fallback;
    }
}
