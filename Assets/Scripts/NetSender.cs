﻿using System.Collections.Generic;
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
    int myReliableSequencedChannelId;
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
        myReliableSequencedChannelId = config.AddChannel(QosType.ReliableSequenced);
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
        NetworkTransport.Send(hostId, connectionId, myReliableSequencedChannelId, buffer, bufferLength, out error);
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
                OnData(outHostId, outConnectionId, outChannelId, buffer, receiveSize, (NetworkError)error);
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

    List<byte> recBytes = new List<byte>();
    void OnData(int recHostId, int recConnectionId, int recChannelId, byte[] recData, int recSize, NetworkError recError)
    {
        if (recChannelId == myStringSendingChannel)
        {
            MemoryStream ms = new MemoryStream(recData);
            BinaryFormatter bf = new BinaryFormatter();

            Debug.Log("rec buffer length " + recData.Length);
            string recText = bf.Deserialize(ms) as string;

            Debug.Log(recText);
            displayText.text = recText.ToUpper();
        }
        else if (recChannelId == myReliableSequencedChannelId)
        {
            List<byte> addBytes = new List<byte>(recData);
            recBytes.AddRange(addBytes);

            if (recSize < ChunkSize)
            {
                byte[] totalData = recBytes.ToArray();
                BinaryFormatter bf = new BinaryFormatter();
                MemoryStream ms = new MemoryStream(totalData);
                MeshWireData meshWireData = bf.Deserialize(ms) as MeshWireData;
                Debug.Log(meshWireData.verticesLength);
                Debug.Log(meshWireData.trianglesLength);


                //loop 2d float array, make into vectors and add to new vertices array
                Vector3[] genVertices = new Vector3[meshWireData.verticesLength];
                for (int i=0; i < meshWireData.verticesLength; i++)
                {
                    //0 = x, 1 = y, 2 = z
                    genVertices[i] = new Vector3(meshWireData.vertices[i, 0], meshWireData.vertices[i, 1], meshWireData.vertices[i, 2]);
                }
                Debug.Log(genVertices.Length);

                //assign received triangle array to genTriangles array
                int[] genTriangles = meshWireData.triangles;
                Debug.Log(genTriangles.Length);

                Mesh genMesh = new Mesh();
                genMesh.vertices = genVertices;
                genMesh.triangles = genTriangles;
                GameObject genGo = new GameObject();
                MeshFilter genGoMeshFilter = genGo.AddComponent<MeshFilter>();
                genGoMeshFilter.mesh = genMesh;
                genGo.AddComponent<MeshRenderer>();
            }
        }
    }

    void OnDisconnect(int hostId, int connectionId)
    {
        byte error;
        NetworkTransport.Disconnect(hostId, connectionId, out error);
    }

    public void SendText()
    {
        string text = inputText.text;
        MemoryStream ms = new MemoryStream();
        BinaryFormatter bf = new BinaryFormatter();
        bf.Serialize(ms, text);
        byte[] buffer = ms.ToArray();
        Debug.Log("send buffer length " + buffer.Length);

        byte error;
        NetworkTransport.Send(hostId, connectionId, myStringSendingChannel, buffer, buffer.Length, out error);
    }

    public void SendModelData()
    {
        Mesh mesh = model.GetComponent<MeshFilter>().mesh;
        Debug.Log("local verts length" + mesh.vertices.Length);
        Debug.Log("local triangles length" + mesh.triangles.Length);

        MeshWireData meshWireData = new MeshWireData(mesh.vertices, mesh.triangles);

        byte[] data = Serialize(meshWireData);

        Debug.Log("data size " + data.Length);

        int finalFragIndex = data.Length - (data.Length % ChunkSize);
        byte[] fragment;
        byte error;
        for (int i = 0; i < finalFragIndex; i += ChunkSize)
        {
            fragment = data.Skip(i).Take(ChunkSize).ToArray();
            NetworkTransport.Send(hostId, connectionId, myReliableSequencedChannelId, fragment, fragment.Length, out error);
        }

        //final frag
        fragment = data.Skip(finalFragIndex).Take(ChunkSize).ToArray();
        NetworkTransport.Send(hostId, connectionId, myReliableSequencedChannelId, fragment, fragment.Length, out error);
    }

    public static byte[] Serialize<T>(T arg)
    {
        BinaryFormatter bf = new BinaryFormatter();
        MemoryStream ms = new MemoryStream();
        bf.Serialize(ms, arg);
        byte[] data = ms.ToArray();
        return data;
    }
}

[Serializable]
public class MeshWireData
{
    [SerializeField]
    public int verticesLength;

    [SerializeField]
    public float[,] vertices;

    [SerializeField]
    public int[] triangles;

    [SerializeField]
    public int trianglesLength;

    public MeshWireData(Vector3[] vertices, int[] triangles)
    {
        this.vertices = new float[vertices.Length, 3];
        this.verticesLength = vertices.Length;
        for (int i=0; i < vertices.Length; i++)
        {
            this.vertices[i, 0] = vertices[i].x;
            this.vertices[i, 1] = vertices[i].y;
            this.vertices[i, 2] = vertices[i].z;
        }

        this.triangles = new int[triangles.Length];
        this.trianglesLength = triangles.Length;
        for (int i=0; i < triangles.Length; i++)
        {
            this.triangles = triangles;
        }

        Debug.Log(this.vertices.Length);
        Debug.Log(this.triangles.Length);
    }
}


