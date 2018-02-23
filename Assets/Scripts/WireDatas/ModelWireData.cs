using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ModelWireData : TransformWireData
{
    //Mesh
    [SerializeField]
    public int verticesLength, trianglesLength, uvsLength;

    [SerializeField]
    public float[,] vertices;

    [SerializeField]
    public float[,] uvs;

    [SerializeField]
    public int[] triangles;

    //Material
    [SerializeField]
    public float[] materialColour;

    [SerializeField]
    public float materialGlossiness, materialMetallic;

    [SerializeField]
    public byte[] textureData;

    [SerializeField]
    public int textureWidth, textureHeight, textureFormat;

    [SerializeField]
    public TransformWireData transform;

    public ModelWireData(Vector3[] vertices, Vector2[] uvs, int[] triangles, float[] materialColour, float materialGlossiness, float materialMetallic, byte[] textureData, int textureWidth, int textureHeight, TextureFormat textureFormat, Transform transform) : base (transform)
    {
        //Mesh Parameters
        //creating 2d float array for v3s
        this.vertices = new float[vertices.Length, 3];
        this.verticesLength = vertices.Length;
        for (int i = 0; i < vertices.Length; i++)
        {
            this.vertices[i, 0] = vertices[i].x;
            this.vertices[i, 1] = vertices[i].y;
            this.vertices[i, 2] = vertices[i].z;
        }

        //constructing v2 uv[]
        this.uvs = new float[uvs.Length, 2];
        this.uvsLength = uvs.Length;
        for (int i = 0; i < uvs.Length; i++)
        {
            this.uvs[i, 0] = uvs[i].x;
            this.uvs[i, 1] = uvs[i].y;
        }

        //constructing int[]
        this.triangles = new int[triangles.Length];
        this.trianglesLength = triangles.Length;
        for (int i = 0; i < triangles.Length; i++)
        {
            this.triangles = triangles;
        }

        //confirmation constructor worked.
        Debug.Log("Finished constructing vertices of length: " + this.vertices.Length);
        Debug.Log("Finished constructing uv of length: " + this.uvs.Length);
        Debug.Log("Finished constructing triangles of length: " + this.triangles.Length);

        //Material Parameters
        this.materialColour = materialColour;
        this.materialGlossiness = materialGlossiness;
        this.materialMetallic = materialMetallic;

        this.textureData = textureData;
        this.textureWidth = textureWidth;
        this.textureHeight = textureHeight;

        this.textureFormat = (int)textureFormat;

        //transform
        this.transform = new TransformWireData(transform);
    }
}
