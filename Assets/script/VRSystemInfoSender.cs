using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR;
using UnityEngine.Video;
using System.Collections;

public class VRSystemInfoSender : MonoBehaviour
{
    public RenderTexture targetTexture;
    private VideoPlayer player;

    private long lastFrame = -1;
    private float freezeTimer = 0f;

    void Start()
    {
        // Vidéo
        player = gameObject.AddComponent<VideoPlayer>();
        player.source = VideoSource.Url;
        player.url = "https://github.com/TrotiRemi/E4_Intership/raw/refs/heads/main/Assets/VRTemplateAssets/Videos/CaptainJack.mp4";
        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = targetTexture;
        player.playOnAwake = true;
        player.isLooping = true;
        player.Prepare();
        player.prepareCompleted += (vp) => vp.Play();

        // Démarre l'envoi après 5s
        InvokeRepeating(nameof(CheckVideoAndSendInfo), 5f, 10f);
    }

    void Update()
    {
        if (player.isPrepared && player.isPlaying)
        {
            if (player.frame == lastFrame)
            {
                freezeTimer += Time.deltaTime;
            }
            else
            {
                freezeTimer = 0f;
                lastFrame = player.frame;
            }

            if (freezeTimer > 2f)
            {
                Debug.LogWarning("⚠️ La vidéo semble bloquée !");
            }
        }
    }

    void CheckVideoAndSendInfo()
    {
        string info = "Nom du casque : " + XRSettings.loadedDeviceName + "\n" +
                      "Résolution œil : " + XRSettings.eyeTextureWidth + "x" + XRSettings.eyeTextureHeight + "\n" +
                      "FOV : " + Camera.main.fieldOfView + "\n" +
                      "Framerate cible : " + Application.targetFrameRate + "\n\n" +
                      "URL vidéo : " + player.url + "\n" +
                      "Frame actuelle : " + player.frame + "\n" +
                      "Framerate vidéo (attendu) : " + player.frameRate + "\n" +
                      "Is Playing : " + player.isPlaying + "\n" +
                      "Is Prepared : " + player.isPrepared + "\n" +
                      "Freeze time : " + freezeTimer.ToString("F1") + "s\n";

        StartCoroutine(SendDataToPC(info));
    }

    IEnumerator SendDataToPC(string message)
    {
        string url = "http://192.168.0.123:8889";
        UnityWebRequest req = UnityWebRequest.Put(url, message);
        req.method = UnityWebRequest.kHttpVerbPOST;
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            Debug.Log("✅ Données envoyées");
        else
            Debug.Log("❌ Erreur : " + req.error);
    }
}
