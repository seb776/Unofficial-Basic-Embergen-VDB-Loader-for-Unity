using UnityEngine;

public class AnimatedVDBRenderer : MonoBehaviour, IVDBRenderer
{
    int HasChangedInt = -1;

    public int RenderOrder { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
    public VDBFileContent Asset { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }


    private int _curFrame = 0;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        VolumeShader.SetInt("CurFrame", (int)Mathf.Floor(CurFrame)); // TODO move this to renderer feature
        int i = (int)Mathf.Floor(CurFrame) % (ValidVoxelSitesBuffer.Length);
        if (HasChangedInt != -1)
        {
            if (HasChangedInt == i)
            {
                HasChangedInt = -1;
            }
        }
        else if (HasChanged) HasChangedInt = i;
        CurFrame += 1.0f;
    }
}
