using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR;
using UnityEngine.Video;
using System.Collections;
using System.Collections.Generic;

public class VRSystemInfoSender : MonoBehaviour
{
    public RenderTexture targetTexture;
    private VideoPlayer player;

    private long lastFrame = -1;
    private float freezeTimer = 0f;
    private bool hasSentJson = false;
    private int bufferingCount = 0;
    private float videoStartTime = 0f;
    private float videoStartDelay = -1f;
    private float networkLatency = -1f;

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

        player.prepareCompleted += (vp) =>
        {
            videoStartDelay = Time.time - videoStartTime;
            vp.Play();
        };

        player.seekCompleted += (_) => bufferingCount++;

        // Envoi régulier (3s)
        InvokeRepeating(nameof(SendLiveDataAsText), 3f, 3f);

        // Mesure de latence réseau
        StartCoroutine(MeasureNetworkLatency());

        // Démarrage du chrono pour startup delay
        videoStartTime = Time.time;
    }

    void Update()
    {
        if (player.isPrepared && player.isPlaying)
        {
            if (player.frame == lastFrame)
                freezeTimer += Time.deltaTime;
            else
            {
                freezeTimer = 0f;
                lastFrame = player.frame;
            }

            if (freezeTimer > 2f)
                Debug.LogWarning("⚠️ La vidéo semble bloquée !");
        }

        if (player.isPrepared && !player.isPlaying && !hasSentJson)
        {
            Debug.Log("📤 Vidéo stoppée → envoi du JSON");
            hasSentJson = true;
            SendFinalJson();
        }
    }

    void SendLiveDataAsText()
    {
        if (hasSentJson) return;

        string info = "== XR Infos ==\n";
        info += "Nom du casque : " + XRSettings.loadedDeviceName + "\n";
        info += "Résolution œil : " + XRSettings.eyeTextureWidth + "x" + XRSettings.eyeTextureHeight + "\n";
        info += "FOV caméra : " + Camera.main.fieldOfView + "\n";
        info += "Framerate cible : " + Application.targetFrameRate + "\n";
#if UNITY_ANDROID
        info += "Modèle appareil : " + SystemInfo.deviceModel + "\n";
#endif

        info += "\n== Vidéo ==\n";
        info += "URL vidéo : " + player.url + "\n";
        info += "Résolution vidéo : " + player.texture?.width + "x" + player.texture?.height + "\n";
        info += "Durée totale (s) : " + player.length.ToString("F2") + "\n";
        info += "Temps actuel (s) : " + player.time.ToString("F2") + "\n";
        info += "Frame actuelle : " + player.frame + "\n";
        info += "Frame total estimé : " + player.frameCount + "\n";
        info += "Framerate vidéo (attendu) : " + player.frameRate + "\n";
        info += "Is Playing : " + player.isPlaying + "\n";
        info += "Is Prepared : " + player.isPrepared + "\n";
        info += "Looping : " + player.isLooping + "\n";
        info += "Freeze time détecté : " + freezeTimer.ToString("F1") + "s\n";
        info += "Start delay : " + videoStartDelay.ToString("F2") + "s\n";
        info += "Buffering count : " + bufferingCount + "\n";
        info += "Network latency : " + networkLatency.ToString("F1") + " ms\n";

        StartCoroutine(SendDataToPC(info));
    }

    void SendFinalJson()
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["deviceName"] = XRSettings.loadedDeviceName,
            ["eyeResolution"] = XRSettings.eyeTextureWidth + "x" + XRSettings.eyeTextureHeight,
            ["fov"] = Camera.main.fieldOfView,
            ["targetFramerate"] = Application.targetFrameRate,
            ["videoUrl"] = player.url,
            ["videoResolution"] = player.texture?.width + "x" + player.texture?.height,
            ["videoLength"] = player.length,
            ["videoTime"] = player.time,
            ["videoFrameRate"] = player.frameRate,
            ["videoFrameCount"] = player.frameCount,
            ["videoFinalFrame"] = player.frame,
            ["freezeTime"] = freezeTimer.ToString("F1") + "s",
            ["videoStartDelay"] = videoStartDelay.ToString("F2") + "s",
            ["bufferingCount"] = bufferingCount,
            ["networkLatency"] = networkLatency.ToString("F1") + " ms",
#if UNITY_ANDROID
            ["deviceModel"] = SystemInfo.deviceModel,
#endif
        };

        string json = JsonUtility.ToJson(new SerializableWrapper(data));
        StartCoroutine(SendDataToPC(json));
    }

    IEnumerator MeasureNetworkLatency()
    {
        UnityWebRequest req = UnityWebRequest.Head("https://drive.google.com/file/d/1YOBLOw2OL2m5cXuNA6AP16vp9Hxudx-d/view?usp=sharing");
        float startTime = Time.realtimeSinceStartup;
        yield return req.SendWebRequest();
        float endTime = Time.realtimeSinceStartup;

        if (req.result == UnityWebRequest.Result.Success)
            networkLatency = (endTime - startTime) * 1000f; // ms
        else
            Debug.LogWarning("⚠️ Latency check failed: " + req.error);
    }

    IEnumerator SendDataToPC(string message)
    {
        string url = "http://192.168.0.123:8888";
        UnityWebRequest req = UnityWebRequest.Put(url, message);
        req.method = UnityWebRequest.kHttpVerbPOST;
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            Debug.Log("✅ Données envoyées");
        else
            Debug.Log("❌ Erreur : " + req.error);
    }

    [System.Serializable]
    public class SerializableWrapper
    {
        public List<KeyValue> entries = new List<KeyValue>();
        public SerializableWrapper(Dictionary<string, object> dict)
        {
            foreach (var kvp in dict)
                entries.Add(new KeyValue { key = kvp.Key, value = kvp.Value.ToString() });
        }
    }

    [System.Serializable]
    public class KeyValue
    {
        public string key;
        public string value;
    }
}
