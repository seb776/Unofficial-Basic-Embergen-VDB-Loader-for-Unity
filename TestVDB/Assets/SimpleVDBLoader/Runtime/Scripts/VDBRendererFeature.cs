//using UnityEngine.Rendering.RenderGraphModule;
//using UnityEngine.Rendering;
//using UnityEngine.Rendering.Universal;
//using UnityEngine;

//class VDBPass : ScriptableRenderPass
//{
//    public GraphicsBuffer outputBuffer;

//    // Add a handle to the output buffer in your pass data
//    class PassData
//    {
//        public BufferHandle output;
//    }

//    // Create the buffer in the render pass constructor
//    public VDBPass(ComputeShader computeShader)
//    {
//        // Create the output buffer as a structured buffer
//        // Create the buffer with a length of 5 integers, so the compute shader can output 5 values.
//        outputBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 5, sizeof(int));
//    }
//    private void ExecutePass(PassData data, ComputeGraphContext context)
//    {

//    }
//    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer contextData)
//    {
//        // Use AddComputePass instead of AddRasterRenderPass.
//        using (var builder = renderGraph.AddComputePass("MyComputePass", out PassData data))
//        {
//            // Use ComputeGraphContext instead of RasterGraphContext.
//            builder.SetRenderFunc((PassData data, ComputeGraphContext context) => ExecutePass(data, context));
//        }
//    }
//}

//// https://docs.unity3d.com/6000.1/Documentation/Manual/urp/render-graph-compute-shader-run.html
//public class VDBRendererFeature : ScriptableRendererFeature
//{
//    private VDBPass _vdbPass;

//    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
//    {
//        throw new System.NotImplementedException();
//    }
//    public override void Create()
//    {
//        if (_vdbPass == null)
//        {
//            //_vdbPass = new VDBPass();
//        }
//    }
//}