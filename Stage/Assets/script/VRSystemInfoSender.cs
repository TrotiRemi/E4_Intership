using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR;
using UnityEngine.Video;
using System.IO;
using System.Globalization;

public class VRSystemInfoSender : MonoBehaviour
{
    public Material videoMaterial;
    public RenderTexture targetTexture;
    private Texture freezeFrameTexture;

    private VideoPlayer player;
    private long lastFrame = -1;
    private float freezeTimer = 0f;
    private bool isFreezing = false;
    private float videoStartTime = 0f;
    private float videoStartDelay = -1f;
    private float networkLatency = -1f;
    private int bufferingCount = 0;

    private float nextFakeFreezeTime = -1f;
    private float fakeFreezeDuration = 0f;
    private bool isInFakeFreeze = false;

    [System.Serializable]
    public class FreezeInfo
    {
        public float start;
        public float end;
        public float duration;
    }

    [System.Serializable]
    public class VRData
    {
        public string deviceName;
        public string eyeResolution;
        public float fov;
        public int targetFramerate;
        public string videoUrl;
        public string videoResolution;
        public float videoLength;
        public float videoTime;
        public float videoFrameRate;
        public ulong videoFrameCount;
        public ulong videoFinalFrame;
        public string freezeTime;
        public float videoStartDelay;
        public int bufferingCount;
        public string networkLatency;
#if UNITY_ANDROID
        public string deviceModel;
#endif
        public List<FreezeInfo> freezes;
    }

    private List<FreezeInfo> freezeEvents = new List<FreezeInfo>();
    private List<VRData> dataSnapshots = new List<VRData>();
    private float currentFreezeStart = -1f;

    void Start()
    {
        player = gameObject.AddComponent<VideoPlayer>();
        player.source = VideoSource.Url;
        player.url = "https://dl.dropboxusercontent.com/scl/fi/hvejtlgdy3xk9363i0a9o/CaptainJack.mp4?rlkey=z7p3iv1st9hcddgj827jnozzd&st=n80ipg88";
        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = targetTexture;
        player.playOnAwake = true;
        player.isLooping = false;
        player.prepareCompleted += OnVideoReady;
        player.loopPointReached += OnVideoEnd;
        player.seekCompleted += (_) => bufferingCount++;
        player.errorReceived += (v, err) => Debug.LogError("Video Error: " + err);

        videoStartTime = Time.time;

        InvokeRepeating(nameof(CaptureDataSnapshot), 1f, 1f);
        StartCoroutine(MeasureNetworkLatency());
        player.Prepare();

        ScheduleNextFakeFreeze();
    }

    void OnVideoReady(VideoPlayer vp)
    {
        videoStartDelay = Time.time - videoStartTime;
        vp.Play();
    }

    void OnVideoEnd(VideoPlayer vp)
    {
        StartCoroutine(SaveDataToCSVAndSend());
    }

    void Update()
    {
        if (Time.time >= nextFakeFreezeTime && !isInFakeFreeze)
        {
            isInFakeFreeze = true;
            StartCoroutine(SimulateFreeze(fakeFreezeDuration));
        }

        if (player.isPrepared && (player.isPlaying || isInFakeFreeze))
        {
            if (player.frame == lastFrame)
            {
                freezeTimer += Time.deltaTime;

                if (!isFreezing && freezeTimer > 0.5f)
                {
                    isFreezing = true;
                    currentFreezeStart = (float)player.time;
                }
            }
            else
            {
                if (isFreezing)
                {
                    float freezeEnd = currentFreezeStart + freezeTimer;
                    freezeEvents.Add(new FreezeInfo
                    {
                        start = currentFreezeStart,
                        end = freezeEnd,
                        duration = freezeEnd - currentFreezeStart
                    });
                    isFreezing = false;
                }

                freezeTimer = 0f;
                lastFrame = player.frame;
            }
        }
    }

    void CaptureDataSnapshot()
    {
        var data = new VRData
        {
            deviceName = XRSettings.loadedDeviceName,
            eyeResolution = XRSettings.eyeTextureWidth + "x" + XRSettings.eyeTextureHeight,
            fov = Camera.main.fieldOfView,
            targetFramerate = Application.targetFrameRate,
            videoUrl = player.url,
            videoResolution = player.texture?.width + "x" + player.texture?.height,
            videoLength = (float)player.length,
            videoTime = (float)player.time,
            videoFrameRate = (float)player.frameRate,
            videoFrameCount = player.frameCount,
            videoFinalFrame = (ulong)player.frame,
            freezeTime = freezeTimer.ToString("F1") + "s",
            videoStartDelay = videoStartDelay,
            bufferingCount = bufferingCount,
            networkLatency = networkLatency.ToString("F1") + " ms",
            freezes = new List<FreezeInfo>(freezeEvents)
#if UNITY_ANDROID
            , deviceModel = SystemInfo.deviceModel
#endif
        };

        dataSnapshots.Add(data);
        freezeEvents.Clear();
    }

