using System;
using System.IO;
using System.Text;
using UnityEngine;
using Vuforia;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json.Linq;
using UnityEngine.InputSystem.EnhancedTouch;
//using Unity.Android.Gradle.Manifest;


public class CameraImageAccess : MonoBehaviour
{
    Vuforia.PixelFormat PIXEL_FORMAT = Vuforia.PixelFormat.RGB888;

    // ARCamera
    public Camera arCamera;
    public GameObject imageTarget;

    // 전송 관련
    UdpClient udpSender;              // Python에게 카메라 이미지를 보낼 소켓
    IPEndPoint pythonEndPoint;
    int packetSize = 60000;

    // 수신 관련
    UdpClient udpReceiver;            // Python에서 보내준 Hand 데이터를 받을 소켓
    Thread receiveThread;
    bool running = false;


    // 손 위치 UI 표시
    private Vector2 screenSize;

    //3D Rendering
    public GameObject objectToRender;  // 렌더링할 3D 오브젝트
    public Vector2 screenPosition; // 렌더링할 3D 위치


    //depth관련 변수
    public float finger_depth = 0;
    public bool is_touched = false;
    public bool is_increase = true;

    //grab관련 변수
    public int is_grabbed;

    Vector3 ConvertScreenToWorld(Vector2 screenPos, float distance)
    {
        Vector3 screenPoint = new Vector3(screenPos.x, screenPos.y, arCamera.nearClipPlane + distance);
        return arCamera.ScreenToWorldPoint(screenPoint);
    }


    void Start()
    {
        //image target밑에 3d객체가 있어서 renderer이 false된 상태임. 그래서 직접 true로 바꿔줘야함.
        Renderer renderer = objectToRender.GetComponentInChildren<Renderer>();
        if (!renderer.enabled)
        {
            Debug.LogWarning("Renderer is disabled! Enabling now...");
            renderer.enabled = true;
        }

        // ------------------------------------------------
        // 1) Python으로 이미지 전송할 소켓 초기화
        // ------------------------------------------------
        udpSender = new UdpClient();
        pythonEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5001);
        // 위 IP/PORT는 파이썬에서 `recv_sock.bind(...)` 한 곳과 맞춰야 함

        // ------------------------------------------------
        // 2) Python으로부터 Hand 정보를 받을 소켓 초기화
        // ------------------------------------------------
        udpReceiver = new UdpClient(5002); // 파이썬이 send_sock.sendto(...) 하는 곳
        running = true;
        receiveThread = new Thread(new ThreadStart(ReceiveHandData));
        receiveThread.IsBackground = true;
        receiveThread.Start();

        screenSize = new Vector2(UnityEngine.Screen.width / 2.0f, UnityEngine.Screen.height / 2.0f);
        Vector3 worldPosition = ConvertScreenToWorld(screenSize, 1.0f);
        objectToRender.transform.position = worldPosition;

        Debug.Log("Cube_Position :  " + worldPosition);

        // ------------------------------------------------
        // 4) Vuforia 이벤트 등록
        // ------------------------------------------------
        VuforiaApplication.Instance.OnVuforiaStarted += OnVuforiaStarted;
        VuforiaApplication.Instance.OnVuforiaStopped += OnVuforiaStopped;

        if (arCamera == null)
            Debug.Log("No camera .......");
        arCamera = Camera.main;

