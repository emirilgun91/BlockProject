using System;
using Assets.SimpleLocalization.Scripts;
using Attributes;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class UpdateTextTest : MonoBehaviour
{
    [SerializeField] private TextMeshProGUILocalized txt;

    [LocalizationKeyDropDown] [SerializeField]
    private string startKey;

    [LocalizationKeyDropDown] [SerializeField]
    private string continueKey;

    [LocalizationKeyDropDown] [SerializeField]
    private string soundVolumeKey;


    [SerializeField] [Range(0, 100)] private float soundVolume;
    
    public void Start()
    {
    }


    private void Update()
    {
        if (Keyboard.current.numpad1Key.wasPressedThisFrame)
            LocalizationManager.Language = "German";

        else if (Keyboard.current.numpad2Key.wasPressedThisFrame)
            LocalizationManager.Language = "English";

        else if (Keyboard.current.numpad3Key.wasPressedThisFrame)
            LocalizationManager.Language = "Turkish";
    }

    public void OnDestroy()
    {
    }
}