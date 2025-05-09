using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR;
using UnityEngine.Video;
using System.IO;
using System.Globalization;
using UnityEngine.UI;

public class VRSystemInfoSender : MonoBehaviour
{
    
    public Material videoMaterial;
    private Texture freezeFrameTexture;
    private float nextFakeFreezeTime = -1f;
    private float fakeFreezeDuration = 0f;
    private bool isInFakeFreeze = false;
    private List<VRData> dataSnapshots = new List<VRData>();

    public RenderTexture targetTexture;
    private VideoPlayer player;

    private long lastFrame = -1;
    private float freezeTimer = 0f;
    private bool isFreezing = false;
    private int bufferingCount = 0;
    private float videoStartTime = 0f;
    private float videoStartDelay = -1f;
    private float networkLatency = -1f;

    [System.Serializable]
    public class FreezeInfo
    {
        public float start;
        public float end;
        public float duration;
    }

    private List<FreezeInfo> freezeEvents = new List<FreezeInfo>();
    private float currentFreezeStart = -1f;
    private Texture2D freezeTexture;

    IEnumerator SimulateFreeze(float duration)
    {
        RenderTexture currentRT = RenderTexture.active;
        RenderTexture.active = player.targetTexture;

        Texture2D freezeTexture = new Texture2D(player.targetTexture.width, player.targetTexture.height, TextureFormat.RGB24, false);
        freezeTexture.ReadPixels(new Rect(0, 0, player.targetTexture.width, player.targetTexture.height), 0, 0);
        freezeTexture.Apply();

        RenderTexture.active = currentRT;

        freezeFrameTexture = freezeTexture;

        videoMaterial.mainTexture = freezeFrameTexture;
        player.Pause();

        float endTime = Time.time + duration;
        while (Time.time < endTime)
        {
            yield return null;
        }

        player.Play();
        videoMaterial.mainTexture = player.targetTexture;

        isInFakeFreeze = false;
        ScheduleNextFakeFreeze();
    }


    void OnVideoEnd(VideoPlayer vp)
    {
        StartCoroutine(SaveDataToCSVAndSend());
    }
    IEnumerator SaveDataToCSVAndSend()
    {
        string filePath = Path.Combine(Application.persistentDataPath, "qoe_data.csv");

        List<string> lines = new List<string>();
        lines.Add("videoTime;deviceName;eyeResolution;fov;targetFramerate;freezeTime;videoStartDelay;bufferingCount;networkLatency;videoResolution;videoLength;videoFrameRate;videoFrameCount;videoFinalFrame;deviceModel;freezes");
        foreach (var d in dataSnapshots)
        {
            string freezesSerialized = "";
            if (d.freezes != null && d.freezes.Count > 0)
            {
                List<string> freezeParts = new List<string>();
                foreach (var f in d.freezes)
                    freezeParts.Add($"[{f.start:F2}-{f.end:F2}:{f.duration:F2}s]");
                freezesSerialized = string.Join(" | ", freezeParts);
            }

            lines.Add(string.Join(";",
                d.videoTime.ToString("F2", CultureInfo.InvariantCulture),
                d.deviceName,
                d.eyeResolution,
                d.fov,
                d.targetFramerate,
                d.freezeTime.Replace("s", "").Trim(),
                d.videoStartDelay.ToString("F2"),
                d.bufferingCount,
                d.networkLatency.Replace("ms", "").Trim(),
                d.videoResolution,
                d.videoLength.ToString("F2"),
                d.videoFrameRate.ToString("F2"),
                d.videoFrameCount,
                d.videoFinalFrame,
        #if UNITY_ANDROID
                d.deviceModel,
        #else
                "\"\"", 
        #endif
                "\"" + freezesSerialized + "\"" 
            ));
        }


        File.WriteAllLines(filePath, lines);
        Debug.Log("✅ CSV saved to: " + filePath);

        yield return StartCoroutine(SendCSVToServer(filePath));
    }


    IEnumerator SendCSVToServer(string filePath)
    {
        byte[] fileData = File.ReadAllBytes(filePath);
        UnityWebRequest www = new UnityWebRequest("http://192.168.0.123:8888", UnityWebRequest.kHttpVerbPOST);
        www.uploadHandler = new UploadHandlerRaw(fileData);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "text/csv");
        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
            Debug.Log("✅ CSV send !");
        else
            Debug.LogError("❌ Error in sending the CSV : " + www.error);
    }

    void Start()
    {
        player = gameObject.AddComponent<VideoPlayer>();
        player.source = VideoSource.Url;
        player.url = "https://github.com/TrotiRemi/E4_Intership/raw/refs/heads/main/Assets/VRTemplateAssets/Videos/CaptainJack.mp4";
        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = targetTexture;
        player.playOnAwake = true;
        player.isLooping = false;
        player.Prepare();
        player.loopPointReached += OnVideoEnd;

        player.prepareCompleted += (vp) =>
        {
            videoStartDelay = Time.time - videoStartTime;
            vp.Play();
        };

        player.seekCompleted += (_) => bufferingCount++;

        InvokeRepeating(nameof(CaptureDataSnapshot), 1f, 1f); 

        StartCoroutine(MeasureNetworkLatency());
        videoStartTime = Time.time;
        ScheduleNextFakeFreeze();
    }

    void ScheduleNextFakeFreeze()
    {
        nextFakeFreezeTime = Time.time + UnityEngine.Random.Range(5f, 15f);
        fakeFreezeDuration = UnityEngine.Random.Range(2f, 4f);
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

    void SendLiveDataAsText()
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

        freezeEvents.Clear();

        string json = JsonUtility.ToJson(data, true);
        StartCoroutine(SendDataToPC(json));
    }

    IEnumerator MeasureNetworkLatency()
    {
        UnityWebRequest req = UnityWebRequest.Head(player.url);
        float startTime = Time.realtimeSinceStartup;
        yield return req.SendWebRequest();
        float endTime = Time.realtimeSinceStartup;

        if (req.result == UnityWebRequest.Result.Success)
            networkLatency = (endTime - startTime) * 1000f;
    }

    IEnumerator SendDataToPC(string message)
    {
        string url = "http://192.168.0.123:8888";
        UnityWebRequest req = UnityWebRequest.Put(url, message);
        req.method = UnityWebRequest.kHttpVerbPOST;
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            Debug.Log("✅ Data sent");
        else
            Debug.Log("❌ Error : " + req.error);
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
        freezeEvents.Clear();
        dataSnapshots.Add(data);
    }

}
