using UnityEngine;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TMPro; // TextMeshPro 사용에 필요

public class TextReceiver : MonoBehaviour
{
    public TextMeshProUGUI textElement; // 인스펙터에서 연결할 TextMeshPro UI

    private TcpClient client;
    private NetworkStream stream;
    private Thread receiveThread;
    private string receivedText = "";
    private bool isTextUpdated = false;

    void Start()
    {
        ConnectToServer();
    }

    void Update()
    {
        if (isTextUpdated)
        {
            textElement.text = receivedText;
            isTextUpdated = false;
        }
    }

    void ConnectToServer()
    {
        try
        {
            receiveThread = new Thread(() =>
            {
                client = new TcpClient("127.0.0.1", 9999);
                stream = client.GetStream();
                byte[] buffer = new byte[1024];
                int bytesRead;

                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    receivedText = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    isTextUpdated = true;
                }
            });
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogError("서버 연결 실패: " + e.Message);
        }
    }

    private void OnDestroy()
    {
        receiveThread?.Abort();
        stream?.Close();
        client?.Close();
    }
}