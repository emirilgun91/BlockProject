using Attributes;
using UnityEngine;

namespace Assets.SimpleLocalization.Scripts
{
    public class LocalizationKeyHolder : MonoBehaviour
    {
        [LocalizationKeyDropDown]
        public string LocalizationKey;
    }
}