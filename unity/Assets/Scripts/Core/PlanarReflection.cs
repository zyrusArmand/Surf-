// What the sea reflects: the world drawn a second time from under the water, mirrored about the
// sea's mean level, at half resolution, into a texture the ocean shader samples with its own
// ripples bending it. The web could only reflect a painted sky gradient; here the palm, the
// rider, the buoys, the plane, the barrel's lip and the clouds all show in the water.
// The reflection camera is an ordinary camera placed at the mirror image of the main one (so no
// inverted culling is needed); the shader projects the mirrored world point through its matrix,
// which absorbs the flip. Anything below the water plane is cut away by an oblique near plane.
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class PlanarReflection : MonoBehaviour
{
    public Camera main;
    public float planeY;                 // the sea's mean level (0 out at sea, the beach's sea level on the title)
    public bool on = true;
    public float scale = 0.5f;           // of the screen's resolution
    // Each floating thing rides the swell at its own height, so no single mirror plane is right for
    // all of them. For the reflection's own render only, whoever owns them moves each one so its
    // waterline sits on planeY (beforeRender) and puts it back straight after (afterRender); the
    // sea looks its reflection up on planeY too, so every reflection starts at its own waterline.
    public System.Action<float> beforeRender;
    public System.Action afterRender;
    Camera rc; RenderTexture rt;
    static readonly int TexId = Shader.PropertyToID("_ReflTex"), VpId = Shader.PropertyToID("_ReflVP"), OnId = Shader.PropertyToID("_ReflOn"), YId = Shader.PropertyToID("_ReflY");
    public const int WaterLayer = 4, UiLayer = 5, StageLayer = 30;

    void OnEnable()
    {
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += Begin;
        UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += End;
    }
    void OnDisable()
    {
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= Begin;
        UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= End;
    }
    bool shifted;
    void Begin(UnityEngine.Rendering.ScriptableRenderContext ctx, Camera c) { if (c == rc && rc != null && !shifted) { shifted = true; beforeRender?.Invoke(planeY); } }
    void End(UnityEngine.Rendering.ScriptableRenderContext ctx, Camera c) { if (c == rc && shifted) { shifted = false; afterRender?.Invoke(); } }

    void Start()
    {
        var go = new GameObject("ReflectionCamera");
        rc = go.AddComponent<Camera>();
        rc.enabled = true;
        rc.depth = (main != null ? main.depth : 0f) - 10f;
        rc.cullingMask = Mask();
        rc.clearFlags = CameraClearFlags.Skybox;
        rc.allowHDR = true; rc.allowMSAA = false;
        var ud = go.AddComponent<UniversalAdditionalCameraData>();
        ud.renderPostProcessing = false; ud.renderShadows = false; ud.requiresDepthTexture = false; ud.requiresColorTexture = false;
        ud.antialiasing = AntialiasingMode.None;
    }

    // whatever the main camera sees, less the sea itself, the UI and the shop's preview stages
    int Mask() => (main != null ? main.cullingMask : ~0) & ~((1 << WaterLayer) | (1 << UiLayer) | (1 << StageLayer));

    void Ensure()
    {
        int w = Mathf.Max(64, Mathf.RoundToInt(Screen.width * scale)), h = Mathf.Max(64, Mathf.RoundToInt(Screen.height * scale));
        if (rt != null && rt.width == w && rt.height == h) return;
        if (rt != null) { rc.targetTexture = null; rt.Release(); Destroy(rt); }
        // with mips, so the sea can soften it by how rough the water is (a hard half-size picture
        // laid on the water read as dark blocks with stepped edges)
        rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGBHalf) { name = "SeaReflection", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, useMipMap = true, autoGenerateMips = true };
        rt.Create();
        rc.targetTexture = rt;
        Shader.SetGlobalTexture(TexId, rt);
    }

    void LateUpdate()
    {
        if (rc == null || main == null) return;
        bool live = on && main.isActiveAndEnabled;
        rc.enabled = live;
        Shader.SetGlobalFloat(OnId, live ? 1f : 0f);
        if (!live) return;
        Ensure();
        // the mirror image of the main camera about the plane y = planeY
        var mt = main.transform;
        var p = mt.position; var f = mt.forward; var u = mt.up;
        var pm = new Vector3(p.x, 2f * planeY - p.y, p.z);
        var fm = new Vector3(f.x, -f.y, f.z); var um = new Vector3(u.x, -u.y, u.z);
        rc.transform.SetPositionAndRotation(pm, Quaternion.LookRotation(fm, um));
        rc.cullingMask = Mask();
        rc.fieldOfView = main.fieldOfView; rc.aspect = main.aspect;
        rc.nearClipPlane = main.nearClipPlane; rc.farClipPlane = main.farClipPlane;
        rc.ResetProjectionMatrix();
        // cut away everything under the water: an oblique near plane on the sea's surface
        var w2c = rc.worldToCameraMatrix;
        // at the mean level itself: the floating things are seated on it for this render (beforeRender),
        // so anything under it is under the water and must not be mirrored up (a log's submerged half
        // came back as a dark band above it)
        var cpos = w2c.MultiplyPoint(new Vector3(0f, planeY - 0.02f, 0f));
        var cn = w2c.MultiplyVector(Vector3.up).normalized;
        var clip = new Vector4(cn.x, cn.y, cn.z, -Vector3.Dot(cpos, cn));
        rc.projectionMatrix = rc.CalculateObliqueMatrix(clip);
        // what the shader needs to find a mirrored point in the texture
        Shader.SetGlobalMatrix(VpId, rc.projectionMatrix * rc.worldToCameraMatrix);
        Shader.SetGlobalFloat(YId, planeY);
    }

    void OnDestroy()
    {
        if (rt != null) { rt.Release(); Destroy(rt); }
        if (rc != null) Destroy(rc.gameObject);
    }
}
