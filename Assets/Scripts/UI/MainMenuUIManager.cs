using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SceneHandler;

namespace UI
{
    /// <summary>
    /// Main Menu UI Manager - Updated to work with BeatmapManager
    /// Waits for beatmaps to be loaded before showing menu
    /// </summary>
    public class MainMenuUIManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private RhythmGameManager gameManager;
        [SerializeField] private BeatmapManager beatmapManager;
        
        [Header("Audio Settings")]
        [SerializeField] private AudioSource musicAudioSource;
        [SerializeField] private AudioSource sfxAudioSource;
        
        [Header("Color Configuration")]
        [SerializeField] private Color primaryColor = new Color(1f, 0.96f, 0.7f);
        [SerializeField] private Color secondaryColor = new Color(1f, 0.46f, 0f);
        [SerializeField] private Color accentColor = new Color(0.37f, 0f, 1f);
        [SerializeField] private Color hoverColor = new Color(1f, 0.89f, 0.56f);
        [SerializeField] private Color clickColor = new Color(1f, 0.6f, 0.2f);
        
        // UI Elements
        private VisualElement root;
        private VisualElement mainContent;
        private VisualElement background;
        
        // Buttons
        private Button startButton;
        private Button settingsButton;
        private Button exitButton;
        
        // Panels
        private VisualElement startPanel;
        private VisualElement settingsPanel;
        private ScrollView songsScrollView;
        
        // Settings UI
        private Slider musicVolumeSlider;
        private Slider sfxVolumeSlider;
        private Label musicVolumeLabel;
        private Label sfxVolumeLabel;
        private Button settingsBackButton;
        
        // Loading Screen
        private VisualElement loadingScreen;
        private Label loadingText;
        private VisualElement loadingProgressBar;
        private VisualElement loadingProgressFill;
        
        // State
        private RhythmMapData selectedSong;
        private bool isInitialized = false;
        private List<RhythmMapData> availableSongs = new List<RhythmMapData>();

        void OnEnable()
        {
            StartCoroutine(InitializeUIDelayed());
        }

        IEnumerator InitializeUIDelayed()
        {
            yield return null;
            InitializeUI();
            
            // Show loading screen immediately
            ShowInitialLoadingScreen();
            
            // Wait for beatmap manager to initialize
            if (beatmapManager != null)
            {
                StartCoroutine(WaitForBeatmapManager());
            }
            else
            {
                Debug.LogWarning("[MainMenuUI] No BeatmapManager assigned!");
                HideLoadingScreen();
            }
        }

        IEnumerator WaitForBeatmapManager()
        {
            loadingText.text = "Scanning for beatmaps...";
            
            // Wait for initialization
            while (!beatmapManager.IsInitialized())
            {
                yield return new WaitForSeconds(0.1f);
            }
            
            loadingText.text = "Loading complete!";
            yield return new WaitForSeconds(0.5f);
            
            // Fade out loading screen
            yield return StartCoroutine(FadeOutLoadingScreen());
            
            // Show main menu
            ShowMainMenu();
        }

        void InitializeUI()
        {
            if (uiDocument == null)
            {
                Debug.LogError("[MainMenuUI] UIDocument not assigned!");
                return;
            }

            root = uiDocument.rootVisualElement;
            
            if (root == null)
            {
                Debug.LogError("[MainMenuUI] Root visual element is null!");
                return;
            }

            mainContent = root.Q<VisualElement>("MainContent");
            background = root.Q<VisualElement>("background");
            
            startButton = root.Q<Button>("Start");
            settingsButton = root.Q<Button>("Settings");
            exitButton = root.Q<Button>("Exit");
            
            startPanel = root.Q<VisualElement>("StartPanel");
            settingsPanel = root.Q<VisualElement>("SettingsPanel");
            songsScrollView = root.Q<ScrollView>("Songs");
            
            SetupButtons();
            SetupStartPanel();
            SetupSettingsPanel();
            CreateLoadingScreen();
            
            // Hide menu initially
            if (mainContent != null)
                mainContent.style.display = DisplayStyle.None;
            
            isInitialized = true;
            
            Debug.Log("[MainMenuUI] UI Initialized successfully!");
        }

        void SetupButtons()
        {
            if (startButton != null)
            {
                startButton.clicked += OnStartButtonClicked;
                ApplyButtonEffects(startButton);
            }
            
            if (settingsButton != null)
            {
                settingsButton.clicked += OnSettingsButtonClicked;
                ApplyButtonEffects(settingsButton);
            }
            
            if (exitButton != null)
            {
                exitButton.clicked += OnExitButtonClicked;
                ApplyButtonEffects(exitButton);
            }
        }

