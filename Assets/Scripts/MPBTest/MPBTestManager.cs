using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// MaterialPropertyBlock（MPB）/ GPU Instancing / 批处理的对照测试场（只服务学习，和战斗逻辑无关）。
/// 进播放后自动在原点建一片方块，用左上角 HUD 的按钮或键盘切 6 种「逐实例数据」写法：
/// 每种写法都实时给出 Batches / SetPass Calls / 材质实例数，并把每种模式最近一次的读数记成一张对照表，
/// 所以按 1→6 走一遍就有一张「同数量下各写法代价」的表；配合 Window > Analysis > Frame Debugger
/// 还能看到每个 draw call 到底为什么被合批 / 为什么没被合批。
///
/// 本项目环境（决定了结论）：Built-in RP（GraphicsSettings 里没有 SRP 资产），所以这里根本没有 SRP Batcher，
/// 「MPB 破坏 SRP Batcher」这一条在本项目不适用；Built-in 下 MPB 反而是喂 GPU Instancing 逐实例数据的正道，
/// 但有个前提：那个属性必须在着色器里声明成逐实例属性（见 MPBTint.shader），
/// 否则 Unity 会禁用 instancing——两个内置材质的 Standard 着色器正好是反例（它的 _Color 是普通 uniform）。
/// 换成 URP / HDRP 后，凡是碰 MPB 的模式都会掉出 SRP Batcher（官方建议改用不同材质）。
///
/// 成员顺序：属性 → 生命周期 → 公开方法 → 私有方法（各组内按调用顺序）。
/// </summary>
public class MPBTestManager : MonoBehaviour
{
    #region 属性

    /// <summary>
    /// 逐实例数据的 6 种写法（前 4 种都是「给每个方块一个自己的颜色」，代价完全不同）：
    /// StandardInstanced      Standard + 开 Instancing，不写 MPB —— 基线，没有逐实例差异但批次最少
    /// StandardMpbColor       Standard + 开 Instancing + MPB 写 _Color —— _Color 不是 instanced 属性，instancing 会被禁用
    /// TintMpbColor           自写着色器（_Color 是 instanced 属性）+ 开 Instancing + MPB 写 _Color —— 正道
    /// TintNoInstancing       同上但关掉 Enable GPU Instancing —— 没有 instancing 兜底时 MPB 不能合批
    /// MaterialCopy           每物体一份 renderer.material —— 官方警告的反面教材（材质数 = 方块数）
    /// DrawMeshInstanced      Graphics.DrawMeshInstanced + MPB 数组 —— 绕开 GameObject / Renderer 的手动实例化
    /// </summary>
    enum Mode { StandardInstanced, StandardMpbColor, TintMpbColor, TintNoInstancing, MaterialCopy, DrawMeshInstanced }

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int[] CountLadder = { 100, 500, 2000, 5000 };   // [ ] 切换的方块数量档位
    const int InstancesPerDraw = 1023;                              // DrawMeshInstanced 单次调用的实例上限

    static readonly string[] ModeNames =
    {
        "1 Standard + Instancing, no MPB",
        "2 Standard + MPB _Color (NOT instanced prop)",
        "3 MPBTint + MPB _Color (instanced prop)",
        "4 MPBTint + MPB _Color, instancing OFF",
        "5 renderer.material (one material per object)",
        "6 DrawMeshInstanced + MPB (no GameObjects)",
    };

    static readonly string[] ModeNotes =
    {
        "same material for all, no per-instance data -> everything in one draw call",
        "_Color is a plain uniform in Standard -> putting it in an MPB disables instancing",
        "_Color lives in UNITY_INSTANCING_BUFFER -> per-instance color AND still one draw call",
        "instancing off: MPB still tints each cube, but nothing can merge these draws",
        "renderer.material copies the material per object: N materials, N SetPass calls",
        "1 call per 1023 objects, drawn straight from the list (no Renderer involved)",
    };

    Shader standardShader;          // 内置标准着色器（_Color 是普通 uniform）
    Shader tintShader;              // 自写着色器（_Color 是逐实例属性）
    Mesh cubeMesh;                  // 内置立方体网格
    Transform fieldRoot;            // 方块的父物体（重建时整个销毁）
    Material sharedMaterial;        // 共享材质（运行期造的，清理时销毁）
    MaterialPropertyBlock block;    // 逐实例数据；每帧复用同一个，避免 GC

    readonly List<Renderer> renderers = new();      // 场上方块的渲染器
    readonly List<Transform> nodes = new();         // 场上方块的 Transform（模式 6 要位置）
    readonly List<Material> created = new();        // 本次建出来的全部材质（HUD 显示 + 清理时销毁）

