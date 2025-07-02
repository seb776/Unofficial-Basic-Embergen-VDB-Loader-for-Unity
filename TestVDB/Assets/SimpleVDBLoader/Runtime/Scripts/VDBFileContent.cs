using System;
using UnityEngine;

[Serializable]
public class VDBFileContent
{
    public byte[] FileContent; // TODO This can be removed if we only use the parsed content
    public Vector4[] NonZeroVoxels;
    public OpenVDBReader VDBContent;
}