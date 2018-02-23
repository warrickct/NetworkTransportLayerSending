using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

