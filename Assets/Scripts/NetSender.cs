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

    int hostId;
    int connectionId;
    int myReliableChannelId;
    int myUnreliableChannelId;
    int myReliableFragmentedSequencedChannelId;

    void Start () {
        StartHost();
        ConnectToHost();
	}
	
	void Update () {
        ReceiveData();
	}

    /// <summary>
    /// Initializes a network transport instance, sets up basic 
    /// QoS channels, defines max connections for a host.
    /// </summary>
    void StartHost()
    {
        // Init Transport using default values.
        NetworkTransport.Init();
        
        // Create a connection config and add a Channel.
        ConnectionConfig config = new ConnectionConfig();

        //Creating channels
        myReliableChannelId = config.AddChannel(QosType.Reliable);
        myUnreliableChannelId = config.AddChannel(QosType.Unreliable);
        myReliableFragmentedSequencedChannelId = config.AddChannel(QosType.ReliableFragmentedSequenced);
        byte channelId = config.AddChannel(QosType.ReliableFragmentedSequenced);

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
        int bufferLength = 1024;
        NetworkTransport.Send(hostId, connectionId, myReliableFragmentedSequencedChannelId, buffer, bufferLength, out error);
    }

    /// <summary>
    /// Listens for network events on a network transport connection.
    /// </summary>
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
                OnConnect(outHostId, outConnectionId, (NetworkError)error);
                break;
            case NetworkEventType.DataEvent:
                Debug.Log(receiveSize);
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

    /// <summary>
    /// Checks host and connection id are correct
    /// </summary>
    /// <param name="outHostId"></param>
    /// <param name="outConnectionId"></param>
    /// <param name="error"></param>
    void OnConnect(int outHostId, int outConnectionId, NetworkError error)
    {
        if (outHostId == hostId &&
            outConnectionId == connectionId &&
            (NetworkError)error == NetworkError.Ok)
        {
            Debug.Log("Connected");
        }
    }

    /// <summary>
    /// Converts network data event buffer data into a usable object.
    /// </summary>
    /// <param name="buffer"></param>
    void OnData(int recHostId, int recConnectionId, int recChannelId, byte[] recData, int recSize, NetworkError recError)
    {

    }

    /// <summary>
    /// Disconnects network transport connection
    /// </summary>
    void OnDisconnect(int hostId, int connectionId)
    {
        byte error;
        NetworkTransport.Disconnect(hostId, connectionId, out error);
    }

    public void SendText()
    {
        string text = inputText.text;
        byte[] buffer = Encoding.ASCII.GetBytes(text);
        SendData(buffer);
    }

    /// <summary>
    /// Converts a model vert, uv, triangles into byte[] and sends across network in chunks.
    /// </summary>
    public void SendModelData()
    {
        //convert to serializable model data
        Mesh mesh = model.GetComponent<MeshFilter>().mesh;
        ModelData modelData = new ModelData(mesh.vertices, mesh.uv, mesh.triangles);

        MemoryStream memStream = new MemoryStream();
        BinaryFormatter binFormatter = new BinaryFormatter();
        binFormatter.Serialize(memStream, modelData);
        byte[] data = memStream.ToArray();

        int chunkSize = 1024;
        int finalByteStartIndex = data.Length - (chunkSize - (data.Length % chunkSize));
        Debug.Log("Total data byte length" + data.Length);
        Debug.Log("final byte index" + finalByteStartIndex);
        byte error;
        for (int i=0; i < data.Length; i+= chunkSize)
        {
            
            byte[] chunk = data.Skip(i).Take(chunkSize).ToArray();
            NetworkTransport.Send(hostId, connectionId, myReliableFragmentedSequencedChannelId, chunk, chunk.Length, out error);
            //NetworkTransport.Send(hostId, connectionId, myReliableFragmentedSequencedChannelId, chunk, chunk.Length, out error);
            if (i == finalByteStartIndex)
            {
                byte[] finalChunk = data.Skip(i).Take(data.Length - i).ToArray();
                //NetworkTransport.Send(hostId, connectionId, myReliableFragmentedSequencedChannelId, finalChunk, finalChunk.Length, out error);
                NetworkTransport.Send(hostId, connectionId, myReliableFragmentedSequencedChannelId, finalChunk, finalChunk.Length, out error);
            }
        }
        NetworkTransport.SendQueuedMessages(hostId, connectionId, out error);
    }
}

/*
[Serializable]
public class WireData
{
    public byte[] modelData;
    public int modelSize;

    public WireData(byte[] modelData, int size)
    {
        this.modelData = modelData;
        this.modelSize = size;
    }
}
*/

/// <summary>
/// Serializable class used for sending objects across a network.
/// </summary>
[Serializable]
public class ModelData

    // TODO: Could refactor to make lists of arrays then no need for vert and uv class.
    // will also make it easier to work with messagePack.
{
    public List<Vertex> verticesList = new List<Vertex>();
    public List<UV> uvsList = new List<UV>();
    public List<int> trisList = new List<int>();

    public long FullModelSize { get; set; }

    /// <summary>
    ///  Iterate through vert, uv, triangle arrays, convert to 
    ///  convenience classes for listing to be serialized.
    /// </summary>
    /// <param name="verts"></param>
    /// <param name="uvs"></param>
    /// <param name="tris"></param>
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
