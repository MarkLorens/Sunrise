using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

// The tutorial's light on something in the park: everything goes dark (Darkness) except the sprites in Lit, which stay
// clear in their own shape with a soft glow of light round them (Resources/TutorialSpotlight.shader). Drawn over the
// finished frame, under the HUD, and injected from here like ActionOutline, so it works on every renderer.
// Tutorial adds it and says what to light.
public class TutorialSpotlight : MonoBehaviour
{
    public static readonly List<Renderer> Lit = new();
    public static float Darkness; // 0 = off

    public Color shade = Color.black; // plain dark, no tint (the park's own colours already show through faintly)
    public float glow = 60f; // how far the light spills round the shape, in pixels on a 1080p screen

    static readonly int ShadeId = Shader.PropertyToID("_ShadeColor");
    static readonly int DarknessId = Shader.PropertyToID("_Darkness");
    static readonly int GlowId = Shader.PropertyToID("_GlowStep");

    Material material;
    MaskPass maskPass;
    DimPass dimPass;

    void Awake()
    {
        material = CoreUtils.CreateEngineMaterial(Resources.Load<Shader>("TutorialSpotlight"));
        maskPass = new MaskPass(material);
        dimPass = new DimPass(material);
    }

    void OnEnable() => RenderPipelineManager.beginCameraRendering += Enqueue;

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= Enqueue;
        Lit.Clear();
        Darkness = 0f;
    }

    void OnDestroy() => CoreUtils.Destroy(material);

    void Enqueue(ScriptableRenderContext context, Camera cam)
    {
        if (Darkness <= 0.001f || cam.cameraType != CameraType.Game || material == null) return;
        float up = glow / 1080f;
        material.SetColor(ShadeId, shade);
        material.SetFloat(DarknessId, Darkness);
        material.SetVector(GlowId, new Vector4(up / cam.aspect, up));
        var renderer = cam.GetUniversalAdditionalCameraData().scriptableRenderer;
        renderer.EnqueuePass(maskPass);
        renderer.EnqueuePass(dimPass);
    }

    class MaskData : ContextItem
    {
        public TextureHandle mask;
        public override void Reset() => mask = TextureHandle.nullHandle;
    }

    // Each lit sprite's shape, white into a mask, once the scene is drawn.
    class MaskPass : ScriptableRenderPass
    {
        class PassData
        {
            public List<Renderer> renderers;
            public Material material;
        }

        readonly Material material;

        public MaskPass(Material material)
        {
            this.material = material;
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
        {
            var target = frame.Get<UniversalCameraData>().cameraTargetDescriptor;
            var shared = frame.Create<MaskData>();
            // Half size: it's only ever blurred into a soft glow, and a quarter of the pixels costs a quarter.
            shared.mask = graph.CreateTexture(new TextureDesc(Mathf.Max(1, target.width / 2), Mathf.Max(1, target.height / 2))
            {
                name = "Tutorial Spotlight Mask",
                format = GraphicsFormat.R8_UNorm,
                clearBuffer = true,
                clearColor = Color.clear,
                filterMode = FilterMode.Bilinear,
            });
            using var builder = graph.AddRasterRenderPass<PassData>("Tutorial Spotlight Mask", out var data);
            data.renderers = Lit;
            data.material = material;
            builder.SetRenderAttachment(shared.mask, 0);
            builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
            {
                // Only what's actually on screen: DrawRenderer would also draw hidden ones, like the spirit's own
                // stand-in sprite (SpiritSprite switches it off and draws the spirit from separate pieces).
                foreach (var r in d.renderers)
                    if (r != null && r.enabled && !r.forceRenderingOff && r.gameObject.activeInHierarchy)
                        ctx.cmd.DrawRenderer(r, d.material, 0, 0);
            });
        }
    }

    // The dark over the finished frame, clear where the mask is.
    class DimPass : ScriptableRenderPass
    {
        class PassData
        {
            public TextureHandle mask, target;
            public Material material;
        }

        readonly Material material;

        public DimPass(Material material)
        {
            this.material = material;
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
        {
            if (!frame.Contains<MaskData>()) return;
            var resources = frame.Get<UniversalResourceData>();
            using var builder = graph.AddRasterRenderPass<PassData>("Tutorial Spotlight", out var data);
            data.mask = frame.Get<MaskData>().mask;
            data.target = resources.activeColorTexture;
            data.material = material;
            builder.UseTexture(data.mask);
            builder.SetRenderAttachment(data.target, 0);
            builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
            {
                // After post-processing the frame may already be the screen itself, which on Metal/Vulkan/DirectX is
                // stored upside down from the mask; read the mask flipped then, or the light lands mirrored off the
                // shape (what URP's own final blit does).
                bool flip = ctx.GetTextureUVOrigin(in d.mask) != ctx.GetTextureUVOrigin(in d.target);
                var scaleBias = flip ? new Vector4(1f, -1f, 0f, 1f) : new Vector4(1f, 1f, 0f, 0f);
                Blitter.BlitTexture(ctx.cmd, d.mask, scaleBias, d.material, 1);
            });
        }
    }
}
