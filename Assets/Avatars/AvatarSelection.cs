using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

public class AvatarSelection : MonoBehaviour
{
    
    [SerializeField]
    private TMP_Dropdown avatarNumberDropdown;
    [SerializeField]
    private TMP_Dropdown avatarSexDropdown;
    [SerializeField] private RawImage avatarImage;

    [SerializeField] private Button randomizeAvatar;
    [SerializeField] private Button randomizeNumber;
    
    private List<TMP_Dropdown.OptionData> _avatarSexOptions;
    private List<TMP_Dropdown.OptionData> _avatarMaleOptions;
    private List<TMP_Dropdown.OptionData> _avatarFemaleOptions;
    
    private const int NumMaleAvatars = 21;
    private const int NumFemaleAvatars = 17;

    // Start is called before the first frame update
    private void Start()
    {
        // Set up the UI components: Load the correct image and fill the options for avatar selection.
        avatarImage = GameObject.Find("AvatarImage").GetComponent<RawImage>();
        _avatarSexOptions = avatarSexDropdown.options;
        // If the dropdown selection changes, update the image.
        avatarSexDropdown.onValueChanged.AddListener(delegate {
            SexChosen(avatarSexDropdown);
            var avatarPNG = UpdatePreviewImage();
            if (avatarPNG == null) return;
            avatarImage.texture = avatarPNG;
        });
        avatarNumberDropdown.onValueChanged.AddListener(delegate
        {
            NumberChosen(avatarNumberDropdown);
            var avatarPNG = UpdatePreviewImage();
            if (avatarPNG == null) return;
            avatarImage.texture = avatarPNG;
        });

        _avatarMaleOptions = new List<TMP_Dropdown.OptionData>();
        for (int i = 1; i <= NumMaleAvatars; i++)
        {
            _avatarMaleOptions.Add(
                new TMP_Dropdown.OptionData
                {
                    text = i >= 10 ? i.ToString() : "0" + i
                }
                );
        }
        _avatarFemaleOptions = new List<TMP_Dropdown.OptionData>();
        for (int i = 1; i <= NumFemaleAvatars; i++)
        {
            _avatarFemaleOptions.Add(
            new TMP_Dropdown.OptionData
                    {
                        text = i >= 10 ? i.ToString() : "0" + i
                    }
            );
        }
        
        // Set up the randomize buttons
        randomizeAvatar.onClick.AddListener(delegate
        {
            Randomize();
            var avatarPNG = UpdatePreviewImage();
            if (avatarPNG == null) return;
            avatarImage.texture = avatarPNG;
        });
        randomizeNumber.onClick.AddListener(delegate
        {
            RandomizeNumber();
            var avatarPNG = UpdatePreviewImage();
            if (avatarPNG == null) return;
            avatarImage.texture = avatarPNG;
        });

        avatarNumberDropdown.options = _avatarFemaleOptions;
        PlayerPrefs.SetString("AvatarSex", avatarSexDropdown.options[avatarSexDropdown.value].text);
        PlayerPrefs.SetString("AvatarNumber", avatarNumberDropdown.options[avatarNumberDropdown.value].text);
    }

    private void Randomize()
    {
        RandomizeSex();
        RandomizeNumber();
    }
    private void RandomizeSex()
    {  
        var randomSex = Random.Range(0, 2);
        avatarSexDropdown.value = randomSex;
        SexChosen(avatarSexDropdown);
    }

    private void RandomizeNumber()
    {
        var randomNumber = avatarSexDropdown.options[avatarSexDropdown.value].text == "Female" ? Random.Range(0, NumFemaleAvatars - 1) : Random.Range(0, NumMaleAvatars - 1);
        avatarNumberDropdown.value = randomNumber;
        NumberChosen(avatarNumberDropdown);
    }
    
    
    /// <summary>
    /// Fills the menu dropdown with the correct options when the sex of the avatar is chosen.
    /// Additionally, sets "AvatarSex" to the selected sex.
    /// </summary>
    /// <param name="sexDropdown">The UI dropdown</param>
    private void SexChosen(TMP_Dropdown sexDropdown)
    {
        var avatarSex = sexDropdown.options[sexDropdown.value].text;
        Debug.Log("Sex chosen: " + avatarSex);
        switch (avatarSex)
        {
            case "Female":
                avatarNumberDropdown.options = _avatarFemaleOptions;
                break;
            case "Male":
                avatarNumberDropdown.options = _avatarMaleOptions;
                break;
        }
        PlayerPrefs.SetString("AvatarSex", avatarSex);
        Debug.Log("AvatarSex: " + PlayerPrefs.GetString("AvatarSex") + " saved.");
    }
    
    /// <summary>
    /// Sets "AvatarNumber" to the selected number.
    /// </summary>
    /// <param name="numberDropdown"></param>
    private void NumberChosen(TMP_Dropdown numberDropdown)
    {
        var avatarNumber = numberDropdown.options[numberDropdown.value].text;
        Debug.Log("Number chosen");
        PlayerPrefs.SetString("AvatarNumber", avatarNumber);
        Debug.Log("AvatarNumber: " + PlayerPrefs.GetString("AvatarNumber") + " saved.");
    }

    /// <summary>
    /// Updates the image displayed on the menu based on the selected avatar.
    /// </summary>
    /// <returns></returns>
    private Texture2D UpdatePreviewImage()
    {
        if (_avatarSexOptions[avatarSexDropdown.value].text == null |
            avatarNumberDropdown.options[avatarNumberDropdown.value].text == null) return null;
            if(!avatarImage.enabled) avatarImage.enabled = true;
            var avatarPNG = Resources.Load("Avatars/Adults/{sex}_Adult_{number}/{sex}_Adult_{number}".Replace(
            "{sex}",
            _avatarSexOptions[avatarSexDropdown.value].text).Replace(
            "{number}",
            avatarNumberDropdown.options[avatarNumberDropdown.value].text));
        return (Texture2D)avatarPNG;
    }
    
}
