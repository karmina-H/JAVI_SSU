using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

// ������ JSON �������� ������ ���� C# Ŭ���� ����
[System.Serializable]
public class EmotionResponseData
{
    public string emotion;
    public float probability;
    public string response;
}

public class InfoReceiver : MonoBehaviour
{
    [Header("UI ����")]
    public TextMeshProUGUI gptResponseText;
    public Image emojiImage;
    public TextMeshProUGUI probabilityText;

    [Header("�̸�Ƽ�� ����")]
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

            // GPT ���� �ؽ�Ʈ ������Ʈ
            if (gptResponseText != null)
            {
                gptResponseText.text = data.response;
            }

            // ���� �̸�Ƽ�� ������Ʈ
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

            // Ȯ�� �ؽ�Ʈ ������Ʈ
            if (probabilityText != null)
            {
                // [����] "�����̸�: Ȯ��%" �������� ���ڿ� ���� ����
                probabilityText.text = $"{data.emotion}: {data.probability * 100:F2}%";
            }
        }
        catch (Exception e)
        {
            Debug.LogError("JSON ������ ó�� �� ����: " + e.Message);
        }
    }

    // --- ��Ʈ��ũ ���� �� ���� �Լ��� ������ ���� ---
    private void ConnectToTcpServer()
    {
        try
        {
            receiveThread = new Thread(() =>
            {
                try // ������ ���ο����� try-catch�� ���δ� ���� �������Դϴ�.
                {
                    client = new TcpClient("127.0.0.1", 9999);

                    // �ڡڡ� ���Ⱑ ������ �κ��Դϴ� �ڡڡ�
                    // Python�� UTF-8�� �������Ƿ�, C#�� �ݵ�� UTF-8�� �о�� �մϴ�.
                    reader = new StreamReader(client.GetStream(), System.Text.Encoding.UTF8);

                    Debug.Log("Python ������ ����Ǿ����ϴ�. ������ ���� ��� ��...");

                    while (true)
                    {
                        // reader.ReadLine()�� ���ŷ �Լ��̹Ƿ�, �����Ͱ� �� ������ ���⼭ ����մϴ�.
                        string line = reader.ReadLine();
                        if (line != null)
                        {
                            receivedJson = line;
                        }
                    }
                }
                catch (SocketException e)
                {
                    Debug.LogError("���� ���� �߻� (������ ���� �ִ��� Ȯ���ϼ���): " + e.Message);
                }
                catch (ThreadAbortException)
                {
                    // ������ ����� �������� ��Ȳ�̹Ƿ� �α׸� ������ �ʽ��ϴ�.
                }
                catch (Exception e)
                {
                    Debug.LogError("���� ������ ����: " + e.Message);
                }
            });
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogError("TCP ���� ���� ������ ���� ����: " + e.Message);
        }
    }

    private void OnDestroy()
    {
        receiveThread?.Abort();
        reader?.Close();
        client?.Close();
    }
}