using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class SectionManager : MonoBehaviour
{
    [System.Serializable]
    public class Section
    {
        public Button titleButton;
        public GameObject scrollView;
    }

    public List<Section> sections;
    private GameObject currentActiveScrollView;

    void Start()
    {
        // Ensure all scroll views are hidden initially
        foreach (var section in sections)
        {
            if(section.scrollView != null)
                section.scrollView.SetActive(false);

            // Add listener to each title button
            if(section.titleButton != null)
            {
                Section capturedSection = section; // capture local reference for closure
                section.titleButton.onClick.AddListener(() => OnSectionClicked(capturedSection));
            }
        }
    }

    void OnSectionClicked(Section clickedSection)
    {
        // If the clicked section's scroll view is already active, collapse it.
        if (clickedSection.scrollView.activeSelf)
        {
            clickedSection.scrollView.SetActive(false);
            currentActiveScrollView = null;
            return;
        }

        // Otherwise, hide current active scroll view (if any)
        if (currentActiveScrollView != null)
            currentActiveScrollView.SetActive(false);

        // Expand clicked section
        clickedSection.scrollView.SetActive(true);
        currentActiveScrollView = clickedSection.scrollView;
    }
}
