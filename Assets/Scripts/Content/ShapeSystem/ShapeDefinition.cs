using RogueBlockBlast.Core;
using UnityEngine;

[CreateAssetMenu(menuName = "RogueBlockBlast/Shape")]
public class ShapeDefinitionSO : ScriptableObject
{
    public ShapeRarity Rarity;
    public int BaseWeight = 100;
    
}


namespace RogueBlockBlast.Core
{
    public enum ShapeRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }
}
