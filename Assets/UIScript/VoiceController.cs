using UnityEngine;
using UnityEngine.UI;
using System.Collections; // �ڷ�ƾ�� ����ϱ� ���� �ʿ��մϴ�.
using UnityEngine.Networking; // UnityWebRequest�� ����ϱ� ���� �ʿ��մϴ�.


public class UIManager : MonoBehaviour
{
    public GameObject voiceOptionsPanel;
    private string selectedVoice;

    // ... (OnVoiceSelectButtonClick �Լ��� �״��) ...
    public void OnVoiceSelectButtonClick()
    {
        bool isActive = voiceOptionsPanel.activeSelf;
        voiceOptionsPanel.SetActive(!isActive);
    }

    public void OnOptionClick(string voiceName)
    {
        selectedVoice = voiceName;
        UnityEngine.Debug.Log("C#: ���õ� ��Ҹ�: " + selectedVoice);
        voiceOptionsPanel.SetActive(false);

        // ���̽� ������ ��û�� ������ �ڷ�ƾ�� �����մϴ�.
        StartCoroutine(SendRequestToPythonServer(selectedVoice));
    }

    IEnumerator SendRequestToPythonServer(string voiceNameToPass)
    {
        // ���̽� �������� ������ �ּ� ���Ŀ� �°� URL�� ����ϴ�.
        string url = "http://127.0.0.1:5001/set_voice/" + voiceNameToPass;

        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            // ��û�� ������ ������ ��ٸ��ϴ�.
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                // ���������� ������ �޾��� ��
                UnityEngine.Debug.Log("Python Server Response: " + webRequest.downloadHandler.text);
            }
            else
            {
                // ������ �߻����� ��
                UnityEngine.Debug.LogError("Error: " + webRequest.error);
            }
        }
    }
}