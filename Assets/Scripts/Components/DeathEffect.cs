using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 阵亡表现：让身上的渲染器逐渐淡出，淡完再把自己停用。
/// 原理：材质必须是支持透明的（内置标准着色器的 Fade 模式），这里用 MaterialPropertyBlock
/// 逐渲染器改 _Color 的 alpha——**不动共享材质**，所以用同一个材质的其它单位不受影响；
/// 属性块在 Clear 里去掉，颜色就回到材质上的值。
/// 职责边界：什么时候死由 Health 判定（它调 Play），这里只管表现，不碰生死数据、不参与结算。
/// 由 Entity.Init / Clear 调（组件自己不写 Awake / Start）。
/// 成员顺序：属性 → 生命周期 → 公开方法 → 私有方法（各组内按调用顺序）。
/// </summary>
public class DeathEffect : MonoBehaviour
{
    #region 属性

    [Tooltip("淡出时长（秒）；<= 0 就直接消失")]
    [SerializeField] float fadeTime = 0.6f;

    Renderer[] renderers;        // 要淡的渲染器（Init 时收集；之后新建的子物体不管，例如血条）
    Color[] baseColors;          // 各自的原始颜色，淡出只改 alpha
    MaterialPropertyBlock block; // 逐渲染器写属性，不生成材质实例
    Coroutine routine;           // 淡出协程（非空 = 正在淡，用来防重复触发）

    static readonly int ColorId = Shader.PropertyToID("_Color");

    #endregion

    #region 公开方法

    /// <summary>初始化：收集渲染器与它们的原始颜色（由 Entity.Init 调）</summary>
    public void Init()
    {
        routine = null;
        block = new MaterialPropertyBlock();

        var list = new List<Renderer>();
        var colors = new List<Color>();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r.sharedMaterial == null) continue;
            if (!r.sharedMaterial.HasProperty(ColorId)) continue;   // 不认 _Color 的着色器跳过（淡不了）

            list.Add(r);
            colors.Add(r.sharedMaterial.GetColor(ColorId));
        }
        renderers = list.ToArray();
        baseColors = colors.ToArray();
    }

    /// <summary>清理：停协程 + 去掉属性覆盖（颜色回到材质上的值），由 Entity.Clear 调</summary>
    public void Clear()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (renderers == null) return;
        foreach (var r in renderers)
        {
            if (r != null) r.SetPropertyBlock(null);
        }
    }

    /// <summary>开始淡出（由 Health.Die 调）：正在淡就直接返回；没配时长就立刻消失</summary>
    public void Play()
    {
        if (routine != null) return;

        if (fadeTime <= 0f)
        {
            Finish();
            return;
        }
        routine = StartCoroutine(FadeOut());
    }

    #endregion

    #region 私有方法

    /// <summary>alpha 从 1 线性降到 0，每帧推一次（用 unscaled 还是 scaled 都行，这里跟游戏时间走）</summary>
    IEnumerator FadeOut()
    {
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            SetAlpha(Mathf.Clamp01(1f - t / fadeTime));
            yield return null;
        }

        routine = null;
        Finish();
    }

    /// <summary>按比例设置所有渲染器的透明度（1 = 原样，0 = 全透明）</summary>
    void SetAlpha(float alpha)
    {
        if (renderers == null) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;

            var c = baseColors[i];
            block.SetColor(ColorId, new Color(c.r, c.g, c.b, c.a * alpha));
            r.SetPropertyBlock(block);
        }
    }

    /// <summary>淡完：全透明 + 停用自己（原来 Health.Die 里那句 SetActive(false) 搬到这儿了）</summary>
    void Finish()
    {
        SetAlpha(0f);
        gameObject.SetActive(false);
    }

    #endregion
}
