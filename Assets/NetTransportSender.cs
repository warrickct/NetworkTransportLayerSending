using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.Networking;

public class NetTransportSender : MonoBehaviour {

    public GameObject playerPrefab;

    public int port;
    public string connectionTargetIp;

    public Transform head;
    public Transform leftHand;
    public Transform rightHand;

    private const int ChunkSize = 1024;

    private int hostId;
    private int connectionId;
    private int myUnreliableChannelId;
    private int myUnreliableSequencedChannelId;

    private void Start()
    {
        StartHost();
        ConnectToHost();
    }

    private void Update()
    {
        ReceiveData();
        SendPosition();
    }

    private void ReceiveData()
    {
        int outHostId;
        int outConnectionId;
        int outChannelId;

        byte[] buffer = new byte[ChunkSize];
        int bufferSize = buffer.Length;
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

    private void OnConnect(int outHostId, int outConnectionId, NetworkError error)
    {
        if (outHostId == hostId &&
            outConnectionId == connectionId &&
            (NetworkError)error == NetworkError.Ok)
        {
            Debug.Log("Connected");
        }
    }

    private void OnData(int recHostId, int recConnectionId, int recChannelId, byte[] recData, int recSize, NetworkError recError)
    {
        MemoryStream memoryStream = new MemoryStream(recData);
        BinaryFormatter binaryFormatter = new BinaryFormatter();
        PlayerWireData receivedPlayerWireData = binaryFormatter.Deserialize(memoryStream) as PlayerWireData;

        //If player doesn't exist in game then create one
        GameObject player = GameObject.Find(receivedPlayerWireData.playerDeviceId);
        if (player == null)
        {
            player = Instantiate(playerPrefab);
            player.name = receivedPlayerWireData.playerDeviceId;
            player.tag = "Player";
        }

        //set transforms of the new player
        TransformWireData[] setTransforms = receivedPlayerWireData.playerTransformWireData;
        for (int i = 0; i < setTransforms.Length; i++)
        {
            TransformWireData transform = setTransforms[i];
            Vector3 position = new Vector3(transform.posX, transform.posY, transform.posZ);
            Vector3 v3rotation = new Vector3(transform.rotX, transform.rotY, transform.rotZ);
            Quaternion rotation = Quaternion.Euler(v3rotation);
            Vector3 scale = new Vector3(transform.scaX, transform.scaY, transform.scaZ);
            player.transform.GetChild(i).position = position;
            player.transform.GetChild(i).rotation = rotation;
            player.transform.GetChild(i).localScale = scale;
        }
    }

    void OnDisconnect(int hostId, int connectionId)
    {
        byte error;
        NetworkTransport.Disconnect(hostId, connectionId, out error);
    }

    private void StartHost()
    {
        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();

        myUnreliableChannelId = config.AddChannel(QosType.Unreliable);
        myUnreliableSequencedChannelId = config.AddChannel(QosType.UnreliableSequenced);

        HostTopology topology = new HostTopology(config, 10);
        hostId = NetworkTransport.AddHost(topology, port);
    }

    private void ConnectToHost()
    {
        byte error;
        connectionId = NetworkTransport.Connect(hostId, connectionTargetIp, port, 0, out error);
    }

    private void SendPosition()
    {
        TransformWireData transformWireDataHead = new TransformWireData(head);
        TransformWireData transformWireDataLeft = new TransformWireData(leftHand);
        TransformWireData transformWireDataRight = new TransformWireData(rightHand);

        string playerDeviceId = SystemInfo.deviceUniqueIdentifier;
        PlayerWireData playerWireData = new PlayerWireData(playerDeviceId, transformWireDataHead, transformWireDataLeft, transformWireDataRight);

        byte[] data = NetSender.Serialize(playerWireData);

        byte error;
        NetworkTransport.Send(hostId, connectionId, myUnreliableChannelId, data, data.Length, out error);
    }
}

[Serializable]
public class TransformWireData
{
    [SerializeField]
    public float posX, posY, posZ, rotX, rotY, rotZ, scaX, scaY, scaZ;

    public TransformWireData(Transform transform)
    {
        this.posX = transform.position.x;
        this.posY = transform.position.y;
        this.posZ = transform.position.z;
        this.rotX = transform.rotation.x;
        this.rotY = transform.rotation.y;
        this.rotZ = transform.rotation.z;
        this.scaX = transform.localScale.x;
        this.scaY = transform.localScale.y;
        this.scaZ = transform.localScale.z;
    }

    public override string ToString()
    {
        string s = String.Format(
            "postion x: {0}, position y: {1}, position z: {2} \n" +
            "rotation x: {3}, rotation y: {4}, rotation z: {5} \n" +
            "scale x: {6}, scale y: {7}, scale z: {8}",
            posX, posY, posZ, rotX, rotY, rotZ, scaX, scaY, scaZ
            );
        return s;
    }
}

[Serializable]
public class PlayerWireData
{
    [SerializeField]
    public string playerDeviceId;

    [SerializeField]
    public TransformWireData[] playerTransformWireData = new TransformWireData[3];

    public PlayerWireData(string playerDeviceId, TransformWireData head, TransformWireData left, TransformWireData right)
    {
        this.playerDeviceId = playerDeviceId;
        this.playerTransformWireData[0] = head;
        this.playerTransformWireData[1] = left;
        this.playerTransformWireData[2] = right;
    }

    public override string ToString()
    {
        string s = String.Format("player device id {0}", playerDeviceId);
        return s;
    }
}