    Matrix4x4[] matrices;           // 模式 6：逐实例矩阵
    Vector4[] colors;               // 模式 6：逐实例颜色
    readonly Matrix4x4[] chunkMatrices = new Matrix4x4[InstancesPerDraw];   // 模式 6 分批用的临时缓冲
    readonly Vector4[] chunkColors = new Vector4[InstancesPerDraw];

    Mode mode = Mode.StandardMpbColor;
    int countIndex = 1;             // 默认 500 个
    bool animate;                   // 每帧写 MPB（感受逐实例数据的 CPU 代价）
    bool staticBatch;               // 重建时调 StaticBatchingUtility.Combine（静态批处理）

    int drawCalls;                  // 本帧模式 6 调了几次 DrawMeshInstanced
    int blockWrites;                // 本帧写了几次 SetPropertyBlock

    ProfilerRecorder batchesRec;    // 下面三个都是「渲染统计」计数器，等同于 Game 视图 Stats 面板
    ProfilerRecorder setPassRec;
    ProfilerRecorder drawCallRec;
    int framesSinceBuild;           // 建场后前几帧的读数没意义，记录表要等稳定
    readonly int[] lastBatches = new int[6];
    readonly int[] lastSetPass = new int[6];
    readonly int[] lastMaterials = new int[6];

    #endregion

    #region 生命周期

    void Awake()
    {
        cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        standardShader = Shader.Find("Standard");
        tintShader = Shader.Find("MPBTest/Tint");

        if (cubeMesh == null || standardShader == null || tintShader == null)
        {
            Debug.LogError("MPB 测试场：拿不到内置立方体网格 / Standard 着色器 / MPBTest/Tint 着色器，测试场停用");
            enabled = false;
            return;
        }

        block = new MaterialPropertyBlock();

        // 渲染统计计数器（名字就是 Stats 面板里的那些；拿不到就显示 n/a）
        batchesRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches");
        setPassRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls");
        drawCallRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls");

        Build();
    }

    void Update()
    {
        HandleKeys();

        if (mode == Mode.DrawMeshInstanced) DrawInstanced();
        else if (animate) AnimateBlock();

        framesSinceBuild++;
        RecordStats();
    }

    void OnGUI() => DrawHud();

    void OnDestroy()
    {
        ClearField();

        batchesRec.Dispose();
        setPassRec.Dispose();
        drawCallRec.Dispose();
    }

    #endregion

    #region 公开方法

    /// <summary>按当前配置重建整片方块（切模式 / 切数量 / 切静态批处理都会调它）</summary>
    [ContextMenu("重建测试场")]
    public void Build()
    {
        ClearField();

        int count = CountLadder[Mathf.Clamp(countIndex, 0, CountLadder.Length - 1)];
        int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
        int rows = (count - 1) / cols;      // 最后一行下标，用来把整片方块居中
        const float spacing = 1.1f, size = 0.9f;

        sharedMaterial = NewMaterial();

        fieldRoot = new GameObject("MPBTestField").transform;
        fieldRoot.SetParent(transform, false);

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject($"Cube_{i}");
            go.transform.SetParent(fieldRoot, false);
            go.transform.localPosition = new Vector3((i % cols - (cols - 1) * 0.5f) * spacing, size * 0.5f,
                                                     (i / cols - rows * 0.5f) * spacing);
            go.transform.localScale = Vector3.one * size;

            go.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = sharedMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;    // 阴影 pass 会让批次数翻倍，读数不好看，这里关掉
            r.receiveShadows = false;

            renderers.Add(r);
            nodes.Add(go.transform);
        }

        ApplyMode();

        // 静态批处理：把这一片网格合成到一起（要运行期手动调，运行时造出来的物体不会自动静态批处理）
        if (staticBatch) StaticBatchingUtility.Combine(fieldRoot.gameObject);

        FrameCamera(cols * spacing);

