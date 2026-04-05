using System.Collections.Generic;
using UnityEngine;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// ScorePopup object pool.
    /// Canvas altına yerleştir — popup'lar bu transform'un child'ı olur.
    ///
    /// Hierarchy:
    ///  Gameplay Canvas
    ///   └── ScorePopupPool  ← bu script + prefab referansı
    /// </summary>
    public sealed class ScorePopupPool : MonoBehaviour
    {
        public static ScorePopupPool Instance { get; private set; }

        [SerializeField] private ScorePopup _prefab;
        [SerializeField] private int        _initialSize = 24;

        private readonly Queue<ScorePopup> _pool = new();

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            // Pool'u ısıt
            for (int i = 0; i < _initialSize; i++)
            {
                var popup = Create();
                popup.gameObject.SetActive(false);
                _pool.Enqueue(popup);
            }
        }

        /// <summary>Kullanıma hazır bir popup alır.</summary>
        public ScorePopup Get()
        {
            ScorePopup popup;

            if (_pool.Count > 0)
            {
                popup = _pool.Dequeue();
                popup.gameObject.SetActive(true);
            }
            else
            {
                popup = Create();
            }

            return popup;
        }

        /// <summary>Kullanımı biten popup'ı pool'a geri verir.</summary>
        public void Return(ScorePopup popup)
        {
            popup.gameObject.SetActive(false);
            popup.transform.SetParent(transform);
            _pool.Enqueue(popup);
        }

        private ScorePopup Create()
        {
            var go = Instantiate(_prefab, transform);
            go.name = "ScorePopup";
            return go;
        }
    }
}