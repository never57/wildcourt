using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局生命周期管理器（单例）。
/// 负责：启动加载配置、持有各子系统管理器引用、应用退出清理。
/// 对应文档十五 Scripts/Core 约定。
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("子系统")]
    public ConfigManager Config;
    public CurrencyManager Currency;
    public InventoryManager Inventory;
    public FarmManager Farm;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Config = new ConfigManager();
        Currency = new CurrencyManager();
        Inventory = new InventoryManager();
        Farm = new FarmManager();
    }

    private void Start()
    {
        // 启动时加载数值配置（文档十六：Excel->Lua->Config）。
        Config.LoadAll();
        // 农业闭环初始化：初始 6 块农田（文档四 4.3）。
        Farm.Init(6, 12);
    }

    private void OnApplicationQuit()
    {
        // TODO: 存档、断连服务端。
    }
}