        framesSinceBuild = 0;
        Debug.Log($"[MPB 测试场] 模式 {ModeNames[(int)mode]}｜方块 {count}｜静态批处理 {(staticBatch ? "开" : "关")}｜" +
                  $"材质 {created.Count} 份 —— 打开 Frame Debugger 看这次的 draw call");
    }

    #endregion

    #region 私有方法

    // ---------- 建场 ----------

    /// <summary>造共享材质：模式 3 起用带逐实例 _Color 的自写着色器，模式 4 故意关掉 GPU Instancing</summary>
    Material NewMaterial()
    {
        bool tint = mode == Mode.TintMpbColor || mode == Mode.TintNoInstancing || mode == Mode.DrawMeshInstanced;
        var m = new Material(tint ? tintShader : standardShader) { enableInstancing = mode != Mode.TintNoInstancing };
        m.SetColor(ColorId, Color.white);
        created.Add(m);
        return m;
    }

    /// <summary>按当前模式给场上的渲染器上材质 / 逐实例数据</summary>
    void ApplyMode()
    {
        // 模式 6 不经过 Renderer，全部交给 DrawMeshInstanced 画
        if (mode == Mode.DrawMeshInstanced)
        {
            matrices = new Matrix4x4[renderers.Count];
            colors = new Vector4[renderers.Count];
            for (int i = 0; i < renderers.Count; i++)
            {
                renderers[i].enabled = false;
                matrices[i] = Matrix4x4.TRS(nodes[i].position, Quaternion.identity, nodes[i].localScale);
                colors[i] = PerInstanceColor(i);
            }
            return;
        }

        for (int i = 0; i < renderers.Count; i++)
        {
            var r = renderers[i];

            switch (mode)
            {
                case Mode.StandardInstanced:
                    break;                                              // 什么都不写：全部共用材质

                case Mode.MaterialCopy:
                    r.material = new Material(sharedMaterial);          // renderer.material —— 每物体一份材质
                    r.material.SetColor(ColorId, PerInstanceColor(i));
                    created.Add(r.material);
                    break;

                default:                                                // 模式 2 / 3 / 4：MPB 写逐实例颜色
                    block.SetColor(ColorId, PerInstanceColor(i));
                    r.SetPropertyBlock(block);
                    break;
            }
        }
    }

    /// <summary>第 i 个实例的颜色：黄金比例铺色相，相邻方块颜色差得开（看得出是逐实例的）</summary>
    static Color PerInstanceColor(int i) => Color.HSVToRGB(Mathf.Repeat(i * 0.618034f, 1f), 0.55f, 0.95f);

    /// <summary>把相机摆到能看全整片方块的位置（换数量后不用自己拖相机）</summary>
    void FrameCamera(float extent)
    {
        var cam = Camera.main;
        if (cam == null) return;

        float dist = extent * 1.6f + 4f;
        cam.transform.position = new Vector3(0f, dist * 0.75f, -dist);
        cam.transform.LookAt(Vector3.zero);
    }

    /// <summary>拆场：销毁方块与本次造出来的材质</summary>
    void ClearField()
    {
        if (fieldRoot != null) Destroy(fieldRoot.gameObject);
        fieldRoot = null;

        foreach (var m in created)
            if (m != null) Destroy(m);
        created.Clear();

        renderers.Clear();
        nodes.Clear();
        matrices = null;
        colors = null;
        sharedMaterial = null;
        blockWrites = 0;
        drawCalls = 0;
    }

    // ---------- 每帧：逐实例数据 ----------

    /// <summary>模式 6：手动实例化绘制，一次调用最多 1023 个，多了就分批调</summary>
    void DrawInstanced()
    {
        if (matrices == null || matrices.Length == 0) return;

        drawCalls = 0;
        int total = matrices.Length;

        for (int start = 0; start < total; start += InstancesPerDraw)
        {
            int n = Mathf.Min(InstancesPerDraw, total - start);
            for (int i = 0; i < n; i++)
            {
                chunkMatrices[i] = matrices[start + i];
                chunkColors[i] = colors[start + i];
            }

            // 逐实例数据靠数组塞进 MPB：一个 SetColor 是整批同一个值，逐实例要 SetVectorArray
            block.SetVectorArray(ColorId, chunkColors);
            Graphics.DrawMeshInstanced(cubeMesh, 0, sharedMaterial, chunkMatrices, n, block,
                                       ShadowCastingMode.Off, false, gameObject.layer, null,
                                       LightProbeUsage.Off, null);
            drawCalls++;
        }
    }

    /// <summary>逐实例动画：每帧给每个渲染器写一次 MPB（这就是 DeathEffect 那类做法的每帧代价）</summary>
    void AnimateBlock()
    {
        if (renderers.Count == 0) return;

        blockWrites = 0;
        float t = Time.time * 2f;

        for (int i = 0; i < renderers.Count; i++)
        {
            var r = renderers[i];
            if (r == null || !r.enabled) continue;

            var c = PerInstanceColor(i);
            float pulse = 0.5f + 0.5f * Mathf.Sin(t + i * 0.05f);        // 亮度呼吸
            block.SetColor(ColorId, new Color(c.r * pulse, c.g * pulse, c.b * pulse, 1f));
            r.SetPropertyBlock(block);
            blockWrites++;
        }
    }

    // ---------- 操作 ----------

    void HandleKeys()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SetMode(Mode.StandardInstanced);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SetMode(Mode.StandardMpbColor);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SetMode(Mode.TintMpbColor);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SetMode(Mode.TintNoInstancing);
        if (Input.GetKeyDown(KeyCode.Alpha5)) SetMode(Mode.MaterialCopy);
        if (Input.GetKeyDown(KeyCode.Alpha6)) SetMode(Mode.DrawMeshInstanced);

        if (Input.GetKeyDown(KeyCode.LeftBracket)) SetCount(countIndex - 1);
        if (Input.GetKeyDown(KeyCode.RightBracket)) SetCount(countIndex + 1);

        if (Input.GetKeyDown(KeyCode.Space)) animate = !animate;         // 只对写 MPB 的模式有影响
        if (Input.GetKeyDown(KeyCode.S)) { staticBatch = !staticBatch; Build(); }
        if (Input.GetKeyDown(KeyCode.R)) Build();
    }

    void SetMode(Mode m)
    {
        if (mode == m) return;
        mode = m;
        Build();
    }

    /// <summary>换数量档位：读数只在同数量下可比，所以顺便把对照表清掉</summary>
    void SetCount(int index)
    {
        index = Mathf.Clamp(index, 0, CountLadder.Length - 1);
        if (index == countIndex) return;

        countIndex = index;
        System.Array.Clear(lastBatches, 0, lastBatches.Length);
        System.Array.Clear(lastSetPass, 0, lastSetPass.Length);
        System.Array.Clear(lastMaterials, 0, lastMaterials.Length);
        Build();
    }

    // ---------- 读数 ----------

    /// <summary>把当前模式的读数记进对照表（建场后前几帧不稳，跳过）</summary>
    void RecordStats()
    {
        if (framesSinceBuild < 3) return;

        long b = Read(batchesRec);
        if (b < 0) return;

        int m = (int)mode;
        lastBatches[m] = (int)b;
        lastSetPass[m] = (int)Read(setPassRec);
        lastMaterials[m] = created.Count;
    }

    static long Read(ProfilerRecorder r) => r.Valid ? r.LastValue : -1;

    static string Show(ProfilerRecorder r) => r.Valid ? r.LastValue.ToString() : "n/a (see Game view Stats)";

    // ---------- HUD ----------

    /// <summary>左上角面板：模式 / 数量 / 开关 / 实时读数 / 对照表。文字用英文——默认字体没有中文字形</summary>
    void DrawHud()
    {
        GUILayout.BeginArea(new Rect(10f, 10f, 520f, 500f), GUI.skin.box);

        GUILayout.Label("MaterialPropertyBlock / Batching Test   [Built-in RP: no SRP Batcher in this project]");
        GUILayout.Label($"count {CountLadder[countIndex]}    static batch {(staticBatch ? "ON" : "OFF")}    " +
                        $"per-frame MPB animate {(animate ? "ON" : "OFF")}");

        for (int i = 0; i < ModeNames.Length; i++)
        {
            if (GUILayout.Button(((Mode)i == mode ? "> " : "   ") + ModeNames[i])) SetMode((Mode)i);
        }
        GUILayout.Label("      " + ModeNotes[(int)mode]);

        GUILayout.BeginHorizontal();
        for (int i = 0; i < CountLadder.Length; i++)
        {
            if (GUILayout.Button(CountLadder[i].ToString())) SetCount(i);
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(animate ? "Stop MPB animate" : "Animate MPB per frame")) animate = !animate;
        if (GUILayout.Button("Toggle static batch")) { staticBatch = !staticBatch; Build(); }
        if (GUILayout.Button("Rebuild")) Build();
        GUILayout.EndHorizontal();

        GUILayout.Space(4f);
        GUILayout.Label($"Batches {Show(batchesRec)}    SetPass Calls {Show(setPassRec)}    Draw Calls {Show(drawCallRec)}");
        GUILayout.Label($"objects {renderers.Count}    materials {created.Count}    " +
                        (mode == Mode.DrawMeshInstanced
                            ? $"DrawMeshInstanced calls/frame {drawCalls}"
                            : $"SetPropertyBlock calls/frame {blockWrites}"));

        GUILayout.Space(4f);
        GUILayout.Label("last reading per mode at this count:   batches / setpass / materials");
        for (int i = 0; i < ModeNames.Length; i++)
        {
            string line = lastMaterials[i] == 0 && lastBatches[i] == 0
                ? "   -"
                : $"   {lastBatches[i]} / {lastSetPass[i]} / {lastMaterials[i]}";
            GUILayout.Label($"{((Mode)i == mode ? "> " : "   ")}{i + 1}{line}");
        }

        GUILayout.Space(4f);
        GUILayout.Label("keys: 1-6 mode | [ ] count | Space animate | S static batch | R rebuild");
        GUILayout.Label("Window > Analysis > Frame Debugger : why each draw call batched or not");

        GUILayout.EndArea();
    }

    #endregion
}
