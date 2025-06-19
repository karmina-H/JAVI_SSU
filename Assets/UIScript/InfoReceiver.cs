using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

// 수신할 JSON 데이터의 구조에 맞춰 C# 클래스 정의
[System.Serializable]
public class EmotionResponseData
{
    public string emotion;
    public float probability;
    public string response;
}

public class InfoReceiver : MonoBehaviour
{
    [Header("UI 연결")]
    public TextMeshProUGUI gptResponseText;
    public Image emojiImage;
    public TextMeshProUGUI probabilityText;

    [Header("이모티콘 설정")]
    public List<EmojiMapping> emojiMappings;
    public Sprite defaultSprite;

    private Dictionary<string, Sprite> emojiDictionary;
    private TcpClient client;
    private StreamReader reader;
    private Thread receiveThread;
    private volatile string receivedJson = null;

    [System.Serializable]
    public struct EmojiMapping
    {
        public string command;
        public Sprite sprite;
    }

    void Awake()
    {
        emojiDictionary = new Dictionary<string, Sprite>();
        foreach (var mapping in emojiMappings)
        {
            emojiDictionary[mapping.command] = mapping.sprite;
        }
    }

    void Start()
    {
        ConnectToTcpServer();
    }

    void Update()
    {
        if (receivedJson != null)
        {
            ProcessReceivedData(receivedJson);
            receivedJson = null;
        }
    }

    private void ProcessReceivedData(string jsonString)
    {
        try
        {
            EmotionResponseData data = JsonUtility.FromJson<EmotionResponseData>(jsonString);

            // GPT 응답 텍스트 업데이트
            if (gptResponseText != null)
            {
                gptResponseText.text = data.response;
            }

            // 감정 이모티콘 업데이트
            if (emojiImage != null)
            {
                if (emojiDictionary.ContainsKey(data.emotion))
                {
                    emojiImage.sprite = emojiDictionary[data.emotion];
                }
                else
                {
                    emojiImage.sprite = defaultSprite;
                }
            }

            // 확률 텍스트 업데이트
            if (probabilityText != null)
            {
                // [수정] "감정이름: 확률%" 형식으로 문자열 포맷 변경
                probabilityText.text = $"{data.emotion}: {data.probability * 100:F2}%";
            }
        }
        catch (Exception e)
        {
            Debug.LogError("JSON 데이터 처리 중 에러: " + e.Message);
        }
    }

    // --- 네트워크 연결 및 종료 함수는 이전과 동일 ---
    private void ConnectToTcpServer()
    {
        try
        {
            receiveThread = new Thread(() =>
            {
                try // 스레드 내부에서도 try-catch로 감싸는 것이 안정적입니다.
                {
                    client = new TcpClient("127.0.0.1", 9999);

                    // ★★★ 여기가 수정된 부분입니다 ★★★
                    // Python이 UTF-8로 보냈으므로, C#도 반드시 UTF-8로 읽어야 합니다.
                    reader = new StreamReader(client.GetStream(), System.Text.Encoding.UTF8);

                    Debug.Log("Python 서버에 연결되었습니다. 데이터 수신 대기 중...");

                    while (true)
                    {
                        // reader.ReadLine()은 블로킹 함수이므로, 데이터가 올 때까지 여기서 대기합니다.
                        string line = reader.ReadLine();
                        if (line != null)
                        {
                            receivedJson = line;
                        }
                    }
                }
                catch (SocketException e)
                {
                    Debug.LogError("소켓 에러 발생 (서버가 켜져 있는지 확인하세요): " + e.Message);
                }
                catch (ThreadAbortException)
                {
                    // 스레드 종료는 정상적인 상황이므로 로그를 남기지 않습니다.
                }
                catch (Exception e)
                {
                    Debug.LogError("수신 스레드 에러: " + e.Message);
                }
            });
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogError("TCP 서버 연결 스레드 시작 실패: " + e.Message);
        }
    }

    private void OnDestroy()
    {
        receiveThread?.Abort();
        reader?.Close();
        client?.Close();
    }
}