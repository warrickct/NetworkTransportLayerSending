using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class NetTest : MonoBehaviour {

    int connectionId;
    int channelId;
    int hostId;

    void Start()
    {
        // Init Transport using default values.
        NetworkTransport.Init();

        // Create a connection config and add a Channel.
        ConnectionConfig config = new ConnectionConfig();
        channelId = config.AddChannel(QosType.Reliable);

        // Create a topology based on the connection config.
        HostTopology topology = new HostTopology(config, 10);

        // Create a host based on the topology we just created, and bind the socket to port 12345.
        hostId = NetworkTransport.AddHost(topology, 12345);

        // Connect to the host with IP 10.0.0.42 and port 54321
        byte error;
        connectionId = NetworkTransport.Connect(hostId, "127.0.0.1", 12345, 0, out error);
    }

    void Update()
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
            case NetworkEventType.DisconnectEvent:
                if (outHostId == hostId &&
                    outConnectionId == connectionId)
                {
                    Debug.Log("Connected, error:" + error.ToString());
                }
                break;
        }
    }
}
