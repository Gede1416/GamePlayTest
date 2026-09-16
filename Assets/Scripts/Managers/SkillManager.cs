using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 技能管理器：缓存技能 GameObject 列表，按键 1~6 显示对应项（键 1 → 下标 0，键 6 → 下标 5）。
/// 显示 0.5 秒后自动隐藏；同一时间只能释放一个技能。
/// </summary>
public class SkillManager : MonoBehaviour
{
    [Tooltip("下标 0~5 依次对应按键 1~6")]
    public List<GameObject> skills = new List<GameObject>();

    [Tooltip("技能显示时长（秒）")]
    public float showTime = 0.5f;

    Coroutine current;

    void Start()
    {
        foreach (var skill in skills)
        {
            skill.SetActive(false);
        }
    }

    void Update()
    {
        for (int i = 0; i < skills.Count && i < 6; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Show(i);   // KeyCode.Alpha1~Alpha6 是连续的
    }

    /// <summary>释放下标对应的技能；正在释放中则忽略本次输入</summary>
    public void Show(int index)
    {
        if (current != null) return;                                            // 每次只能释放一个
        if (index < 0 || index >= skills.Count || skills[index] == null) return;
        current = StartCoroutine(ShowRoutine(skills[index]));
    }

    IEnumerator ShowRoutine(GameObject skill)
    {
        skill.SetActive(true);
        yield return new WaitForSeconds(showTime);
        skill.SetActive(false);
        current = null;
    }
}
