using System.Collections.Generic;
using UnityEngine;

public class BattleManager : MonoBehaviour
{
    [SerializeField] private string mapPath;
    [SerializeField] private string turnPath;
    [SerializeField] private List<string> entityPaths = new();
    [SerializeField] private Transform mapPos;

    private MapManager _mapManager;
    private TurnManager _turnManager;
    private List<Entity> _entities;

    void Awake()
    {
        // 加载目标路径的全部对象 并缓存对应组件
        // _mapManager = ?
        // _turnManager = ?
        // _entities = ?
    }

    void Start()
    {
        _mapManager.Init();
        foreach (var entity in _entities)
        {
            entity.Init();
        }
        _turnManager.Init();
    }

    public void StartBattle()
    {

    }

    public void RebuildBattle()
    {

    }


}