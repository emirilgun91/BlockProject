using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Pool yenileme butonu.
    /// RunController'dan Bind() ile hak sayısı verilir.
    /// 
    /// Hierarchy:
    ///  PoolRerollButton (bu script + Button)
    ///   ├── Icon (TMP — "🔄")
    ///   └── CountText (TMP — "x2")
    /// </summary>
    public sealed class PoolRerollButton : MonoBehaviour
    {
        [SerializeField] private Button   _button;
        [SerializeField] private TMP_Text _countText;
        [SerializeField] private CanvasGroup _canvasGroup;

        public System.Action OnRerollClicked;

        private void Awake()
        {
            _button?.onClick.AddListener(() => OnRerollClicked?.Invoke());
        }

        /// <summary>Kalan hak sayısını güncelle.</summary>
        public void UpdateCount(int remaining)
        {
            bool hasRolls = remaining > 0;

            if (_countText != null)
                _countText.text = $"x{remaining}";

            if (_button != null)
                _button.interactable = hasRolls;

            if (_canvasGroup != null)
                _canvasGroup.alpha = hasRolls ? 1f : 0.35f;
        }

        /// <summary>Upgrade yoksa gizle.</summary>
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }
    }
}