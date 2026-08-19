using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.IO;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Linq;
using WildCourt.Net;
using WildCourt.Net.Protocol;

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
    private bool _isNicknameSaved = false;
    private bool _isLocationSaved = false;

    void Start()
    {
        LoadLocationData();
        SetupDropdowns();
        confirmButton.onClick.AddListener(OnConfirm);
    }

    void LoadLocationData()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "config/Config_Regions.json");
        if (File.Exists(filePath))
        {
            string jsonContent = File.ReadAllText(filePath);
            LocationData locationData = JsonUtility.FromJson<LocationData>(jsonContent);
            
            if(locationData != null && locationData.rows != null)
            {
                var provinceDict = new Dictionary<int, Province>();
                foreach(var row in locationData.rows)
                {
                    if (!provinceDict.ContainsKey(row.provinceId))
                    {
                        provinceDict[row.provinceId] = new Province
                        {
                            id = row.provinceId,
                            name = row.provinceName,
                            cities = new List<City>()
                        };

                    }
                    provinceDict[row.provinceId].cities.Add(new City
                    {
                        id = row.cityId,
                        name = row.cityName
                    });
                }
                provinces = provinceDict.Values.ToArray();
                Debug.Log($"加载了{provinces.Length}个省份数据,共{locationData.rows.Length}个城市");
            }
            else
            {
                Debug.LogWarning("JSON文件解析失败");
            }
        }
        else
        {
            Debug.LogWarning($"找不到文件:{filePath}");
        }
    }

    void SetupDropdowns()
    {
        if (provinces == null || provinces.Length == 0)
        {
            Debug.LogWarning("省份数据为空，无法加载数据");
            return;
        }

        List<string> provinceNames = new List<string>();
        foreach(var province in provinces)
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
        List<string> cityNames = new List<string>();
        foreach(var city in selectedProvince.cities)
        {
            cityNames.Add(city.name);
        }

        cityDropdown.ClearOptions();
        cityDropdown.AddOptions(cityNames);
    }

    void OnConfirm()
    {
        string nickname = nameInput.text;
        if (string.IsNullOrEmpty(nickname))
        {
            Debug.LogWarning("名字不能为空!");
            return;
        }

        SaveNickname(nickname);

    }

    void SaveNickname(string nickname)
    {
        Debug.Log("已进入SaveNickname方法");
        var net = NetManager.Instance;
        if (net == null)
        {
            Debug.LogWarning("NetManager为null喵");
            return;
        }
        Debug.Log("NetManager存在喵");
        net.Put<UserProfileResponse>(ApiPaths.UserUpdateNickname, new UpdateNicknameRequest { nickname = nickname},
            resp =>
            {
                Debug.Log("收到服务器响应喵");
                if(resp != null && resp.data != null)
                {
                    _lastNickname = resp.data.nickname;
                    Debug.Log($"名称已更改为{_lastNickname}!");

                    SaveLocation();
                }
            },
            (code,msg) =>
            {
                Debug.LogWarning($"修改名称失败，错误码{code},错误信息{msg}");
            });
    }

    void SaveLocation()
    {
        if (selectedProvince == null) return;

        int provinceId = selectedProvince.id;
        int cityId = selectedProvince.cities[cityDropdown.value].id;

        var net = NetManager.Instance;
        if (net == null) return;
        net.Put<UserProfileResponse>(ApiPaths.UserLocation, new UpdateLocationRequest { provinceId = provinceId, cityId = cityId },
            resp =>
            {
                Debug.Log("地区信息更新成功!");
                OnAllDataSaved();

            },
            (code, msg) =>
            {
                Debug.LogWarning("地区信息更新失败!");
            });
    }

    void OnAllDataSaved()
    {
        Debug.Log("所有数据保存完毕");
        ClosePanel();
    }

    public void ClosePanel()
    {
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if(cg != null)
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