using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[System.Serializable]
public class SeasonData
{
    public string seasonName;
    public TileBase[] tiles;

}


public class dijituhuan : MonoBehaviour
{
    public Tilemap targetTilemap;
    public List<SeasonData> seasons;

    private int currentSeasonIndex = 0;  // 当前季节索引为第一个

    public void SwitchToSeason(int index)
    {
         if(index < 0 || index >= seasons.Count)
        {
            Debug.LogWarning("季节超出范围索引");
            return;
        }
        SwitchSeason(seasons[index].tiles);
        currentSeasonIndex = index;

    }

    public void SwitchToSeason(string seasonName)
    {
        for(int i = 0;i < seasons.Count; i++)
        {
            if (seasons[i].seasonName == seasonName)
            {
                SwitchToSeason(i);
                return;
            }
        }
        Debug.LogWarning("找不到名为" + seasonName + "的季节");
    }

    private void SwitchSeason(TileBase[] newTile)
    {
        if (targetTilemap == null) return;

        BoundsInt bounds = targetTilemap.cellBounds;
        TileBase[] currentTiles = targetTilemap.GetTilesBlock(bounds);

        for(int i = 0; i < currentTiles.Length; i++)
        {
            Vector3Int pos = new Vector3Int(
                bounds.x + i % bounds.size.x,
                bounds.y + i / bounds.size.y,
                0);
            int tileIndex = GetTileIndex(currentTiles[i]);
            if(tileIndex > 0 && tileIndex < newTile.Length)
            {
                targetTilemap.SetTile(pos, newTile[tileIndex]);
            }
        }
    }
    private int GetTileIndex(TileBase tile)
    {
        if (seasons.Count == 0 || seasons[0].tiles == null) return -1;
        return System.Array.IndexOf(seasons[0].tiles, tile);
    }

}
