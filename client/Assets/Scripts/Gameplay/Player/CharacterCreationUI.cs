using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using System.IO;
using WildCourt.Net;
using WildCourt.Net.Protocol;

/// <summary>
/// 角色创建界面：玩家首次进入时设置「昵称 + 省份/城市」，并标记资料初始化完成。
/// 提交流程：校验昵称 → 保存昵称 → 保存地区 → 标记初始化 → 关闭面板。
/// </summary>
public class CharacterCreationUI : MonoBehaviour
{
    [Header("UI组件")]
    public TMP_InputField nameInput;
    public TMP_Dropdown provinceDropdown;
    public TMP_Dropdown cityDropdown;
    public Button confirmButton;

    private Province[] provinces;
    private Province selectedProvince;

    private string _lastNickname = "";
    private bool _isNicknameSaved = false;   // 本次流程昵称是否保存成功（用于防重复/流程判断，不落库）
    private bool _isSubmitting = false;      // 是否正在提交（防重复点击）

    void Start()
    {
        StartCoroutine(LoadLocationDataCoroutine());
        confirmButton.onClick.AddListener(OnConfirm);
    }

    /// <summary>
    /// 读取地区配置（跨平台兼容）：统一用 UnityWebRequest 读 StreamingAssets。
    /// PC/编辑器/iOS 为本地路径（file://），Android 为 jar:file 或 file:///android_asset，都可用 UnityWebRequest 读取。
    /// </summary>
    private IEnumerator LoadLocationDataCoroutine()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "config/Config_Regions.json");
        string url = Application.platform == RuntimePlatform.Android
            ? path                                // Android 的 streamingAssetsPath 自带 jar:file 协议
            : "file://" + path;                   // 其他平台本地路径补 file:// 协议

        using (var req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"读取地区配置失败:{path}，错误:{req.error}");
                SetupDropdowns();
                yield break;
            }

            LocationData locationData = JsonUtility.FromJson<LocationData>(req.downloadHandler.text);
            if (locationData != null && locationData.rows != null)
            {
                var provinceDict = new Dictionary<int, Province>();
                foreach (var row in locationData.rows)
                {
                    if (!provinceDict.TryGetValue(row.provinceId, out var province))
                    {
                        province = new Province { id = row.provinceId, name = row.provinceName, cities = new List<City>() };
                        provinceDict[row.provinceId] = province;
                    }
                    province.cities.Add(new City { id = row.cityId, name = row.cityName });
                }
                provinces = new List<Province>(provinceDict.Values).ToArray();
                Debug.Log($"加载了{provinces.Length}个省份数据,共{locationData.rows.Length}个城市");
            }
            else
            {
                Debug.LogWarning("地区配置 JSON 解析失败");
            }
        }

        SetupDropdowns();
    }

    void SetupDropdowns()
    {
        if (provinces == null || provinces.Length == 0)
        {
            Debug.LogWarning("省份数据为空，无法加载数据");
            return;
        }

        var provinceNames = new List<string>();
        foreach (var province in provinces)
        {
            provinceNames.Add(province.name);
        }
        provinceDropdown.ClearOptions();
        provinceDropdown.AddOptions(provinceNames);

        OnProvinceChanged(0);
        provinceDropdown.onValueChanged.AddListener(OnProvinceChanged);
    }

    void OnProvinceChanged(int index)
    {
        if (provinces == null || index < 0 || index >= provinces.Length) return;

        selectedProvince = provinces[index];
        var cityNames = new List<string>();
        foreach (var city in selectedProvince.cities)
        {
            cityNames.Add(city.name);
        }

        cityDropdown.ClearOptions();
        cityDropdown.AddOptions(cityNames);
    }

    void OnConfirm()
    {
        if (_isSubmitting) return;   // 防重复提交

        string nickname = nameInput.text;
        if (string.IsNullOrEmpty(nickname))
        {
            Debug.LogWarning("名字不能为空!");
            return;
        }

        _isSubmitting = true;
        SetConfirmEnabled(false);
        _isNicknameSaved = false;
        SaveNickname(nickname);
    }

    void SaveNickname(string nickname)
    {
        var net = NetManager.Instance;
        if (net == null)
        {
            OnSaveFailed("网络未初始化");
            return;
        }

        net.Put<UserProfileResponse>(ApiPaths.UserUpdateNickname, new UpdateNicknameRequest { nickname = nickname },
            resp =>
            {
                if (resp != null && resp.data != null)
                {
                    _lastNickname = resp.data.nickname;
                    _isNicknameSaved = true;
                    Debug.Log($"名称已更改为{_lastNickname}!");
                    SaveLocation();
                }
                else
                {
                    OnSaveFailed("昵称保存失败：响应为空");
                }
            },
            (code, msg) => OnSaveFailed($"昵称保存失败：{msg}"));
    }

    void SaveLocation()
    {
        if (selectedProvince == null || selectedProvince.cities.Count == 0)
        {
            OnSaveFailed("地区数据为空");
            return;
        }

        // 防止城市下拉越界
        int cityIndex = Mathf.Clamp(cityDropdown.value, 0, selectedProvince.cities.Count - 1);
        int provinceId = selectedProvince.id;
        int cityId = selectedProvince.cities[cityIndex].id;

        var net = NetManager.Instance;
        if (net == null)
        {
            OnSaveFailed("网络未初始化");
            return;
        }

        net.Put<UserProfileResponse>(ApiPaths.UserLocation, new UpdateLocationRequest { provinceId = provinceId, cityId = cityId },
            resp =>
            {
                if (resp != null && resp.data != null)
                {
                    Debug.Log("地区信息更新成功!");
                    MarkInitializationComplete();
                }
                else
                {
                    OnSaveFailed("地区信息保存失败：响应为空");
                }
            },
            (code, msg) => OnSaveFailed($"地区信息保存失败：{msg}"));
    }

    /// <summary>昵称 + 地区都保存成功后，标记角色初始化完成（profile_initialized=true）。</summary>
    void MarkInitializationComplete()
    {
        var net = NetManager.Instance;
        if (net == null)
        {
            OnSaveFailed("网络未初始化");
            return;
        }

        net.Put<UserProfileResponse>(ApiPaths.UserCompleteInitialization, null,
            resp =>
            {
                OnAllDataSaved();
            },
            (code, msg) => OnSaveFailed($"初始化标记失败：{msg}"));
    }

    /// <summary>任一步保存失败的回滚：恢复提交按钮、允许重试（不关闭面板，玩家可修正后重新提交）。</summary>
    void OnSaveFailed(string reason)
    {
        _isSubmitting = false;
        _isNicknameSaved = false;
        SetConfirmEnabled(true);
        Debug.LogWarning(reason);
    }

    void OnAllDataSaved()
    {
        Debug.Log("所有数据保存完毕");
        _isSubmitting = false;
        SetConfirmEnabled(true);
        ClosePanel();
    }

    void SetConfirmEnabled(bool enabled)
    {
        if (confirmButton != null) confirmButton.interactable = enabled;
    }

    public void ClosePanel()
    {
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 0;
            cg.interactable = false;
            cg.blocksRaycasts = false;
            Debug.Log("面板已关闭");
        }
    }
}

[System.Serializable]
public class LocationData
{
    public RegionRow[] rows;
}

[System.Serializable]
public class RegionRow
{
    public int provinceId;
    public string provinceName;
    public int cityId;
    public string cityName;
}

[System.Serializable]
public class Province
{
    public int id;
    public string name;
    public List<City> cities;
}

[System.Serializable]
public class City
{
    public int id;
    public string name;
}
