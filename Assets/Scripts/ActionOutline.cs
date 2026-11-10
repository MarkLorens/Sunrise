using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

// A white outline round everything you could act on right now, like the edge on usable things in Cult of the Lamb:
// whoever you'd possess, what E would use (a ride part, the stall, someone to pick a fight with), what J would pick
// up, and the gear box a charged throw is locked onto. Only what you're allowed to act on lights up; refusals stay
// plain (the rings and the HUD say why). Only lit sprites are outlined - the standing art, not shadows, bubbles or
// glows. Drawn over the finished frame (Resources/ActionOutline.shader), so the night grade doesn't tint it, and
// injected from here rather than the renderer assets, so it works on the PC and Mobile renderers alike.
// GameHud adds this to the spirit.
[RequireComponent(typeof(Possession))]
public class ActionOutline : MonoBehaviour
{
    public Color color = new(0.388f, 0.298f, 0.459f); // #634C75, a dusky purple that sits in the night rather than glaring
    public float width = 4f; // pixels on a 1080p screen; scales with the screen

    static readonly int ColorId = Shader.PropertyToID("_OutlineColor");
    static readonly int StepId = Shader.PropertyToID("_OutlineStep");

    Possession spirit;
    PlayerInteractor interactor;
    Material material;
    MaskPass maskPass;
    DrawPass drawPass;
    readonly List<Transform> targets = new();
    readonly List<Renderer> renderers = new();
    readonly List<SpriteRenderer> found = new();

    void Awake()
    {
        spirit = GetComponent<Possession>();
        interactor = GetComponent<PlayerInteractor>();
        material = CoreUtils.CreateEngineMaterial(Resources.Load<Shader>("ActionOutline"));
        maskPass = new MaskPass(renderers, material);
        drawPass = new DrawPass(material);
    }

    void OnEnable() => RenderPipelineManager.beginCameraRendering += Enqueue;
    void OnDisable() => RenderPipelineManager.beginCameraRendering -= Enqueue;
    void OnDestroy() => CoreUtils.Destroy(material);

    // After Possession has picked this frame's candidate.
    void LateUpdate()
    {
        targets.Clear();
        if (!Possession.InputBlocked) FindTargets();

        renderers.Clear();
        foreach (var t in targets)
        {
            t.GetComponentsInChildren(found);
            foreach (var r in found)
                if (r.enabled && r.sprite != null && r.sharedMaterial != null && r.sharedMaterial.shader == ParkArt.LitMaterial.shader)
                    renderers.Add(r);
        }
    }

    void FindTargets()
    {
        switch (spirit.State)
        {
            case Possession.Mode.Spirit:
                // E possesses whoever you're touching; with nobody there it goes to the nearest thing instead.
                if (spirit.Candidate == null) AddUsable(null);
                else if (spirit.CandidateBlocked == null) targets.Add(spirit.Candidate);
                break;
            case Possession.Mode.Possessing when !spirit.Riding:
                AddUsable(spirit.Host);
                if (spirit.GrabCandidate != null) targets.Add(spirit.GrabCandidate.transform);
                break;
        }
    }

    void AddUsable(Visitor host)
    {
        var target = interactor != null ? interactor.Nearest() : null;
        if (target != null && target.Blocked(host) == null) targets.Add(target.transform);
    }

    void Enqueue(ScriptableRenderContext context, Camera cam)
    {
        if (renderers.Count == 0 || cam.cameraType != CameraType.Game || material == null) return;
        float up = width / 1080f;
        material.SetColor(ColorId, color);
        material.SetVector(StepId, new Vector4(up / cam.aspect, up));
        var renderer = cam.GetUniversalAdditionalCameraData().scriptableRenderer;
        renderer.EnqueuePass(maskPass);
        renderer.EnqueuePass(drawPass);
    }

    // The mask, handed from one pass to the next within a frame.
    class MaskData : ContextItem
    {
        public TextureHandle mask;
        public override void Reset() => mask = TextureHandle.nullHandle;
    }

    // Each outlined sprite as solid white into a mask, once the scene is drawn (the camera is still set up for it).
    class MaskPass : ScriptableRenderPass
    {
        class PassData
        {
            public List<Renderer> renderers;
            public Material material;
        }

        readonly List<Renderer> renderers;
        readonly Material material;

        public MaskPass(List<Renderer> renderers, Material material)
        {
            this.renderers = renderers;
            this.material = material;
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
        {
            var target = frame.Get<UniversalCameraData>().cameraTargetDescriptor;
            var shared = frame.Create<MaskData>();
            shared.mask = graph.CreateTexture(new TextureDesc(target.width, target.height)
            {
                name = "Outline Mask",
                format = GraphicsFormat.R16_SFloat, // distance from the camera, see the shader
                clearBuffer = true,
                clearColor = Color.clear,
            });

            using var builder = graph.AddRasterRenderPass<PassData>("Outline Mask", out var data);
            data.renderers = renderers;
            data.material = material;
            builder.SetRenderAttachment(shared.mask, 0);
            builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
            {
                foreach (var r in d.renderers)
                    if (r != null) ctx.cmd.DrawRenderer(r, d.material, 0, 0);
            });
        }
    }

    // The outline laid over the frame after post-processing, so the night grade doesn't shift its colour. The scene's
    // depth tells it where someone stands in front of what's outlined.
    class DrawPass : ScriptableRenderPass
    {
        class PassData
        {
            public TextureHandle mask;
            public Material material;
        }

        readonly Material material;

        public DrawPass(Material material)
        {
            this.material = material;
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            ConfigureInput(ScriptableRenderPassInput.Depth); // the Mobile renderer doesn't make a depth texture otherwise
        }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
        {
            if (!frame.Contains<MaskData>()) return;
            var resources = frame.Get<UniversalResourceData>();
            using var builder = graph.AddRasterRenderPass<PassData>("Outline", out var data);
            data.mask = frame.Get<MaskData>().mask;
            data.material = material;
            builder.UseTexture(data.mask);
            builder.UseTexture(resources.cameraDepthTexture);
            builder.SetRenderAttachment(resources.activeColorTexture, 0);
            builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
                Blitter.BlitTexture(ctx.cmd, d.mask, new Vector4(1f, 1f, 0f, 0f), d.material, 1));
        }
    }
}
