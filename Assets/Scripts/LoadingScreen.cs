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
        if (loadingPanel == null)
        {
            Debug.LogError("LoadingScreenController: loadingPanel is null");
        }
        else
        {
            loadingPanel.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (generator != null)
        {
            generator.OnMapGenerationStart += ShowLoading;
            generator.OnMapGenerationComplete += HideLoading;
        }
    }

    private void OnDisable()
    {
        if (generator != null)
        {
            generator.OnMapGenerationStart -= ShowLoading;
            generator.OnMapGenerationComplete -= HideLoading;
        }
    }

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