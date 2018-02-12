using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System;

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

    // Use this for initialization
    void Start () {
        StartHost();
        ConnectToHost();
	}
	
	// Update is called once per frame
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
        myReliableChannelId = config.AddChannel(QosType.Reliable);

        // Create a topology based on the connection config.
        HostTopology topology = new HostTopology(config, 10);

        // Create a host based on the topology we just created, and bind the socket to port 12345.
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
        int bufferLength = buffer.Length;
        NetworkTransport.Send(hostId, connectionId, myReliableChannelId, buffer, bufferLength, out error);
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
        string decodedData = System.Text.ASCIIEncoding.ASCII.GetString(buffer);
        displayText.text = decodedData.ToUpper();
        Debug.Log(decodedData.ToUpper());
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

    public void SendModel()
    {
        Mesh mesh = model.GetComponent<MeshFilter>().mesh;
        Vector3[] vertices = mesh.vertices;
        Vector2[] uvs = mesh.uv;
        int[] triangles = mesh.triangles;

        //todo: convert model data to byte[] then send as buffer.
    }
}

[Serializable]
public class ModelData
{

    //todo: convert to lists to be dynamically added.
    public float[,,] vertices;
    public float[,] uvs;
    public int[] triangles;


    //todo: loop through each array and add as you go.
    public ModelData(Vector3[] verts, Vector2[] uvs, int[] tris)
    {
        //loop here
    }
}
