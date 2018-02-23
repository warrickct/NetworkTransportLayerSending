using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.Networking;

public class NetworkTransportManager : MonoBehaviour {

    public int port;
    public string targetIp;
    
    public int hostId;
    public int connId;

    public int unreliableChannelId;
    public int reliableFragmentedSequencedChannelId;

    public const int ChunkSize = 1024;

    public void StartHost()
    {
        NetworkTransport.Init();

        //setting up config for large files.
        ConnectionConfig config = new ConnectionConfig();
        config.PacketSize = 1400;
        config.MaxSentMessageQueueSize = 256;
        config.FragmentSize = 1024;
        

        unreliableChannelId = config.AddChannel(QosType.Unreliable);
        reliableFragmentedSequencedChannelId = config.AddChannel(QosType.ReliableFragmentedSequenced);

        config.MaxCombinedReliableMessageCount = 60;
        config.MaxCombinedReliableMessageSize = 1024;

        HostTopology topology = new HostTopology(config, 10);

        //might need to add another host for different channel
        hostId = NetworkTransport.AddHost(topology, port);
    }

    public void GetConnection()
    {
        byte error;
        connId = NetworkTransport.Connect(hostId, targetIp, port, 0, out error);
    }

    public void SendUnreliableData(byte[] data)
    {
        byte error;
        NetworkTransport.Send(hostId, connId, unreliableChannelId, data, data.Length, out error);
    }

    public void SendReliableData(byte[] data)
    {
        int finalFragIndex = data.Length - (data.Length % ChunkSize);
        byte[] fragment;
        byte error;
        for (int i = 0; i < finalFragIndex; i += ChunkSize)
        {
            fragment = data.Skip(i).Take(ChunkSize).ToArray();
            NetworkTransport.Send(hostId, connId, reliableFragmentedSequencedChannelId, fragment, fragment.Length, out error);
        }
        fragment = data.Skip(finalFragIndex).Take(ChunkSize).ToArray();
        NetworkTransport.Send(hostId, connId, reliableFragmentedSequencedChannelId, fragment, fragment.Length, out error);
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
