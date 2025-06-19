using UnityEngine;
using UnityEngine.UI;
using System.Collections; // 코루틴을 사용하기 위해 필요합니다.
using UnityEngine.Networking; // UnityWebRequest를 사용하기 위해 필요합니다.


public class UIManager : MonoBehaviour
{
    public GameObject voiceOptionsPanel;
    private string selectedVoice;

    // ... (OnVoiceSelectButtonClick 함수는 그대로) ...
    public void OnVoiceSelectButtonClick()
    {
        bool isActive = voiceOptionsPanel.activeSelf;
        voiceOptionsPanel.SetActive(!isActive);
    }

    public void OnOptionClick(string voiceName)
    {
        selectedVoice = voiceName;
        UnityEngine.Debug.Log("C#: 선택된 목소리: " + selectedVoice);
        voiceOptionsPanel.SetActive(false);

        // 파이썬 서버에 요청을 보내는 코루틴을 시작합니다.
        StartCoroutine(SendRequestToPythonServer(selectedVoice));
    }

    IEnumerator SendRequestToPythonServer(string voiceNameToPass)
    {
        // 파이썬 서버에서 설정한 주소 형식에 맞게 URL을 만듭니다.
        string url = "http://127.0.0.1:5001/set_voice/" + voiceNameToPass;

        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            // 요청을 보내고 응답을 기다립니다.
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                // 성공적으로 응답을 받았을 때
                UnityEngine.Debug.Log("Python Server Response: " + webRequest.downloadHandler.text);
            }
            else
            {
                // 에러가 발생했을 때
                UnityEngine.Debug.LogError("Error: " + webRequest.error);
            }
        }
    }
}