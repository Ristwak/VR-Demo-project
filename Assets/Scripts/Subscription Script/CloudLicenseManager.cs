using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class IpInfo
{
    public string city;
    public string region;
    public string country;
}

public class CloudLicenseManager : MonoBehaviour
{
    [Header("License Info")]
    public string licenseKey = "12345678";
    private string projectName;
    private string scriptURL = "https://script.google.com/macros/s/AKfycbxTEQbD1YhznRzg4jE8KrznOmuGHeoJVdA8_GfMqSOsur16LyE20XQGFI8IQtSxD-Ka6w/exec";

    [Header("Subscription Panel")]
    public GameObject subscriptionPanel;

    [Header("Camera Reference")]
    public Transform vrCamera;   // Assign your VR or Main Camera here in Inspector
    public float distanceFromCamera = 2.0f; // How far the panel appears in front of camera
    public float heightOffset = 0.0f;       // Optional offset if needed

    private string location = "Unknown";
    private bool isValid = false;

    void Start()
    {
        if (vrCamera == null)
            vrCamera = Camera.main?.transform; // Auto-assign main camera if not set

        StartCoroutine(DetectLocationAndCheckLicense());
        projectName = Application.productName;
    }

    IEnumerator DetectLocationAndCheckLicense()
    {
        yield return StartCoroutine(DetectLocation());
        yield return StartCoroutine(CheckLicense());
    }

    IEnumerator DetectLocation()
    {
        UnityWebRequest www = UnityWebRequest.Get("https://ipinfo.io/json");
        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            string json = www.downloadHandler.text;
            IpInfo info = JsonUtility.FromJson<IpInfo>(json);
            location = $"{info.region}, {info.country}".Trim();
        }
        else
        {
            try
            {
                location = new RegionInfo(CultureInfo.CurrentCulture.Name).EnglishName;
            }
            catch
            {
                location = "Unknown";
            }
        }

        Debug.Log("📍 Auto-detected Location: " + location);
    }

    IEnumerator CheckLicense()
    {
        string deviceID = SystemInfo.deviceUniqueIdentifier;
        string url = string.Format(
            "{0}?key={1}&device={2}&project={3}&loc={4}",
            scriptURL,
            UnityWebRequest.EscapeURL(licenseKey),
            UnityWebRequest.EscapeURL(deviceID),
            UnityWebRequest.EscapeURL(projectName),
            UnityWebRequest.EscapeURL(location)
        );

        UnityWebRequest www = UnityWebRequest.Get(url);
        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            string result = www.downloadHandler.text;

            if (result.StartsWith("VALID"))
            {
                string expiryStr = result.Split('|')[1].Trim();
                string[] formats = {
                    "ddd MMM dd yyyy HH:mm:ss 'GMT'K '(India Standard Time)'",
                    "ddd MMM dd yyyy HH:mm:ss 'GMT+0530 (India Standard Time)'",
                    "yyyy-MM-dd",
                    "yyyy-MM-ddTHH:mm:ss"
                };

                if (DateTime.TryParseExact(expiryStr, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime expiryDate))
                {
                    if (DateTime.Now <= expiryDate)
                    {
                        Debug.Log("✅ License valid until: " + expiryDate.ToString("yyyy-MM-dd"));
                        isValid = true;
                        yield break;
                    }
                }
            }
        }

        Debug.Log("❌ Invalid License — showing subscription panel.");
        Time.timeScale = 0f;
        ShowSubscriptionPanelInFrontOfCamera();
    }

    private void ShowSubscriptionPanelInFrontOfCamera()
    {
        if (subscriptionPanel == null || vrCamera == null) return;

        subscriptionPanel.SetActive(true);

        // Calculate position in front of camera
        Vector3 targetPosition = vrCamera.position + vrCamera.forward * distanceFromCamera;
        targetPosition.y += heightOffset;

        // Set panel position & rotation
        subscriptionPanel.transform.position = targetPosition;
        subscriptionPanel.transform.rotation = Quaternion.LookRotation(
            subscriptionPanel.transform.position - vrCamera.position
        );
    }
}
