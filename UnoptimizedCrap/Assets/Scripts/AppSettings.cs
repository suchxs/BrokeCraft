using UnityEngine;

/// <summary>
/// Centralized app settings persisted across scenes.
/// </summary>
public static class AppSettings
{
    private const string PrefAudio = "AppSettings_AudioVolume";
    private const string PrefResWidth = "AppSettings_ResWidth";
    private const string PrefResHeight = "AppSettings_ResHeight";
    private const string PrefHoriz = "AppSettings_HView";
    private const string PrefVert = "AppSettings_VView";

    public static float AudioVolume { get; private set; } = 0.45f;
    public static int ResolutionWidth { get; private set; } = 0;
    public static int ResolutionHeight { get; private set; } = 0;
    public static int HorizontalViewDistance { get; private set; } = 32;
    public static int VerticalViewDistance { get; private set; } = 20;

    public static void Load()
    {
        AudioVolume = PlayerPrefs.GetFloat(PrefAudio, AudioVolume);
        ResolutionWidth = PlayerPrefs.GetInt(PrefResWidth, ResolutionWidth);
        ResolutionHeight = PlayerPrefs.GetInt(PrefResHeight, ResolutionHeight);
        HorizontalViewDistance = PlayerPrefs.GetInt(PrefHoriz, HorizontalViewDistance);
        VerticalViewDistance = PlayerPrefs.GetInt(PrefVert, VerticalViewDistance);
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat(PrefAudio, AudioVolume);
        PlayerPrefs.SetInt(PrefResWidth, ResolutionWidth);
        PlayerPrefs.SetInt(PrefResHeight, ResolutionHeight);
        PlayerPrefs.SetInt(PrefHoriz, HorizontalViewDistance);
        PlayerPrefs.SetInt(PrefVert, VerticalViewDistance);
        PlayerPrefs.Save();
    }

    public static void SetAudioVolume(float volume)
    {
        AudioVolume = Mathf.Clamp01(volume);
    }

    public static void SetResolution(int width, int height)
    {
        ResolutionWidth = width;
        ResolutionHeight = height;
    }

    public static void SetViewDistances(int horizontal, int vertical)
    {
        HorizontalViewDistance = Mathf.Max(2, horizontal);
        VerticalViewDistance = Mathf.Max(2, vertical);
    }

    public static void ApplyToWorld(World world)
    {
        if (world == null)
        {
            return;
        }

        if (HorizontalViewDistance > 0)
        {
            world.horizontalViewDistance = HorizontalViewDistance;
        }

        if (VerticalViewDistance > 0)
        {
            world.verticalViewDistance = VerticalViewDistance;
        }
    }
}