        void ApplyButtonEffects(Button button)
        {
            button.RegisterCallback<MouseEnterEvent>(evt =>
            {
                button.style.color = new StyleColor(hoverColor);
                button.style.scale = new StyleScale(new Vector2(1.1f, 1.1f));
                button.style.transitionDuration = new StyleList<TimeValue>(new List<TimeValue> { new TimeValue(0.2f) });
            });
            
            button.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                button.style.color = new StyleColor(Color.white);
                button.style.scale = new StyleScale(new Vector2(1f, 1f));
            });
            
            button.RegisterCallback<MouseDownEvent>(evt =>
            {
                button.style.color = new StyleColor(clickColor);
                button.style.scale = new StyleScale(new Vector2(1.05f, 1.05f));
            });
            
            button.RegisterCallback<MouseUpEvent>(evt =>
            {
                button.style.color = new StyleColor(hoverColor);
                button.style.scale = new StyleScale(new Vector2(1.1f, 1.1f));
            });
        }

        void SetupStartPanel()
        {
            if (startPanel == null)
            {
                Debug.LogWarning("[MainMenuUI] StartPanel not found!");
                return;
            }
            
            startPanel.style.display = DisplayStyle.None;
        }

        void SetupSettingsPanel()
        {
            if (settingsPanel == null)
            {
                Debug.LogWarning("[MainMenuUI] SettingsPanel not found!");
                return;
            }
            
            settingsPanel.Clear();
            
            var settingsContainer = new VisualElement();
            settingsContainer.style.flexGrow = 1;
            settingsContainer.style.paddingTop = 32;
            settingsContainer.style.paddingLeft = 32;
            settingsContainer.style.paddingRight = 32;
            settingsContainer.style.paddingBottom = 32;
            
            var settingsTitle = new Label("Settings");
            settingsTitle.style.fontSize = 48;
            settingsTitle.style.color = primaryColor;
            settingsTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            settingsTitle.style.marginBottom = 32;
            settingsContainer.Add(settingsTitle);
            
            // Load saved volumes
            float savedMusicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.8f);
            float savedSfxVolume = PlayerPrefs.GetFloat("SFXVolume", 0.8f);
            
            if (musicAudioSource != null)
                musicAudioSource.volume = savedMusicVolume;
            if (sfxAudioSource != null)
                sfxAudioSource.volume = savedSfxVolume;
            
            var musicContainer = CreateVolumeSlider("Music Volume", out musicVolumeSlider, out musicVolumeLabel);
            musicVolumeSlider.value = savedMusicVolume * 100;
            musicVolumeSlider.RegisterValueChangedCallback(evt =>
            {
                float volume = evt.newValue / 100f;
                if (musicAudioSource != null)
                    musicAudioSource.volume = volume;
                musicVolumeLabel.text = $"Music Volume: {evt.newValue:F0}%";
                PlayerPrefs.SetFloat("MusicVolume", volume);
            });
            settingsContainer.Add(musicContainer);
            
            var sfxContainer = CreateVolumeSlider("SFX Volume", out sfxVolumeSlider, out sfxVolumeLabel);
            sfxVolumeSlider.value = savedSfxVolume * 100;
            sfxVolumeSlider.RegisterValueChangedCallback(evt =>
            {
                float volume = evt.newValue / 100f;
                if (sfxAudioSource != null)
                    sfxAudioSource.volume = volume;
                sfxVolumeLabel.text = $"SFX Volume: {evt.newValue:F0}%";
                PlayerPrefs.SetFloat("SFXVolume", volume);
            });
            settingsContainer.Add(sfxContainer);
            
            settingsBackButton = new Button { text = "Back" };
            settingsBackButton.style.fontSize = 40;
            settingsBackButton.style.color = Color.white;
            settingsBackButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            settingsBackButton.style.marginTop = 48;
            settingsBackButton.style.height = 60;
            settingsBackButton.style.backgroundColor = new Color(0, 0, 0, 0);
            settingsBackButton.clicked += ShowMainMenu;
            ApplyButtonEffects(settingsBackButton);
            settingsContainer.Add(settingsBackButton);
            
            settingsPanel.Add(settingsContainer);
            settingsPanel.style.display = DisplayStyle.None;
        }

        VisualElement CreateVolumeSlider(string labelText, out Slider slider, out Label valueLabel)
        {
            var container = new VisualElement();
            container.style.marginBottom = 24;
            
            valueLabel = new Label(labelText + ": 80%");
            valueLabel.style.fontSize = 32;
            valueLabel.style.color = Color.white;
            valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            valueLabel.style.marginBottom = 8;
            container.Add(valueLabel);
            
            slider = new Slider(0, 100);
            slider.value = 80;
            slider.style.height = 40;
            slider.style.marginTop = 8;
            slider.style.marginBottom = 8;
            
            var dragger = slider.Q("unity-dragger");
            if (dragger != null)
            {
                dragger.style.backgroundColor = secondaryColor;
                dragger.style.width = 30;
                dragger.style.height = 30;
                dragger.style.borderTopLeftRadius = 15;
                dragger.style.borderTopRightRadius = 15;
                dragger.style.borderBottomLeftRadius = 15;
                dragger.style.borderBottomRightRadius = 15;
            }
            
            var tracker = slider.Q("unity-tracker");
            if (tracker != null)
            {
                tracker.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
                tracker.style.height = 10;
            }
            
            container.Add(slider);
            
            return container;
        }

        public void SetAvailableSongs(List<RhythmMapData> songs)
        {
            availableSongs = songs;
            
            if (isInitialized)
            {
                PopulateSongList();
            }
        }

        void PopulateSongList()
        {
            if (songsScrollView == null)
            {
                Debug.LogWarning("[MainMenuUI] Songs ScrollView not found!");
                return;
            }
            
            songsScrollView.Clear();
            
            if (availableSongs.Count == 0)
            {
                var noSongsLabel = new Label("No songs available. Add .osz files to StreamingAssets/Beatmaps folder.");
                noSongsLabel.style.fontSize = 24;
                noSongsLabel.style.color = Color.gray;
                noSongsLabel.style.marginTop = 16;
                noSongsLabel.style.whiteSpace = WhiteSpace.Normal;
                songsScrollView.Add(noSongsLabel);
                return;
            }
            
            foreach (var song in availableSongs)
            {
                CreateSongButton(song);
            }
            
            Debug.Log($"[MainMenuUI] Populated song list with {availableSongs.Count} songs");
        }

        void CreateSongButton(RhythmMapData songData)
        {
            if (songData == null) return;
            
            var songButton = new Button();
            
            string songName = !string.IsNullOrEmpty(songData.mapName) ? songData.mapName : "Unnamed Song";
            songButton.text = songName;
            
            songButton.AddToClassList("menu-item");
            songButton.style.backgroundColor = new Color(0, 0, 0, 0);
            songButton.style.borderTopWidth = 0;
            songButton.style.borderBottomWidth = 0;
            songButton.style.borderLeftWidth = 0;
            songButton.style.borderRightWidth = 0;
            songButton.style.marginBottom = 8;
            songButton.style.marginTop = 8;
            
            songButton.clicked += () => OnSongSelected(songData);
            
            songButton.RegisterCallback<MouseEnterEvent>(evt =>
            {
                songButton.style.color = hoverColor;
                songButton.style.scale = new StyleScale(new Vector2(1.05f, 1.05f));
                songButton.style.transitionDuration = new StyleList<TimeValue>(new List<TimeValue> { new TimeValue(0.15f) });
            });
            
            songButton.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                songButton.style.color = Color.white;
                songButton.style.scale = new StyleScale(new Vector2(1f, 1f));
            });
            
            songButton.RegisterCallback<MouseDownEvent>(evt =>
            {
                songButton.style.color = clickColor;
            });
            
            songsScrollView.Add(songButton);
        }

        void CreateLoadingScreen()
        {
            loadingScreen = new VisualElement();
            loadingScreen.name = "LoadingScreen";
            loadingScreen.style.position = Position.Absolute;
            loadingScreen.style.width = Length.Percent(100);
            loadingScreen.style.height = Length.Percent(100);
            loadingScreen.style.backgroundColor = new Color(0, 0, 0, 1);
            loadingScreen.style.alignItems = Align.Center;
            loadingScreen.style.justifyContent = Justify.Center;
            loadingScreen.style.display = DisplayStyle.None;
            
            loadingText = new Label("Loading...");
            loadingText.style.fontSize = 64;
            loadingText.style.color = primaryColor;
            loadingText.style.unityFontStyleAndWeight = FontStyle.Bold;
            loadingText.style.unityTextOutlineWidth = 2;
            loadingText.style.unityTextOutlineColor = secondaryColor;
            
            loadingScreen.Add(loadingText);
            root.Add(loadingScreen);
        }

        void ShowInitialLoadingScreen()
        {
            if (loadingScreen != null)
            {
                loadingScreen.style.display = DisplayStyle.Flex;
                loadingScreen.style.opacity = 1;
                loadingText.style.opacity = 1;
            }
        }

        void HideLoadingScreen()
        {
            if (loadingScreen != null)
            {
                loadingScreen.style.display = DisplayStyle.None;
            }
        }

        void OnStartButtonClicked()
        {
            Debug.Log("[MainMenuUI] Start button clicked");
            HideAllPanels();
            
            if (startPanel != null)
            {
                startPanel.style.display = DisplayStyle.Flex;
            }
        }

        void OnSettingsButtonClicked()
        {
            Debug.Log("[MainMenuUI] Settings button clicked");
            HideAllPanels();
            
            if (settingsPanel != null)
            {
                settingsPanel.style.display = DisplayStyle.Flex;
            }
        }

        void OnExitButtonClicked()
        {
            Debug.Log("[MainMenuUI] Exit button clicked");
            
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }

        void OnSongSelected(RhythmMapData songData)
        {
            Debug.Log($"[MainMenuUI] Song selected: {songData.mapName}");
            selectedSong = songData;
            StartCoroutine(LoadAndStartGame(songData));
        }

        IEnumerator LoadAndStartGame(RhythmMapData songData)
        {
            ShowLoadingScreen();
            yield return StartCoroutine(FadeInLoadingScreen());
            
            loadingText.text = "Loading song...";
            yield return new WaitForSeconds(0.3f);
            
            if (gameManager != null)
            {
                gameManager.SetMapAndPrepare(songData);
                
                loadingText.text = "Calibrating...";
                yield return new WaitForSeconds(0.3f);
                
                loadingText.text = "Preparing lanes...";
                yield return new WaitForSeconds(0.3f);
                
                // Wait for game to be ready
                while (!gameManager.IsReady())
                {
                    yield return new WaitForSeconds(0.1f);
                }
                
                loadingText.text = "Ready!";
                yield return new WaitForSeconds(0.3f);
                
                yield return StartCoroutine(FadeOutLoadingScreen());
                
                HideMainMenu();
                gameManager.BeginGameFromUI();
            }
            else
            {
                Debug.LogError("[MainMenuUI] Game Manager not assigned!");
                yield return StartCoroutine(FadeOutLoadingScreen());
            }
        }

        void ShowLoadingScreen()
        {
            if (loadingScreen != null)
            {
                loadingScreen.style.display = DisplayStyle.Flex;
                loadingScreen.style.opacity = 0;
                loadingText.style.opacity = 0;
            }
        }

        IEnumerator FadeInLoadingScreen()
        {
            float duration = 0.5f;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(0, 1, elapsed / duration);
                
                loadingScreen.style.opacity = alpha;
                loadingText.style.opacity = alpha;
                
                yield return null;
            }
            
            loadingScreen.style.opacity = 1;
            loadingText.style.opacity = 1;
        }

        IEnumerator FadeOutLoadingScreen()
        {
            float duration = 0.5f;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1, 0, elapsed / duration);
                
                loadingScreen.style.opacity = alpha;
                loadingText.style.opacity = alpha;
                
                yield return null;
            }
            
            loadingScreen.style.display = DisplayStyle.None;
        }

        void HideAllPanels()
        {
            if (startPanel != null)
                startPanel.style.display = DisplayStyle.None;
            
            if (settingsPanel != null)
                settingsPanel.style.display = DisplayStyle.None;
        }

        void ShowMainMenu()
        {
            HideAllPanels();
            
            if (mainContent != null)
                mainContent.style.display = DisplayStyle.Flex;
        }

        void HideMainMenu()
        {
            if (mainContent != null)
                mainContent.style.display = DisplayStyle.None;
        }

        void OnDisable()
        {
            if (startButton != null)
                startButton.clicked -= OnStartButtonClicked;
            
            if (settingsButton != null)
                settingsButton.clicked -= OnSettingsButtonClicked;
            
            if (exitButton != null)
                exitButton.clicked -= OnExitButtonClicked;
            
            if (settingsBackButton != null)
                settingsBackButton.clicked -= ShowMainMenu;
        }

        public RhythmMapData GetSelectedSong() => selectedSong;
        public void ShowMenu() => ShowMainMenu();
        public void HideUI() => HideMainMenu();
    }
}