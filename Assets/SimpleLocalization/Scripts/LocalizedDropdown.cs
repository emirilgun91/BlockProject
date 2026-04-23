using System;
using Attributes;
using JetBrains.Annotations;
using UnityEngine;
using TMPro;

namespace Assets.SimpleLocalization.Scripts
{
	/// <summary>
	/// Localize dropdown component.
	/// </summary>
    [RequireComponent(typeof(TMP_Dropdown))]
    public class LocalizedDropdown : MonoBehaviour
    {

	    
	    [Serializable]
	    private class LocalizationKeyData
	    {
		    [LocalizationKeyDropDown]
		    public string key;
		    [CanBeNull] public object[] args;

	    }
	    
	    
        public string[] LocalizationKeys;

        public void Start()
        {
            Localize();
            LocalizationManager.OnLocalizationChanged += Localize;
        }

        public void OnDestroy()
        {
            LocalizationManager.OnLocalizationChanged -= Localize;
        }

        private void Localize()
        {
	        var dropdown = GetComponent<TMP_Dropdown>();

			for (var i = 0; i < LocalizationKeys.Length; i++)
	        {
		        dropdown.options[i].text = LocalizationManager.Localize(LocalizationKeys[i]);
	        }

	        if (dropdown.value < LocalizationKeys.Length)
	        {
		        dropdown.captionText.text = LocalizationManager.Localize(LocalizationKeys[dropdown.value]);
	        }
        }
    }
}