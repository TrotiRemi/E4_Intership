using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using UnityEngine.XR;

public class VRSystemInfoSender : MonoBehaviour
{
    void Start()
    {
        string info = "Helmet Name : " + XRSettings.loadedDeviceName + "\n" +
                      "Oeil Resolution : " + XRSettings.eyeTextureWidth + "x" + XRSettings.eyeTextureHeight + "\n" +
                      "FOV : " + Camera.main.fieldOfView + "\n" +
                      "Framerate : " + Application.targetFrameRate;

        StartCoroutine(SendDataToPC(info));
    }

    IEnumerator SendDataToPC(string message)
    {
        string url = "http://192.168.0.123:8888";
        UnityWebRequest req = UnityWebRequest.Put(url, message);
        req.method = UnityWebRequest.kHttpVerbPOST;
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            Debug.Log("Données envoyées avec succès");
        else
            Debug.Log("Erreur d’envoi : " + req.error);
    }
}
