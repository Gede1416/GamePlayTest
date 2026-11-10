using UnityEngine;
using FairyGUI;

[RequireComponent(typeof(UIPanel))]
public class CooldownMain : MonoBehaviour
{
    const float Duration0 = 5;
    const float Duration1 = 10;

    GButton button0;
    GButton button1;
    GImage mask0;
    GImage mask1;
    float remaining0;
    float remaining1;
    bool initialized;

    // 独立测试场的入口，不接入战斗初始化链。
    void Start()
    {
        Init();
    }

    void Update()
    {
        if (!initialized) return;

        remaining0 -= Time.deltaTime;
        remaining1 -= Time.deltaTime;
        if (remaining0 < 0) remaining0 = Duration0;
        if (remaining1 < 0) remaining1 = Duration1;
        Refresh();
    }

    void OnDestroy()
    {
        Clear();
    }

    void Init()
    {
        Clear();
        GComponent view = GetComponent<UIPanel>().ui;
        button0 = view?.GetChild("b0") as GButton;
        button1 = view?.GetChild("b1") as GButton;
        mask0 = button0?.GetChild("mask") as GImage;
        mask1 = button1?.GetChild("mask") as GImage;
        if (button0 == null || button1 == null || mask0 == null || mask1 == null)
        {
            Debug.LogError("[FairyGUI] 请检查 Cooldown/Main：需要按钮 b0、b1，各自包含图片 mask。", this);
            Clear();
            return;
        }

        button0.icon = "Cooldown/k0";
        button1.icon = "Cooldown/k1";
        button0.onClick.Add(ResetFirst);
        button1.onClick.Add(ResetSecond);
        remaining0 = Duration0;
        remaining1 = Duration1;
        initialized = true;
        Refresh();
    }

    void Clear()
    {
        initialized = false;
        // UIPanel 负责销毁视图，这里只解除自己注册的事件。
        if (button0 != null && !button0.isDisposed) button0.onClick.Remove(ResetFirst);
        if (button1 != null && !button1.isDisposed) button1.onClick.Remove(ResetSecond);
        button0 = null;
        button1 = null;
        mask0 = null;
        mask1 = null;
    }

    void ResetFirst()
    {
        remaining0 = Duration0;
        Refresh();
        Debug.Log("[FairyGUI] b0 点击，冷却重置", this);
    }

    void ResetSecond()
    {
        remaining1 = Duration1;
        Refresh();
        Debug.Log("[FairyGUI] b1 点击，冷却重置", this);
    }

    void Refresh()
    {
        mask0.fillAmount = remaining0 / Duration0;
        mask1.fillAmount = remaining1 / Duration1;
        button1.text = Mathf.CeilToInt(remaining1).ToString();
    }

    [ContextMenu("运行时自检：重新初始化与点击")]
    void SelfCheck()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[FairyGUI] 请先进入播放模式。", this);
            return;
        }

        Init();
        Init();
        if (!initialized) return;
        remaining0 = remaining1 = 1;
        button0.onClick.Call();
        button1.onClick.Call();
        bool passed = remaining0 == Duration0 && remaining1 == Duration1
            && Mathf.Approximately(mask0.fillAmount, 1)
            && Mathf.Approximately(mask1.fillAmount, 1) && button1.text == "10";
        Debug.Assert(passed, "[FairyGUI] 点击重置或视图更新失败。", this);
        if (passed) Debug.Log("[FairyGUI] 自检通过：重新初始化、点击回调、遮罩与倒计时。", this);
    }
}