        if (VuforiaBehaviour.Instance != null)
        {
            VuforiaBehaviour.Instance.World.OnStateUpdated += OnVuforiaUpdated;
        }
    }

    void OnDestroy()
    {
        // 이벤트 해제
        if (VuforiaBehaviour.Instance != null)
        {
            VuforiaBehaviour.Instance.World.OnStateUpdated -= OnVuforiaUpdated;
        }
        VuforiaApplication.Instance.OnVuforiaStarted -= OnVuforiaStarted;
        VuforiaApplication.Instance.OnVuforiaStopped -= OnVuforiaStopped;

        // 픽셀 포맷 해제
        if (VuforiaApplication.Instance.IsRunning)
        {
            UnregisterFormat();
        }

        // 스레드 및 소켓 종료
        running = false;
        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Abort();
            receiveThread = null;
        }
        udpReceiver.Close();
        udpSender.Close();
    }

    // ---------------------------------------------------------------------
    // Vuforia 시작/종료 시점의 콜백 (카메라 픽셀 포맷 설정 등)
    // ---------------------------------------------------------------------
    void OnVuforiaStarted()
    {
        bool success = VuforiaBehaviour.Instance.CameraDevice.SetFrameFormat(PIXEL_FORMAT, true);
    }
    void OnVuforiaStopped()
    {
        UnregisterFormat();
    }
    void UnregisterFormat()
    {
        //Debug.Log("Unregistering camera pixel format " + PIXEL_FORMAT);
        VuforiaBehaviour.Instance.CameraDevice.SetFrameFormat(PIXEL_FORMAT, false);
    }

    // ---------------------------------------------------------------------
    // 매 프레임 Vuforia가 업데이트될 때, 카메라 이미지를 Python 쪽으로 전송
    // ---------------------------------------------------------------------


    void OnVuforiaUpdated()
    {
        Debug.Log("Cube_Position :  " + objectToRender.transform.position);
        var image = VuforiaBehaviour.Instance.CameraDevice.GetCameraImage(PIXEL_FORMAT);

        if (image != null)
        {
            byte[] pixels = image.Pixels;
            int totalPackets = (pixels.Length + packetSize - 1) / packetSize;

            // (1) 여러 패킷으로 나눠 전송
            for (int i = 0; i < totalPackets; i++)
            {
                int offset = i * packetSize;
                int length = Math.Min(packetSize, pixels.Length - offset);
                byte[] packet = new byte[length + 4];

                // 패킷 ID (packet_id) 넣기
                BitConverter.GetBytes(i).CopyTo(packet, 0);
                Array.Copy(pixels, offset, packet, 4, length);

                udpSender.Send(packet, packet.Length, pythonEndPoint);
            }

            // (2) 마지막 패킷 (ID = -1) 전송 → Python에서 한 프레임 마무리
            byte[] endSignal = BitConverter.GetBytes(-1);
            udpSender.Send(endSignal, endSignal.Length, pythonEndPoint);
        }

    }

    // ---------------------------------------------------------------------
    // [핵심] Python이 보내주는 Hand JSON 데이터를 UDP로 수신
    // ---------------------------------------------------------------------
    void ReceiveHandData()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

        while (running)
        {
            try
            {
                // 블로킹 수신
                byte[] data = udpReceiver.Receive(ref remoteEP);
                string jsonText = Encoding.UTF8.GetString(data);

                JArray jsonArray = JArray.Parse(jsonText);
                if (jsonArray.Count > 0)
                {
                    //gesture여부 판단하는곳
                    if ((JObject)jsonArray[1] != null)
                    {
                        JObject grab_info = (JObject)jsonArray[1];
                        JArray grab_array = (JArray)grab_info["grab"];
                        is_grabbed = grab_array[0].ToObject<int>();
                    }
                    else
                    {
                        is_grabbed = 0;
                    }

                    //손관절 좌표 
                    JObject firstHand = (JObject)jsonArray[0];
                    JArray lmList = (JArray)firstHand["lmList"];
                    if (lmList.Count > 9)
                    {
                        float x1 = lmList[4][0].ToObject<float>(); //검지 끝 x좌표
                        float y1 = lmList[4][1].ToObject<float>(); //검지 끝 y좌표
                        float x2 = lmList[8][0].ToObject<float>(); //검지 끝 x좌표
                        float y2 = lmList[8][1].ToObject<float>(); //검지 끝 y좌표
                        //Debug.Log("x: " + x);
                        //Debug.Log("y: " + y);
                        // Unity 메인 스레드에서 UI를 갱신하기 위해 Invoke
                        // (만약 다른 MainThreadDispatcher 등을 쓰고 있다면 맞춰서 변경)
                        //원래 객체의 위치나 UI업데이트는 메인스레드에서 해야해서
                        //MainThreadDispatcher를 통해서 메일 스레드에서 UpdateHandPosition함수를 호출하도록 큐에넣어줌
                        //지금 이 함수가 메인스레드가 아닌 스레드에서 수행되고 있기 때문에 이거해줌
                        UnityMainThreadDispatcher.Instance().Enqueue(() =>
                        {
                            UpdateHandPosition(x1, y1, x2, y2);
                        });
                    }
                }

            }
            catch (Exception e)
            {
                Debug.LogWarning("UDP 수신 오류: " + e.Message);
            }
        }
    }

    // ---------------------------------------------------------------------
    // 3D UI상에 손 위치 업데이트
    // ---------------------------------------------------------------------

    void UpdateHandPosition(float x1, float y1, float x2, float y2)
    {
        // Python에서 검출 기준 640×480으로 가정하고 (width=640, height=480)
        // Unity스크린 해상도(or Canvas 크기)에 맞춰보려면 적절히 변환
        Vector3 screenPos1 = new Vector3(
            x1,
            (1 - (y1 / 480f)) * UnityEngine.Screen.height, // y는 위아래가 뒤집힐 수 있으니 1 - (y/480) -> 좌표계가 달라서 변환해주는것
            10.0f + finger_depth
        );
        Vector3 screenPos2 = new Vector3(
            x2,
            (1 - (y2 / 480f)) * UnityEngine.Screen.height, // y는 위아래가 뒤집힐 수 있으니 1 - (y/480) -> 좌표계가 달라서 변환해주는것
            10.0f + finger_depth
        );
        //왼쪽 아래가 원점으로!
        //Debug.Log("screenPos1: " + screenPos1);
        //Debug.Log("screenPos2: " + screenPos2);
        Vector2 screenPosition1 = new Vector2(screenPos1.x, screenPos1.y);
        Vector2 screenPosition2 = new Vector2(screenPos2.x, screenPos2.y);

        Vector3 finger1_world1 = ConvertScreenToWorld(screenPosition1, (float)10.5);
        Vector3 finger1_world2 = ConvertScreenToWorld(screenPosition2, (float)10.5);

        float distance1 = Vector3.Distance(finger1_world1, objectToRender.transform.position);
        float distance2 = Vector3.Distance(finger1_world2, objectToRender.transform.position);

        if (distance1 < 1f && distance2 < 1f)
        {
            is_touched = true;
            //Debug.Log("touched!");
        }
        else
        {
            is_touched = false;
        }

        if (is_touched && is_grabbed == 1)
        {
            Debug.Log("grabbing...");
            float average_x1 = screenPosition1.x + screenPosition2.x;
            float average_y1 = screenPosition1.y + screenPosition2.y;
            Vector2 average_pos2 = new Vector2(average_x1 / (float)2.0, average_y1 / (float)2.0);
            Vector3 average_pos3 = ConvertScreenToWorld(average_pos2, (float)10.5);

            objectToRender.transform.position = average_pos3;
        }

    }
}