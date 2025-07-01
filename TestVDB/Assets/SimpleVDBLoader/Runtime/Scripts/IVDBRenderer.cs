using UnityEngine;

abstract class AVDBRenderer : MonoBehaviour
{
    public abstract int RenderOrder { get; set; }
    public abstract VDBFileContent Asset { get; set; } // Equivalent of mesh in meshrenderer
    public abstract VDBMaterial VDBMaterial { get; set; } // Equivalent of material in meshrenderer


    public abstract ComputeBuffer GetShadowBuffer();
    public abstract Vector3 GetSize();
}