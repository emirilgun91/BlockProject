using System.Collections.Generic;
using UnityEngine;

namespace RogueBlockBlast.Content
{
    /// <summary>
    /// Tüm upgrade'lerin listesi.
    /// Assets/Content/UpgradeLibrary.asset olarak oluştur.
    /// UpgradesController'a bağla.
    /// </summary>
    [CreateAssetMenu(menuName = "RogueBlockBlast/UpgradeLibrary", fileName = "UpgradeLibrary")]
    public sealed class UpgradeLibrarySO : ScriptableObject
    {
        public List<UpgradeSO> Upgrades = new List<UpgradeSO>();

        /// <summary>Id'ye göre upgrade bul.</summary>
        public UpgradeSO Get(string id) =>
            Upgrades.Find(u => u != null && u.Id == id);
    }
}