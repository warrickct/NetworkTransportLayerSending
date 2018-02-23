using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    /// <summary>
    /// Applies transform wiredata coordinates to a object transform. Bool to include scale.
    /// </summary>
    /// <param name="transformWireData"></param>
    /// <param name="transformTarget"></param>
    /// <param name="includeScale"></param>
    public static void ApplyTransform(TransformWireData transformWireData, Transform transformTarget, bool includeScale)
    {
        Vector3 newPosition = new Vector3(transformWireData.posX, transformWireData.posY, transformWireData.posZ);
        transformTarget.position = newPosition;

        Vector3 v3rotation = new Vector3(transformWireData.rotX, transformWireData.rotY, transformWireData.rotZ);
        Quaternion newRotation = Quaternion.Euler(v3rotation);
        transformTarget.localRotation = newRotation;

        if (includeScale)
        {
            Vector3 newScale = new Vector3(transformWireData.scaX, transformWireData.scaY, transformWireData.scaZ);
            transformTarget.localScale = newScale;
        }
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

