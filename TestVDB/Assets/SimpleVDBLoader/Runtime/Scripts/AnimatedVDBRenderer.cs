using UnityEngine;

public class AnimatedVDBRenderer : AVDBRenderer
{
    int HasChangedInt = -1;

    public override int RenderOrder { get; set; }
    public override VDBFileContent Asset { get; set; }
    public override VDBMaterial VDBMaterial { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

    private int _curFrame = 0;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        //VolumeShader.SetInt("CurFrame", _curFrame); // TODO move this to renderer feature
        //int i = _curFrame % (ValidVoxelSitesBuffer.Length);
        //if (HasChangedInt != -1)
        //{
        //    if (HasChangedInt == i)
        //    {
        //        HasChangedInt = -1;
        //    }
        //}
        //else if (HasChanged) HasChangedInt = i;
        //_curFrame += 1;
    }

    public override ComputeBuffer GetShadowBuffer()
    {
        throw new System.NotImplementedException();
    }

    public override Vector3 GetSize()
    {
        throw new System.NotImplementedException();
    }
}
