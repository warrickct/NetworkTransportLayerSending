using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Linq;

public class NetSender : MonoBehaviour {

    public Text inputText;
    public Text displayText;

    public GameObject model;

    GlobalConfig gConfig;

    public int port = 12345;
    public string connectionTargetIp = "127.0.0.1";
    public int connectionLimit = 10;

    int hostId;
    int connectionId;
    int myReliableChannelId;
    int myUnreliableChannelId;
    int myReliableFragmentedChannelId;

    void Start () {
        StartHost();
        ConnectToHost();
	}
	
	void Update () {
        ReceiveData();
        //Debug.Log("connection id: " + connectionId);
	}

    void StartHost()
    {
        // Init Transport using default values.
        NetworkTransport.Init();
        
        // Create a connection config and add a Channel.
        ConnectionConfig config = new ConnectionConfig();
        config.MaxCombinedReliableMessageSize = 300;

        //Creating channels
        myReliableChannelId = config.AddChannel(QosType.Reliable);
        myUnreliableChannelId = config.AddChannel(QosType.Unreliable);
        myReliableFragmentedChannelId = config.AddChannel(QosType.ReliableFragmented);

        // Create a topology based on the connection config.
        HostTopology topology = new HostTopology(config, 10);

        // Create a host based on the topology we just created, and bind the socket to port in inspector.
        hostId = NetworkTransport.AddHost(topology, port);
    }

    void ConnectToHost()
    {
        byte error;
        connectionId = NetworkTransport.Connect(hostId, "127.0.0.1", port, 0, out error);
    }

    void SendData(byte[] buffer)
    {
        byte error;
        int bufferLength = 1024;
        NetworkTransport.Send(hostId, connectionId, myReliableFragmentedChannelId, buffer, bufferLength, out error);
    }

    void ReceiveData()
    {
        int outHostId;
        int outConnectionId;
        int outChannelId;
        byte[] buffer = new byte[1024];
        int bufferSize = 1024;
        int receiveSize;
        byte error;

        NetworkEventType evnt = NetworkTransport.Receive(out outHostId, out outConnectionId, out outChannelId, buffer, bufferSize, out receiveSize, out error);
        switch (evnt)
        {
            case NetworkEventType.ConnectEvent:
                if (outHostId == hostId &&
                    outConnectionId == connectionId &&
                    (NetworkError)error == NetworkError.Ok)
                {
                    Debug.Log("Connected");
                }
                break;
            case NetworkEventType.DataEvent:
                Debug.Log("Received data from hostid: " + outHostId + " on connection id: " + outConnectionId);
                DecodeData(buffer);
                break;
        }
    }

    void DecodeData(byte[] buffer)
    {
        
    }

    void Disconnect()
    {
        byte error;
        NetworkTransport.Disconnect(hostId, connectionId, out error);
    }

    public void SendText()
    {
        string text = inputText.text;
        byte[] buffer = System.Text.Encoding.ASCII.GetBytes(text);
        SendData(buffer);
    }

    public void SendStart()
    {

    }

    public void SendModelData()
    {
        //convert to serializable model data
        Mesh mesh = model.GetComponent<MeshFilter>().mesh;
        ModelData modelData = new ModelData(mesh.vertices, mesh.uv, mesh.triangles);

        //convert serializable to byte array
        MemoryStream ms = new MemoryStream();
        BinaryFormatter bf = new BinaryFormatter();
        bf.Serialize(ms, modelData);
        byte[] data = ms.ToArray();

        //send byte array
        byte error;
        NetworkTransport.Send(hostId, connectionId, myReliableFragmentedChannelId, data, data.Length, out error);
    }
}

[Serializable]
public class ModelData
{

    //todo: convert to lists to be dynamically added.
    public List<Vertex> verticesList = new List<Vertex>();
    public List<UV> uvsList = new List<UV>();
    public List<int> trisList = new List<int>();

    //todo: loop through each array and add as you go.
    public ModelData(Vector3[] verts, Vector2[] uvs, int[] tris)
    {
        // Iterate through vert, uv, triangle arrays, convert to convenience classes for listing to be serialized.
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
