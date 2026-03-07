using System.Collections.Generic;
using RogueBlockBlast.Core;
using UnityEngine;

namespace RogueBlockBlast.UI
{
    public class PoolView : MonoBehaviour
    {
        [SerializeField] private List<PoolSlotView> _slots;
        
        public void Bind(List<PieceDefinition> pool, int selectedIndex)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (i < pool.Count)
                {
                    _slots[i].gameObject.SetActive(true);

                    _slots[i].Render(pool[i], i == selectedIndex);
                    
                }
                else
                {
                    _slots[i].gameObject.SetActive(false);
                }
            }
        }
        
    }
}