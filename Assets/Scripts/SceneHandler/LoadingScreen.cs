using System;
using UnityEngine;
using UnityEngine.UI;

public class LoadingScreenController : MonoBehaviour
{
    [Header("References")]
    public AudioMapGenerator generator;
    public GameObject loadingPanel; // Drag your UI Panel here
    
    // Optional: Add a spinning icon or text reference here if you want to animate it

    private void Awake()
    {
        // Set up the event listeners as early as possible (in Awake) 
        // to prevent missed events if the generator starts too quickly.
        if (generator != null)
        {
            generator.OnMapGenerationStart += ShowLoading;
            generator.OnMapGenerationComplete += HideLoading;
        }
        else
        {
            Debug.LogError("LoadingScreenController: AudioMapGenerator reference is null.");
        }

        if (loadingPanel == null)
        {
            Debug.LogError("LoadingScreenController: loadingPanel is null");
        }
        else
        {
            // Ensure the panel is initially hidden.
            loadingPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events when the object is destroyed
        if (generator != null)
        {
            generator.OnMapGenerationStart -= ShowLoading;
            generator.OnMapGenerationComplete -= HideLoading;
        }
    }
    
    // NOTE: OnEnable/OnDisable are no longer needed for subscription

    private void ShowLoading()
    {
        if(loadingPanel != null) 
            loadingPanel.SetActive(true);

        // Pause the game time
        Time.timeScale = 0f; 
        Debug.Log("Game Paused for Loading...");
    }

    private void HideLoading()
    {
        if(loadingPanel != null) 
            loadingPanel.SetActive(false);

        // Resume game time
        Time.timeScale = 1f;
        Debug.Log("Game Resumed.");
    }
}