    IEnumerator SaveDataToCSVAndSend()
    {
        string path = Path.Combine(Application.persistentDataPath, "qoe_data.csv");
        List<string> lines = new List<string>
        {
            "videoTime;deviceName;eyeResolution;fov;targetFramerate;freezeTime;videoStartDelay;bufferingCount;networkLatency;videoResolution;videoLength;videoFrameRate;videoFrameCount;videoFinalFrame;deviceModel;freezes"
        };

        foreach (var d in dataSnapshots)
        {
            string freezeSummary = "";
            if (d.freezes != null && d.freezes.Count > 0)
            {
                List<string> parts = new List<string>();
                foreach (var f in d.freezes)
                    parts.Add($"[{f.start:F2}-{f.end:F2}:{f.duration:F2}s]");
                freezeSummary = string.Join(" | ", parts);
            }

            lines.Add(string.Join(";",
                d.videoTime.ToString("F2", CultureInfo.InvariantCulture),
                d.deviceName,
                d.eyeResolution,
                d.fov.ToString(CultureInfo.InvariantCulture),
                d.targetFramerate,
                d.freezeTime.Replace("s", ""),
                d.videoStartDelay.ToString("F2", CultureInfo.InvariantCulture),
                d.bufferingCount,
                d.networkLatency.Replace("ms", ""),
                d.videoResolution,
                d.videoLength.ToString("F2", CultureInfo.InvariantCulture),
                d.videoFrameRate.ToString("F2", CultureInfo.InvariantCulture),
                d.videoFrameCount,
                d.videoFinalFrame,
#if UNITY_ANDROID
                d.deviceModel,
#else
                "\"\"",
#endif
                "\"" + freezeSummary + "\""
            ));
        }

        File.WriteAllLines(path, lines);
        Debug.Log("✅ CSV saved at: " + path);
        yield return StartCoroutine(SendCSVToServer(path));
    }

    IEnumerator SimulateFreeze(float duration)
    {
        RenderTexture currentRT = RenderTexture.active;
        RenderTexture.active = player.targetTexture;

        Texture2D freezeTex = new Texture2D(player.targetTexture.width, player.targetTexture.height, TextureFormat.RGB24, false);
        freezeTex.ReadPixels(new Rect(0, 0, player.targetTexture.width, player.targetTexture.height), 0, 0);
        freezeTex.Apply();

        RenderTexture.active = currentRT;

        freezeFrameTexture = freezeTex;
        videoMaterial.mainTexture = freezeFrameTexture;
        player.Pause();

        float endTime = Time.time + duration;
        while (Time.time < endTime)
            yield return null;

        player.Play();
        videoMaterial.mainTexture = player.targetTexture;
        isInFakeFreeze = false;
        ScheduleNextFakeFreeze();
    }

    void ScheduleNextFakeFreeze()
    {
        nextFakeFreezeTime = Time.time + UnityEngine.Random.Range(5f, 15f);
        fakeFreezeDuration = UnityEngine.Random.Range(2f, 4f);
    }

    IEnumerator MeasureNetworkLatency()
    {
        UnityWebRequest req = UnityWebRequest.Head(player.url);
        float start = Time.realtimeSinceStartup;
        yield return req.SendWebRequest();
        float end = Time.realtimeSinceStartup;

        if (req.result == UnityWebRequest.Result.Success)
            networkLatency = (end - start) * 1000f;
    }

    IEnumerator SendCSVToServer(string filePath)
    {
        byte[] bytes = File.ReadAllBytes(filePath);
        UnityWebRequest req = new UnityWebRequest("http://192.168.0.123:8888", UnityWebRequest.kHttpVerbPOST);
        req.uploadHandler = new UploadHandlerRaw(bytes);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "text/csv");
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            Debug.Log("✅ CSV sent to server");
        else
            Debug.LogError("❌ CSV send error: " + req.error);
    }

    IEnumerator SendDataToPC(string message)
    {
        UnityWebRequest req = UnityWebRequest.Put("http://192.168.0.123:8888", message);
        req.method = UnityWebRequest.kHttpVerbPOST;
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            Debug.Log("✅ JSON sent");
        else
            Debug.Log("❌ JSON error: " + req.error);
    }
}
