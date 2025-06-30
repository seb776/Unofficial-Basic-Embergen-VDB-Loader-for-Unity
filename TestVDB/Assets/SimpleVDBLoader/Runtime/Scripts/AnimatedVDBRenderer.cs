using UnityEngine;

public class AnimatedVDBRenderer : MonoBehaviour
{
    int HasChangedInt = -1;
    float CurFrame = 0;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        VolumeShader.SetInt("CurFrame", (int)Mathf.Floor(CurFrame));
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
