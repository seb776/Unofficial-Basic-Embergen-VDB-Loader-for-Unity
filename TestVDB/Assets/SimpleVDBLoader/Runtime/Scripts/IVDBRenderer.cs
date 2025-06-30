interface IVDBRenderer
{
    public int RenderOrder { get; set; }
    public VDBFileContent Asset { get; set; } // Equivalent of mesh in meshrenderer
}