using FairyGUI;
using UnityEngine;

[RequireComponent(typeof(UIPanel))]
public class ControlsShowcase : MonoBehaviour
{
    GList items;
    GComboBox category;
    GButton showImage;
    GButton submit;
    GLoader preview;
    GTextField details;
    GTextField status;

    // 独立示例入口，布局和控件均来自 FairyGUI 编辑器资源。
    void Start()
    {
        Init();
    }

    void OnDestroy()
    {
        Clear();
    }

    void Init()
    {
        GComponent view = GetComponent<UIPanel>().ui;
        if (view == null) return;
        items = view.GetChild("items").asList;
        category = view.GetChild("category").asComboBox;
        showImage = view.GetChild("showImage").asButton;
        submit = view.GetChild("submit").asButton;
        preview = view.GetChild("preview").asLoader;
        details = view.GetChild("details").asTextField;
        status = view.GetChild("status").asTextField;
        category.onChanged.Add(FillList);
        items.onClickItem.Add(SelectItem);
        showImage.onChanged.Add(ToggleImage);
        submit.onClick.Add(Submit);
        FillList();
    }

    void Clear()
    {
        if (items == null || items.isDisposed) return;
        category.onChanged.Remove(FillList);
        items.onClickItem.Remove(SelectItem);
        showImage.onChanged.Remove(ToggleImage);
        submit.onClick.Remove(Submit);
    }

    void FillList()
    {
        items.RemoveChildrenToPool();
        for (int i = 0; i < 30; i++)
        {
            int type = i % 2 + 1;
            if (category.selectedIndex != 0 && category.selectedIndex != type) continue;
            GButton row = items.AddItemFromPool().asButton;
            row.data = i;
            row.title = $"{(type == 1 ? "装备" : "消耗品")}  {i + 1:00}";
        }
        items.selectedIndex = 0;
        items.scrollPane.ScrollTop();
        SelectItem();
        status.text = $"已切换：{category.text}，共 {items.numChildren} 项";
    }

    void SelectItem()
    {
        GButton row = items.GetChildAt(items.selectedIndex).asButton;
        int id = (int)row.data;
        preview.url = id % 2 == 0 ? "Cooldown/k0" : "Cooldown/k1";
        details.text = $"{row.title}\n\n编号：{id + 1:00}\n这是用于测试列表选择的示例数据。";
        status.text = $"当前选择：{row.title}";
    }

    void ToggleImage()
    {
        preview.visible = showImage.selected;
        status.text = showImage.selected ? "图片已显示" : "图片已隐藏";
    }

    void Submit()
    {
        status.text = $"已确认：{items.GetChildAt(items.selectedIndex).text}\n图片显示：{showImage.selected}";
        Debug.Log($"[FairyGUI] {status.text}", this);
    }
}
