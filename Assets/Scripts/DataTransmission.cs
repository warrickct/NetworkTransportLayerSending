using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.Networking;

public class DataTransmission : MonoBehaviour {

    public GameObject modelToSend;

    public Transform head;
    public Transform leftHand;
    public Transform rightHand;

    public GameObject playerPrefab;

    public const int ChunkSize = 1024;

    public NetworkTransportManager netTransportManager;

    private void Start()
    {
        netTransportManager.StartHost();
        netTransportManager.GetConnection();
    }

    public void Update()
    {
        ReceiveData();
        SendPlayerPosition();
    }

    private void SendPlayerPosition()
    {
        TransformWireData transformWireDataHead = new TransformWireData(head);
        TransformWireData transformWireDataLeft = new TransformWireData(leftHand);
        TransformWireData transformWireDataRight = new TransformWireData(rightHand);

        string playerDeviceId = SystemInfo.deviceUniqueIdentifier;
        PlayerWireData playerWireData = new PlayerWireData(playerDeviceId, transformWireDataHead, transformWireDataLeft, transformWireDataRight);

        byte[] data = NetworkTransportManager.Serialize(playerWireData);
        netTransportManager.SendUnreliableData(data);
    }

    public void SendModel()
    {
        // Extracting mesh to use in constructor
        Mesh mesh = modelToSend.GetComponent<MeshFilter>().mesh;

        // Extracting material settings/properties to use in constructor
        Material material = modelToSend.GetComponent<MeshRenderer>().material;
        float[] materialColour = { material.color.r, material.color.g, material.color.b, material.color.a };
        float materialGlossiness = material.GetFloat("_Glossiness");
        float materialMetallic = material.GetFloat("_Metallic");

        Texture2D texture2D = (Texture2D)material.mainTexture;
        int textureWidth = texture2D.width;
        int textureHeight = texture2D.height;
        TextureFormat textureFormat = texture2D.format;
        byte[] textureData = texture2D.GetRawTextureData();

        //Construct modelWireData from the properties extracted.
        ModelWireData modelWireData = new ModelWireData(mesh.vertices, mesh.uv, mesh.triangles, materialColour, materialGlossiness, materialMetallic, textureData, textureWidth, textureHeight, textureFormat, modelToSend.transform);

        byte[] data = NetworkTransportManager.Serialize(modelWireData);
        netTransportManager.SendReliableData(data);
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
                Debug.Log("connected");
                break;
            case NetworkEventType.DataEvent:
                HandleData(outHostId, outConnectionId, outChannelId, buffer, receiveSize, (NetworkError)error);
                break;
            case NetworkEventType.DisconnectEvent:
                Debug.Log("Disconnect event");
                break;
        }
    }

    List<byte> receivedBytes = new List<byte>();
    public void HandleData(int hostId, int connId, int chanId, byte[] data, int dataSize, NetworkError error)
    {
        if (chanId == netTransportManager.reliableFragmentedSequencedChannelId)
        {
            List<byte> addBytes = new List<byte>(data);
            receivedBytes.AddRange(addBytes);

            //TODO: Will break if send a modelMeshData thats a multiple of 1024 bytes
            if (dataSize < ChunkSize)
            {
                byte[] totalData = receivedBytes.ToArray();
                BinaryFormatter bf = new BinaryFormatter();
                MemoryStream ms = new MemoryStream(totalData);
                ModelWireData modelWireData = bf.Deserialize(ms) as ModelWireData;
                Debug.Log(modelWireData.verticesLength);
                Debug.Log(modelWireData.trianglesLength);

                //loop 2d float array, make into vectors and add to new vertices array
                Vector3[] genVertices = new Vector3[modelWireData.verticesLength];
                for (int i = 0; i < modelWireData.verticesLength; i++)
                {
                    //0 = x, 1 = y, 2 = z
                    genVertices[i] = new Vector3(modelWireData.vertices[i, 0], modelWireData.vertices[i, 1], modelWireData.vertices[i, 2]);
                }
                Debug.Log("generated vertices length: " + genVertices.Length);

                //loop through uvs into v2 array
                Vector2[] genUvs = new Vector2[modelWireData.uvsLength];
                for (int i = 0; i < modelWireData.uvsLength; i++)
                {
                    //0 = uv.x, 1 = uv.y
                    genUvs[i] = new Vector2(modelWireData.uvs[i, 0], modelWireData.uvs[i, 1]);
                }
                Debug.Log("generated uv length: " + genUvs.Length);

                //assign received triangle array to genTriangles array
                int[] genTriangles = modelWireData.triangles;
                Debug.Log("Generated triangles " + genTriangles.Length);

                //creating mesh with the generated vertices and triangles
                Mesh genMesh = new Mesh
                {
                    vertices = genVertices,
                    uv = genUvs,
                    triangles = genTriangles
                };

                //calculating mesh properties for rendering purposes
                genMesh.RecalculateNormals();
                genMesh.RecalculateBounds();
                genMesh.RecalculateTangents();

                //Adding generated mesh to container game object
                GameObject genGo = new GameObject
                {
                    name = "GeneratedModel",
                    tag = "Model"
                };
                MeshFilter genGoMeshFilter = genGo.AddComponent<MeshFilter>();
                genGoMeshFilter.mesh = genMesh;

                //Adding mesh renderer and assigning generated mesh to mesh renderer.
                MeshRenderer genGoMeshRenderer = genGo.AddComponent<MeshRenderer>();

                //Material reconstruction
                Material genMaterial = genGoMeshRenderer.material = new Material(Shader.Find("Standard"));
                genMaterial.name = "generatedMaterial";
                //Generate colour from float array [r=0, g=1, b=2, g=3]
                Color genColour = new Color(modelWireData.materialColour[0], modelWireData.materialColour[1], modelWireData.materialColour[2], modelWireData.materialColour[3]);
                genMaterial.color = genColour;
                genMaterial.SetFloat("_Glossiness", modelWireData.materialGlossiness);
                genMaterial.SetFloat("_Metallic", modelWireData.materialMetallic);

                //Texture reconstruction
                byte[] texBytes = modelWireData.textureData;
                TextureFormat textureFormat = (TextureFormat)modelWireData.textureFormat;
                Texture2D genTex2D = new Texture2D(modelWireData.textureWidth, modelWireData.textureHeight, textureFormat, false);
                genTex2D.LoadRawTextureData(texBytes);
                Debug.Log("generated texture" + genTex2D.width + " " + genTex2D.height);
                genMaterial.mainTexture = genTex2D;
                genTex2D.Apply();

                //Transform reconstruction
                TransformWireData.ApplyTransform(modelWireData.transform, genGo.transform, true);

                //Empty list for new model
                receivedBytes.Clear();
            }
        }
        else if (chanId == netTransportManager.unreliableChannelId)
        {
            MemoryStream memoryStream = new MemoryStream(data);
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
            TransformWireData[] transformsArray = receivedPlayerWireData.playerTransformWireData;
            for (int i = 0; i < transformsArray.Length; i++)
            {
                TransformWireData.ApplyTransform(transformsArray[i], player.transform.GetChild(i), false);
            }
        }
    }
}
