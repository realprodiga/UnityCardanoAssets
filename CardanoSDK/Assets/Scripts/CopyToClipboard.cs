using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;  // Needed for IEnumerator

public class CopyToClipboard : MonoBehaviour
{
    [SerializeField] private TMP_InputField textToCopy;   // Reference to the text you want to copy
    [SerializeField] private Sprite copyIconSprite;       // Assign CopyIconWhiteOnly sprite in Inspector
    [SerializeField] private Sprite checkmarkSprite;      // Assign Checkmark sprite in Inspector

    private Image buttonImage;

    private void Awake()
    {
        // Assuming this script is on the same GameObject as the Button with the Image component
        buttonImage = GetComponent<Image>();
    }

    public void CopyText()
    {
        // Copy text to clipboard
        GUIUtility.systemCopyBuffer = textToCopy.text; 
        Debug.Log("Text copied to clipboard: " + textToCopy.text);

        // Start coroutine to change icon
        StartCoroutine(FlashCheckmark());
    }

    private IEnumerator FlashCheckmark()
    {
        if (buttonImage != null)
        {
            // Change image to checkmark
            buttonImage.sprite = checkmarkSprite;
            
            yield return new WaitForSeconds(0.5f);
            
            // Revert image to original copy icon
            buttonImage.sprite = copyIconSprite;
        }
    }
}