using UnityEngine;

[System.Serializable]
public struct UnityLight
{
    public Vector3 Position;
    public Vector3 Direction;
    public int Type;
    public Vector3 Col; // TODO rename Color
}