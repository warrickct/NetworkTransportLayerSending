using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
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
                OnData(outHostId, outConnectionId, outChannelId, buffer, bufferSize, (NetworkError)error);
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
                Debug.Log("End send, total bytes received: " + totalBytes);
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
        ModelData modelData = new ModelData(mesh.vertices, mesh.uv, mesh.triangles);

        //convert to byte array
        MemoryStream memStream = new MemoryStream();
        BinaryFormatter binFormatter = new BinaryFormatter();
        binFormatter.Serialize(memStream, modelData);
        byte[] data = memStream.ToArray();
        memStream.Dispose();

        Debug.Log("Sending model of byte[] size: " + data.Length);

        byte[] chunk;
        int finalChunkSize = ChunkSize - (data.Length % ChunkSize);
        Debug.Log("final chunk size" + finalChunkSize);
        for (int i=0; i < data.Length; i += ChunkSize)
        {
            if (i + ChunkSize < data.Length)
            {
                chunk = data.Skip(i).Take(ChunkSize).ToArray();
                Debug.Log("taking size: " + chunk.Length);
            }
            else
            {
                chunk = data.Skip(i).Take(data.Length - i).ToArray();
                Debug.Log("taking size: " + chunk.Length);
            }
            byte error;
            NetworkTransport.Send(hostId, connectionId, myModelSendingChannel, chunk, chunk.Length, out error);
        }
    }
}

[Serializable]
public class ModelData

    // TODO: Could refactor to make lists of arrays then no need for vert and uv class.
    // will also make it easier to work with messagePack.
{
    public List<Vertex> verticesList = new List<Vertex>();
    public List<UV> uvsList = new List<UV>();
    public List<int> trisList = new List<int>();

    public ModelData(Vector3[] verts, Vector2[] uvs, int[] tris)
    {
        foreach(Vector3 v3 in verts)
        {
            Vertex vertex = new Vertex(v3);
            verticesList.Add(vertex);
        }
        foreach (Vector2 v2 in uvs)
        {
            UV uv = new UV(v2);
            uvsList.Add(uv);
        }
        foreach (int triangle in tris)
        {
            trisList.Add(triangle);
        }
        Debug.Log("model data construction done. Verts: " + verticesList.Count + " UVS: " + uvsList.Count + " triangles: " + trisList.Count);
    }
}

[Serializable]
public class Vertex
{
    public float vertX, vertY, vertZ;

    public Vertex(Vector3 v3)
    {
        this.vertX = v3.x;
        this.vertY = v3.y;
        this.vertZ = v3.z;
    }
}

[Serializable]
public class UV
{
    public float uvX, uvY;

    public UV(Vector2 v2)
    {
        this.uvX = v2.x;
        this.uvY = v2.y;
    }
}
