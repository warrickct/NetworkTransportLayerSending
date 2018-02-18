using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Linq;
using System.Text;

public class NetSender : MonoBehaviour {

    public Text inputText;
    public Text displayText;

    public GameObject model;

    GlobalConfig gConfig;

    public int port = 12345;
    public string connectionTargetIp = "127.0.0.1";
    public int connectionLimit = 10;

    const int ChunkSize = 1024;

    int hostId;
    int connectionId;
    int myModelSendingChannel;
    int myStringSendingChannel;

    BinaryFormatter bf = new BinaryFormatter();

    void Start () {
        StartHost();
        ConnectToHost();
	}
	
	void Update () {
        ReceiveData();
	}

    void StartHost()
    {
        // Init Transport using default values.
        NetworkTransport.Init();
        
        // Create a connection config and add a Channel.
        ConnectionConfig config = new ConnectionConfig();

        //Creating channels
        myModelSendingChannel = config.AddChannel(QosType.ReliableFragmentedSequenced);
        myStringSendingChannel = config.AddChannel(QosType.ReliableFragmentedSequenced);

        // Create a topology based on the connection config.
        HostTopology topology = new HostTopology(config, 10);

        // Create a host based on the topology we just created, and bind the socket to port in inspector.
        hostId = NetworkTransport.AddHost(topology, port);
    }

    void ConnectToHost()
    {
        byte error;
        connectionId = NetworkTransport.Connect(hostId, connectionTargetIp, port, 0, out error);
    }

    void SendData(byte[] buffer)
    {
        byte error;
        int bufferLength = ChunkSize;
        NetworkTransport.Send(hostId, connectionId, myModelSendingChannel, buffer, bufferLength, out error);
    }

    void ReceiveData()
    {
        int outHostId;
        int outConnectionId;
        int outChannelId;
        byte[] buffer = new byte[ChunkSize];
        int bufferSize = ChunkSize;
        int receiveSize;
        byte error;

        NetworkEventType evnt = NetworkTransport.Receive(out outHostId, out outConnectionId, out outChannelId, buffer, bufferSize, out receiveSize, out error);
        switch (evnt)
        {
            case NetworkEventType.ConnectEvent:
                OnConnect(outHostId, outConnectionId, (NetworkError)error);
                break;
            case NetworkEventType.DataEvent:
                if (receiveSize < ChunkSize)
                {
                    byte[] smallerBuffer = new byte[receiveSize];
                    OnData(outHostId, outConnectionId, outChannelId, smallerBuffer, smallerBuffer.Length, (NetworkError)error);
                }
                else
                {
                    OnData(outHostId, outConnectionId, outChannelId, buffer, bufferSize, (NetworkError)error);
                }
                break;
            case NetworkEventType.DisconnectEvent:
                if (outConnectionId == connectionId &&
                    (NetworkError)error == NetworkError.Ok)
                {
                    OnDisconnect(outHostId, outConnectionId);
                }
                break;
        }
    }

    void OnConnect(int outHostId, int outConnectionId, NetworkError error)
    {
        if (outHostId == hostId &&
            outConnectionId == connectionId &&
            (NetworkError)error == NetworkError.Ok)
        {
            Debug.Log("Connected");
        }
    }

    byte[] totalBytes = new byte[0];

    void OnData(int recHostId, int recConnectionId, int recChannelId, byte[] recData, int recSize, NetworkError recError)
    {
        if (recChannelId == myStringSendingChannel)
        {
            byte[] data = recData;
            string decodeString = ASCIIEncoding.ASCII.GetString(data);
            displayText.text = decodeString.ToUpper();
        }
        if (recChannelId == myModelSendingChannel)
        {
            totalBytes = Combine(totalBytes, recData);
            Debug.Log(totalBytes.Length);
            if (recSize < ChunkSize)
            {
                Debug.Log("End send, total bytes received: " + totalBytes.Length);
                MemoryStream ms = new MemoryStream(totalBytes);
                ModelData recModelData = (ModelData)bf.Deserialize(ms);
            }
        }
    }

    public static byte[] Combine(byte[] first, byte[] second)
    {
        byte[] ret = new byte[first.Length + second.Length];
        Buffer.BlockCopy(first, 0, ret, 0, first.Length);
        Buffer.BlockCopy(second, 0, ret, first.Length, second.Length);
        return ret;
    }

    void OnDisconnect(int hostId, int connectionId)
    {
        byte error;
        NetworkTransport.Disconnect(hostId, connectionId, out error);
    }

    public void SendText()
    {
        string text = inputText.text;
        byte[] buffer = Encoding.ASCII.GetBytes(text);

        byte error;
        NetworkTransport.Send(hostId, connectionId, myStringSendingChannel, buffer, buffer.Length, out error);
    }

    public void SendModelData()
    {
        //Extract model mesh properties, convert to serializable model data
        Mesh mesh = model.GetComponent<MeshFilter>().mesh;
        ModelData modelData = new ModelData();

        foreach (Vector3 vert in mesh.vertices)
        {
            modelData.AddVertex(vert);
        }
        foreach (int triangle in mesh.triangles)
        {
            modelData.AddTriangle(triangle);
        }

        //convert to byte array
        
        MemoryStream ms = new MemoryStream();
        bf.Serialize(ms, modelData);
        byte[] data = ms.ToArray();

        //final byte calculations
        Debug.Log("data  " + data.Length);
        int finalSkipIndex = data.Length - (data.Length % ChunkSize);
        Debug.Log("final byte index " + finalSkipIndex);
        int finalTakeAmount = data.Length - finalSkipIndex;
        Debug.Log("Final take amount " + finalTakeAmount);

        for (int i = 0; i < data.Length; i+= ChunkSize)
        {
            byte[] chunk;
            if (!i.Equals(finalSkipIndex))
            {
                chunk = data.Skip(i).Take(ChunkSize).ToArray();
            }
            else
            {
                chunk = data.Skip(i).Take(finalTakeAmount).ToArray();
            }
            byte error;
            NetworkTransport.Send(hostId, connectionId, myModelSendingChannel, chunk, chunk.Length, out error);
        }
    }
}

[Serializable]
public class ModelData
{
    [SerializeField]
    public List<float[]> vertices = new List<float[]>();

    [SerializeField]
    public List<int> triangles = new List<int>();

    [SerializeField]
    public int verticesLength;

    [SerializeField]
    public int trianglesLength;

    public void AddVertex(Vector3 vertex)
    {
        float[] f = {vertex.x, vertex.y, vertex.z };
        vertices.Add(f);
    }

    public void AddTriangle(int triangle)
    {
        triangles.Add(triangle);
    }

    public ModelData()
    {
        
    }
}
