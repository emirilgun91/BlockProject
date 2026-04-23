#nullable enable
using TMPro;
using UnityEngine;

namespace Assets.SimpleLocalization.Scripts
{
    [RequireComponent(typeof(LocalizationKeyHolder))]
    public class TextMeshProGUILocalized : TextMeshProUGUI
    {
        protected override void Awake()
        {
            base.Awake();
        }

        private LocalizationKeyHolder _keyHolder;
        private object[]? _args;


        public void ChangeText(string key)
        {
            _keyHolder.LocalizationKey = key;
            Localize();
        }

        public void ChangeText(string key, params object[] args)
        {
            _args = args;
            _keyHolder.LocalizationKey = key;
            Localize();
        }

        public void UpdateArgs(params object[] args)
        {
            _args = args;
            Localize();
        }


        private void Localize()
        {
            if (_args is not null)
            {
                text = LocalizationManager.Localize(_keyHolder.LocalizationKey, _args);
                return;
            }

            text = LocalizationManager.Localize(_keyHolder.LocalizationKey);
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            if (_keyHolder == null)
                _keyHolder = GetComponent<LocalizationKeyHolder>();
            Localize();
            LocalizationManager.OnLocalizationChanged += Localize;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            LocalizationManager.OnLocalizationChanged -= Localize;
        }
    }
}