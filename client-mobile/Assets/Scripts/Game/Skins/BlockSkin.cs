using UnityEngine;
using BlokusUnity.Common;

[CreateAssetMenu(menuName = "Blokus/Block Skin", fileName = "BlockSkin_Default")]
public class BlockSkin : ScriptableObject
{
    [Header("Player Tints")]
    public Color blue   = new Color(0.35f, 0.60f, 1.00f, 1f);
    public Color yellow = new Color(1.00f, 0.85f, 0.30f, 1f);
    public Color red    = new Color(1.00f, 0.40f, 0.40f, 1f);
    public Color green  = new Color(0.40f, 0.90f, 0.55f, 1f);

    public Color GetTint(PlayerColor p) => p switch
    {
        PlayerColor.Blue   => blue,
        PlayerColor.Yellow => yellow,
        PlayerColor.Red    => red,
        PlayerColor.Green  => green,
        _ => Color.white
    };
}
