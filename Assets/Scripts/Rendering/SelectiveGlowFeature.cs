using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Selective glow (URP Render Graph): objects with a GameJam/GlowSource material are drawn into a
/// half-resolution HDR buffer, blurred with a dual-Kawase pyramid (tight + wide), and added back to the
/// camera colour before post-processing - so it is tone-mapped and bloomed like real light.
/// </summary>
public class SelectiveGlowFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Shader shader;
        [Min(0f)] public float intensity = 1.3f;
        [Tooltip("Large, soft spread of light.")]
        [Min(0f)] public float wideWeight = 0.75f;
        [Tooltip("Tight glow right around the edges.")]
        [Min(0f)] public float narrowWeight = 0.9f;
        [Tooltip("How much the glow is reduced on top of the object itself (keeps the face readable).")]
        [Range(0f, 1f)] public float innerDim = 0.7f;
        [Range(0.5f, 3f)] public float spread = 1.35f;
        public RenderPassEvent passEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    public Settings settings = new Settings();

    Material material;
    GlowPass pass;

    static readonly int SpreadId = Shader.PropertyToID("_Spread");
    static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    static readonly int WideId = Shader.PropertyToID("_WideWeight");
    static readonly int NarrowWeightId = Shader.PropertyToID("_NarrowWeight");
    static readonly int InnerDimId = Shader.PropertyToID("_InnerDim");

    public override void Create()
    {
        if (settings.shader == null) settings.shader = Shader.Find("Hidden/GameJam/SelectiveGlow");
        if (settings.shader != null) material = CoreUtils.CreateEngineMaterial(settings.shader);
        pass = new GlowPass { renderPassEvent = settings.passEvent };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (material == null) return;
        var type = renderingData.cameraData.cameraType;
        if (type == CameraType.Preview || type == CameraType.Reflection) return;

        material.SetFloat(SpreadId, settings.spread);
        material.SetFloat(IntensityId, settings.intensity);
        material.SetFloat(WideId, settings.wideWeight);
        material.SetFloat(NarrowWeightId, settings.narrowWeight);
        material.SetFloat(InnerDimId, settings.innerDim);
        pass.Setup(material);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing) => CoreUtils.Destroy(material);

    class GlowPass : ScriptableRenderPass
    {
        static readonly ShaderTagId GlowTag = new ShaderTagId("GlowSource");
        static readonly int NarrowTexId = Shader.PropertyToID("_GlowNarrow");
        const int DownPass = 0, UpPass = 1, CompositePass = 2;

        Material material;

        public void Setup(Material m) => material = m;

        class SourceData
        {
            public RendererListHandle rendererList;
        }

        class CompositeData
        {
            public Material material;
            public TextureHandle wide;
            public TextureHandle narrow;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;          // needs an intermediate HDR colour target
            var cameraData = frameData.Get<UniversalCameraData>();
            var renderingData = frameData.Get<UniversalRenderingData>();
            var lightData = frameData.Get<UniversalLightData>();

            // derive every glow texture from the active colour target, changing only what must differ
            TextureDesc baseDesc = renderGraph.GetTextureDesc(resourceData.activeColorTexture);
            int width = baseDesc.sizeMode == TextureSizeMode.Explicit ? baseDesc.width : cameraData.cameraTargetDescriptor.width;
            int height = baseDesc.sizeMode == TextureSizeMode.Explicit ? baseDesc.height : cameraData.cameraTargetDescriptor.height;

            TextureHandle Make(string name, int divisor, bool clear)
            {
                TextureDesc d = baseDesc;
                d.sizeMode = TextureSizeMode.Explicit;
                d.width = Mathf.Max(1, width / divisor);
                d.height = Mathf.Max(1, height / divisor);
                d.format = GraphicsFormat.R16G16B16A16_SFloat;
                d.msaaSamples = MSAASamples.None;
                d.useMipMap = false;
                d.enableRandomWrite = false;
                d.filterMode = FilterMode.Bilinear;
                d.wrapMode = TextureWrapMode.Clamp;
                d.clearBuffer = clear;
                d.clearColor = Color.clear;
                d.name = name;
                return renderGraph.CreateTexture(d);
            }

            // 1) glow sources -> half-res HDR buffer (alpha = silhouette)
            TextureHandle source = Make("_GlowSource", 2, true);
            using (var builder = renderGraph.AddRasterRenderPass<SourceData>("Glow Source", out var data))
            {
                DrawingSettings drawing = CreateDrawingSettings(GlowTag, renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
                var filtering = new FilteringSettings(RenderQueueRange.all);
                data.rendererList = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, drawing, filtering));
                builder.UseRendererList(data.rendererList);
                builder.SetRenderAttachment(source, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (SourceData d, RasterGraphContext ctx) => ctx.cmd.DrawRendererList(d.rendererList));
            }

            // 2) dual-Kawase pyramid: 1/4 .. 1/32 down, then back up to 1/2
            TextureHandle d1 = Make("_GlowDown1", 4, false);
            TextureHandle d2 = Make("_GlowDown2", 8, false);
            TextureHandle d3 = Make("_GlowDown3", 16, false);
            TextureHandle d4 = Make("_GlowDown4", 32, false);
            TextureHandle u3 = Make("_GlowUp3", 16, false);
            TextureHandle u2 = Make("_GlowUp2", 8, false);
            TextureHandle u1 = Make("_GlowUp1", 4, false);
            TextureHandle u0 = Make("_GlowUp0", 2, false);
            Blit(renderGraph, source, d1, DownPass, "Glow Down 1/4");
            Blit(renderGraph, d1, d2, DownPass, "Glow Down 1/8");
            Blit(renderGraph, d2, d3, DownPass, "Glow Down 1/16");
            Blit(renderGraph, d3, d4, DownPass, "Glow Down 1/32");
            Blit(renderGraph, d4, u3, UpPass, "Glow Up 1/16");
            Blit(renderGraph, u3, u2, UpPass, "Glow Up 1/8");
            Blit(renderGraph, u2, u1, UpPass, "Glow Up 1/4");
            Blit(renderGraph, u1, u0, UpPass, "Glow Up 1/2");

            // 3) wide (u0) + tight (d1), dimmed over the soft silhouette (d1 alpha), added to the HDR colour
            using (var builder = renderGraph.AddRasterRenderPass<CompositeData>("Glow Composite", out var data))
            {
                data.material = material;
                data.wide = u0;
                data.narrow = d1;
                builder.UseTexture(u0, AccessFlags.Read);
                builder.UseTexture(d1, AccessFlags.Read);
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.SetRenderFunc(static (CompositeData d, RasterGraphContext ctx) =>
                {
                    d.material.SetTexture(NarrowTexId, (RTHandle)d.narrow);
                    Blitter.BlitTexture(ctx.cmd, d.wide, new Vector4(1f, 1f, 0f, 0f), d.material, CompositePass);
                });
            }
        }

        void Blit(RenderGraph renderGraph, TextureHandle src, TextureHandle dst, int shaderPass, string name)
        {
            renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(src, dst, material, shaderPass), name);
        }
    }
}
