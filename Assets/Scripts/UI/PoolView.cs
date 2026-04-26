using System.Collections.Generic;
using DG.Tweening;
using RogueBlockBlast.Core;
using UnityEngine;

namespace RogueBlockBlast.UI
{
    public class PoolView : MonoBehaviour
    {
        [SerializeField] private List<PoolSlotView> _slots;

        [Header("Pool Entrance Animation")]
        [SerializeField] private bool  _animateOnBind = true;
        [SerializeField] private float _staggerDelay  = 0.06f;

        // Önceki pool sayısını takip et — sadece yeni pool gelince animasyon
        private int _prevPoolCount = -1;

        public void Bind(List<PieceDefinition> pool, int selectedIndex)
        {
            bool isNewPool = pool.Count != _prevPoolCount;
            _prevPoolCount = pool.Count;

            for (int i = 0; i < _slots.Count; i++)
            {
                if (i < pool.Count)
                {
                    _slots[i].gameObject.SetActive(true);
                    _slots[i].Render(pool[i], i == selectedIndex);

                    // Yeni pool gelince entrance animasyonu — seçim değişiminde değil
                    if (_animateOnBind && isNewPool)
                        _slots[i].PlayEntranceAnim(i * _staggerDelay);
                }
                else
                {
                    _slots[i].gameObject.SetActive(false);
                }
            }
        }
    }
}