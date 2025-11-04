using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class WeatherVideoBackgroundController : MonoBehaviour
{
    [Header("RawImages (one per weather)")]
    public RawImage clearImage;
    public RawImage droughtImage;
    public RawImage wildfireImage;
    public RawImage stormImage;

    [Header("VideoPlayers (one per weather)")]
    public VideoPlayer clearPlayer;
    public VideoPlayer droughtPlayer;
    public VideoPlayer wildfirePlayer;
    public VideoPlayer stormPlayer;

    [Header("Playback")] 
    public bool playOnStart = true;
    public bool loopVideos = true;

	[Header("Camera Overlay (no UI path)")]
	public Camera targetCamera; // If set, fades use VideoPlayer.targetCameraAlpha

    private WeatherType currentType = WeatherType.Clear;

    void Awake()
    {
        SetupPlayback(clearPlayer);
        SetupPlayback(droughtPlayer);
        SetupPlayback(wildfirePlayer);
        SetupPlayback(stormPlayer);
		// Hide until the first frame of Clear is ready
		SetAlpha(clearImage, 0f);
		SetAlpha(droughtImage, 0f);
		SetAlpha(wildfireImage, 0f);
		SetAlpha(stormImage, 0f);
		SetActive(clearImage, false);
		SetActive(droughtImage, false);
		SetActive(wildfireImage, false);
		SetActive(stormImage, false);

		// If using camera overlay, initialize players to render to camera with alpha 0
		if (targetCamera != null)
		{
			SetupCameraOverlay(clearPlayer);
			SetupCameraOverlay(droughtPlayer);
			SetupCameraOverlay(wildfirePlayer);
			SetupCameraOverlay(stormPlayer);
		}
    }

	void Start()
	{
		StartCoroutine(InitializeAndShowClear());
	}

	IEnumerator InitializeAndShowClear()
	{
		// Prepare all players to avoid a flash of uninitialized RenderTextures
		SafePrepare(clearPlayer);
		SafePrepare(droughtPlayer);
		SafePrepare(wildfirePlayer);
		SafePrepare(stormPlayer);

		// Wait for the Clear video to be ready
		while (clearPlayer != null && !clearPlayer.isPrepared)
		{
			yield return null;
		}
		// Force first frame into the RenderTexture, then reveal
		if (clearPlayer != null)
		{
			clearPlayer.Play();
			clearPlayer.Pause();
		}
		ForceTo(WeatherType.Clear);
		currentType = WeatherType.Clear;

		if (playOnStart)
		{
			// Start all players so their RenderTextures are continuously updated
			SafePlay(clearPlayer);
			SafePlay(droughtPlayer);
			SafePlay(wildfirePlayer);
			SafePlay(stormPlayer);
		}
	}

    static void SetupPlayback(VideoPlayer vp)
    {
        if (vp == null) return;
        vp.isLooping = true;
        // If audio tracks exist but you don't want them, mute
        vp.SetDirectAudioMute(0, true);
    }

    static void SafePlay(VideoPlayer vp)
    {
        if (vp == null) return;
        if (!vp.isPrepared)
        {
            vp.Prepare();
        }
        vp.Play();
    }

	static void SafePrepare(VideoPlayer vp)
	{
		if (vp == null) return;
		if (!vp.isPrepared) vp.Prepare();
	}

	public void ForceTo(WeatherType type)
    {
        currentType = type;
		if (targetCamera != null)
		{
			// Camera overlay: use player camera alpha
			SetCameraAlpha(clearPlayer, type == WeatherType.Clear ? 1f : 0f);
			SetCameraAlpha(droughtPlayer, type == WeatherType.Drought ? 1f : 0f);
			SetCameraAlpha(wildfirePlayer, type == WeatherType.Wildfire ? 1f : 0f);
			SetCameraAlpha(stormPlayer, type == WeatherType.Storm ? 1f : 0f);
			SafePlay(clearPlayer);
			SafePlay(droughtPlayer);
			SafePlay(wildfirePlayer);
			SafePlay(stormPlayer);
		}
		else
		{
			// UI path: use RawImages
			SetAlpha(clearImage, type == WeatherType.Clear ? 1f : 0f);
			SetAlpha(droughtImage, type == WeatherType.Drought ? 1f : 0f);
			SetAlpha(wildfireImage, type == WeatherType.Wildfire ? 1f : 0f);
			SetAlpha(stormImage, type == WeatherType.Storm ? 1f : 0f);
			SetActive(clearImage, type == WeatherType.Clear);
			SetActive(droughtImage, type == WeatherType.Drought);
			SetActive(wildfireImage, type == WeatherType.Wildfire);
			SetActive(stormImage, type == WeatherType.Storm);
		}
    }

	public IEnumerator CrossfadeTo(WeatherType target, float duration = 0.7f)
    {
        if (target == currentType)
        {
            yield break;
        }

		Debug.Log($"[WeatherVideoBG] Crossfade {currentType} -> {target} (UI path: {targetCamera == null})");

		// Prepare target first frame
		var toPlayer = GetPlayer(target);
		if (toPlayer != null && !toPlayer.isPrepared)
		{
			toPlayer.Prepare();
			while (!toPlayer.isPrepared) { yield return null; }
			toPlayer.Play();
			toPlayer.Pause();
		}
		SafePlay(toPlayer);

		if (targetCamera != null)
		{
			var fromPlayer = GetPlayer(currentType);
			float startFrom = GetCameraAlpha(fromPlayer);
			float startTo = GetCameraAlpha(toPlayer);
			// Ensure draw order: target on NearPlane, source on FarPlane during blend
			SetPlane(fromPlayer, VideoRenderMode.CameraFarPlane);
			SetPlane(toPlayer, VideoRenderMode.CameraNearPlane);
			float t = 0f;
			while (t < duration)
			{
				t += Time.deltaTime;
				float u = Mathf.Clamp01(t / duration);
				SetCameraAlpha(fromPlayer, Mathf.Lerp(startFrom, 0f, u));
				SetCameraAlpha(toPlayer, Mathf.Lerp(startTo, 1f, u));
				yield return null;
			}
			SetCameraAlpha(fromPlayer, 0f);
			SetCameraAlpha(toPlayer, 1f);
			// Optionally pause the previous video to save CPU
			SafePause(fromPlayer);
			// Keep target visible; previous remains on FarPlane with alpha 0
		}
		else
		{
			RawImage from = GetImage(currentType);
			RawImage to = GetImage(target);
			SetActive(to, true);
			float t = 0f;
			float startFrom = GetAlpha(from);
			float startTo = GetAlpha(to);
			while (t < duration)
			{
				t += Time.deltaTime;
				float u = Mathf.Clamp01(t / duration);
				if (from != null) SetAlpha(from, Mathf.Lerp(startFrom, 0f, u));
				if (to != null) SetAlpha(to, Mathf.Lerp(startTo, 1f, u));
				yield return null;
			}
			if (from != null) SetAlpha(from, 0f);
			if (to != null) SetAlpha(to, 1f);
			SetActive(from, false);
		}
		currentType = target;
		Debug.Log($"[WeatherVideoBG] Now active: {currentType}");
    }

    RawImage GetImage(WeatherType type)
    {
        switch (type)
        {
            case WeatherType.Clear: return clearImage;
            case WeatherType.Drought: return droughtImage;
            case WeatherType.Wildfire: return wildfireImage;
            case WeatherType.Storm: return stormImage;
        }
        return null;
    }

    VideoPlayer GetPlayer(WeatherType type)
    {
        switch (type)
        {
            case WeatherType.Clear: return clearPlayer;
            case WeatherType.Drought: return droughtPlayer;
            case WeatherType.Wildfire: return wildfirePlayer;
            case WeatherType.Storm: return stormPlayer;
        }
        return null;
    }

    static void SetAlpha(RawImage img, float a)
    {
        if (img == null) return;
        var c = img.color;
        c.a = Mathf.Clamp01(a);
        img.color = c;
    }

    static float GetAlpha(RawImage img)
    {
        if (img == null) return 0f;
        return img.color.a;
    }

    static void SetActive(Behaviour b, bool active)
    {
        if (b == null) return;
        if (b.gameObject != null) b.gameObject.SetActive(active);
        b.enabled = active;
    }

	void SetupCameraOverlay(VideoPlayer vp)
	{
		if (vp == null || targetCamera == null) return;
		vp.renderMode = VideoRenderMode.CameraFarPlane;
		vp.targetCamera = targetCamera;
		vp.targetCameraAlpha = 0f;
	}

	void SetPlane(VideoPlayer vp, VideoRenderMode mode)
	{
		if (vp == null || targetCamera == null) return;
		vp.renderMode = mode;
		vp.targetCamera = targetCamera;
	}

	static void SetCameraAlpha(VideoPlayer vp, float a)
	{
		if (vp == null) return;
		vp.targetCameraAlpha = Mathf.Clamp01(a);
	}

	static float GetCameraAlpha(VideoPlayer vp)
	{
		if (vp == null) return 0f;
		return vp.targetCameraAlpha;
	}

	static void SafePause(VideoPlayer vp)
	{
		if (vp == null) return;
		if (vp.isPlaying) vp.Pause();
	}
}


