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
    public Material dummyMaterial;

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

    List<byte> receivedBytes = new List<byte>();
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
            receivedBytes.AddRange(addBytes);

            //TODO: Will break if send a modelMeshData thats a multiple of 1024 bytes
            if (recSize < ChunkSize)
            {
                byte[] totalData = receivedBytes.ToArray();
                BinaryFormatter bf = new BinaryFormatter();
                MemoryStream ms = new MemoryStream(totalData);
                ModelWireData modelWireData = bf.Deserialize(ms) as ModelWireData;
                Debug.Log(modelWireData.verticesLength);
                Debug.Log(modelWireData.trianglesLength);

                //loop 2d float array, make into vectors and add to new vertices array
                Vector3[] genVertices = new Vector3[modelWireData.verticesLength];
                for (int i=0; i < modelWireData.verticesLength; i++)
                {
                    //0 = x, 1 = y, 2 = z
                    genVertices[i] = new Vector3(modelWireData.vertices[i, 0], modelWireData.vertices[i, 1], modelWireData.vertices[i, 2]);
                }
                Debug.Log(genVertices.Length);

                //assign received triangle array to genTriangles array
                int[] genTriangles = modelWireData.triangles;
                Debug.Log(genTriangles.Length);

                //creating mesh with the generated vertices and triangles
                Mesh genMesh = new Mesh
                {
                    vertices = genVertices,
                    triangles = genTriangles
                };

                //calculating mesh properties for rendering purposes
                genMesh.RecalculateNormals();
                genMesh.RecalculateBounds();
                genMesh.RecalculateTangents();

                //Adding generated mesh to container game object
                GameObject genGo = new GameObject
                {
                    name = "generatedModel",
                    tag = "model",
                };
                MeshFilter genGoMeshFilter = genGo.AddComponent<MeshFilter>();
                genGoMeshFilter.mesh = genMesh;

                //Adding mesh renderer and assigning generated mesh to mesh renderer.
                MeshRenderer genGoMeshRenderer = genGo.AddComponent<MeshRenderer>();

                //Generate new empty material with standard shader.
                Material genMaterial = genGoMeshRenderer.material = new Material(Shader.Find("Standard"));
                genMaterial.name = "generatedMaterial";
                //Generate colour from float array [r=0, g=1, b=2, g=3]
                Color genColour = new Color(modelWireData.materialColour[0], modelWireData.materialColour[1], modelWireData.materialColour[2], modelWireData.materialColour[3]);
                genMaterial.color = genColour;
                genMaterial.SetFloat("_Glossiness", modelWireData.materialGlossiness);
                genMaterial.SetFloat("_Metallic", modelWireData.materialMetallic);

                
                byte[] texBytes = modelWireData.textureData;
                TextureFormat textureFormat = (TextureFormat)modelWireData.textureFormat;
                Texture2D genTex2D = new Texture2D(modelWireData.textureWidth, modelWireData.textureHeight, textureFormat, false);
                genTex2D.LoadRawTextureData(texBytes);
                Debug.Log("generated texture" + genTex2D.width + " " + genTex2D.height);
                genMaterial.mainTexture = genTex2D;
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
        // Extracting mesh to use in constructor
        Mesh mesh = model.GetComponent<MeshFilter>().mesh;
        Debug.Log("local verts length" + mesh.vertices.Length);
        Debug.Log("local triangles length" + mesh.triangles.Length);

        // Extracting material settings/properties to use in constructor
        Material material = model.GetComponent<MeshRenderer>().material;
        float[] materialColour = { material.color.r, material.color.g, material.color.b, material.color.a };
        float materialGlossiness = material.GetFloat("_Glossiness");
        float materialMetallic= material.GetFloat("_Metallic");

        Texture2D texture2D = (Texture2D)material.mainTexture;
        int textureWidth = texture2D.width;
        int textureHeight = texture2D.height;
        TextureFormat textureFormat = texture2D.format;
        byte[] textureData = texture2D.GetRawTextureData();

        //TODO Need to include the texture settings so the logo etc. can get mapped properly
        //TODO: Also need to include the model UV (at the moment just the first for texture
        // ... mapping.

        //Construct modelWireData from the properties extracted.
        ModelWireData modelWireData = new ModelWireData(mesh.vertices, mesh.triangles, materialColour, materialGlossiness, materialMetallic, textureData, textureWidth, textureHeight, textureFormat);

        byte[] data = Serialize(modelWireData);

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

//TODO: Add more properties to models to send.
[Serializable]
public class ModelWireData
{

    //Mesh
    [SerializeField]
    public int verticesLength;

    [SerializeField]
    public float[,] vertices;

    [SerializeField]
    public int[] triangles;

    [SerializeField]
    public int trianglesLength;


    //Material
    [SerializeField]
    public float[] materialColour;

    [SerializeField]
    public float materialGlossiness, materialMetallic;

    [SerializeField]
    public byte[] textureData;

    [SerializeField]
    public int textureWidth, textureHeight, textureFormat;

    public ModelWireData(Vector3[] vertices, int[] triangles, float[] materialColour, float materialGlossiness, float materialMetallic, byte[] textureData, int textureWidth, int textureHeight, TextureFormat textureFormat)
    {
        //Mesh Parameters
        //creating 2d float array for v3s
        this.vertices = new float[vertices.Length, 3];
        this.verticesLength = vertices.Length;
        for (int i=0; i < vertices.Length; i++)
        {
            this.vertices[i, 0] = vertices[i].x;
            this.vertices[i, 1] = vertices[i].y;
            this.vertices[i, 2] = vertices[i].z;
        }

        //constructing int[]
        this.triangles = new int[triangles.Length];
        this.trianglesLength = triangles.Length;
        for (int i=0; i < triangles.Length; i++)
        {
            this.triangles = triangles;
        }

        //confirmation constructor worked.
        Debug.Log(this.vertices.Length);
        Debug.Log(this.triangles.Length);

        //Material Parameters
        this.materialColour = materialColour;
        this.materialGlossiness = materialGlossiness;
        this.materialMetallic = materialMetallic;

        this.textureData = textureData;
        this.textureWidth = textureWidth;
        this.textureHeight = textureHeight;

        this.textureFormat = (int)textureFormat;
    }
}


