using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace UI
{
    /// <summary>
    /// Handles keyboard and gamepad input for menu navigation
    /// Works alongside PlayerInputManager for a unified input experience
    /// </summary>
    public class MenuInputHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private PlayerInputManager playerInputManager;
        
        [Header("Navigation Settings")]
        [SerializeField] private bool enableKeyboardNavigation = true;
        [SerializeField] private bool enableGamepadNavigation = true;
        [SerializeField] private float navigationRepeatDelay = 0.5f;
        [SerializeField] private float navigationRepeatRate = 0.1f;
        
        [Header("Audio Feedback")]
        [SerializeField] private AudioSource sfxAudioSource;
        [SerializeField] private AudioClip navigationSound;
        [SerializeField] private AudioClip selectSound;
        [SerializeField] private AudioClip backSound;
        
        // UI Elements
        private VisualElement root;
        private List<Button> currentMenuButtons = new List<Button>();
        private int selectedButtonIndex = 0;
        
        // Input timing
        private float lastNavigationTime;
        private float nextRepeatTime;
        private bool isRepeating;
        
        // State
        private bool isMenuActive = true;
        private MenuState currentState = MenuState.MainMenu;
        
        private enum MenuState
        {
            MainMenu,
            SongSelection,
            Settings
        }
        
        void Start()
        {
            if (uiDocument != null)
            {
                root = uiDocument.rootVisualElement;
            }
            
            // Subscribe to input events if PlayerInputManager is assigned
            if (playerInputManager != null)
            {
                playerInputManager.OnMoveInput += HandleMenuNavigation;
            }
            
            UpdateCurrentMenuButtons();
        }
        
        void Update()
        {
            if (!isMenuActive) return;
            
            // Keyboard navigation (if enabled)
            if (enableKeyboardNavigation)
            {
                HandleKeyboardInput();
            }
            
            // Gamepad navigation (if enabled and PlayerInputManager not handling it)
            if (enableGamepadNavigation && playerInputManager == null)
            {
                HandleGamepadInput();
            }
            
            // Handle back/cancel
            HandleBackInput();
        }
        
        void HandleKeyboardInput()
        {
            // Up/Down navigation
            if (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame)
            {
                NavigateUp();
            }
            else if (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame)
            {
                NavigateDown();
            }
            
            // Select
            if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                SelectCurrentButton();
            }
            
            // Quick access numbers
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                SelectButton(0); // Start
            }
            else if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                SelectButton(1); // Settings
            }
            else if (Keyboard.current.digit3Key.wasPressedThisFrame)
            {
                SelectButton(2); // Exit
            }
        }
        
        void HandleGamepadInput()
        {
            var gamepad = Gamepad.current;
            if (gamepad == null) return;
            
            // D-Pad or Left Stick navigation
            Vector2 dpad = gamepad.dpad.ReadValue();
            Vector2 leftStick = gamepad.leftStick.ReadValue();
            
            bool upPressed = dpad.y > 0.5f || leftStick.y > 0.5f;
            bool downPressed = dpad.y < -0.5f || leftStick.y < -0.5f;
            
            float currentTime = Time.time;
            
            // Handle repeat navigation
            if (upPressed || downPressed)
            {
                if (!isRepeating)
                {
                    // First press
                    if (upPressed) NavigateUp();
                    else NavigateDown();
                    
                    lastNavigationTime = currentTime;
                    nextRepeatTime = currentTime + navigationRepeatDelay;
                    isRepeating = true;
                }
                else if (currentTime >= nextRepeatTime)
                {
                    // Repeat
                    if (upPressed) NavigateUp();
                    else NavigateDown();
                    
                    nextRepeatTime = currentTime + navigationRepeatRate;
                }
            }
            else
            {
                isRepeating = false;
            }
            
            // A button to select
            if (gamepad.buttonSouth.wasPressedThisFrame)
            {
                SelectCurrentButton();
            }
        }
        
        void HandleBackInput()
        {
            bool backPressed = false;
            
            // Keyboard ESC
            if (enableKeyboardNavigation && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                backPressed = true;
            }
            
            // Gamepad B button
            if (enableGamepadNavigation)
            {
                var gamepad = Gamepad.current;
                if (gamepad != null && gamepad.buttonEast.wasPressedThisFrame)
                {
                    backPressed = true;
                }
            }
            
            if (backPressed)
            {
                HandleBackAction();
            }
        }
        
        void HandleMenuNavigation(int direction)
        {
            // Called from PlayerInputManager
            if (direction < 0)
                NavigateUp();
            else if (direction > 0)
                NavigateDown();
        }
        
        void NavigateUp()
        {
            if (currentMenuButtons.Count == 0) return;
            
            selectedButtonIndex--;
            if (selectedButtonIndex < 0)
                selectedButtonIndex = currentMenuButtons.Count - 1;
            
            FocusCurrentButton();
            PlaySound(navigationSound);
        }
        
        void NavigateDown()
        {
            if (currentMenuButtons.Count == 0) return;
            
            selectedButtonIndex++;
            if (selectedButtonIndex >= currentMenuButtons.Count)
                selectedButtonIndex = 0;
            
            FocusCurrentButton();
            PlaySound(navigationSound);
        }
        
        void SelectCurrentButton()
        {
            if (currentMenuButtons.Count == 0) return;
            if (selectedButtonIndex < 0 || selectedButtonIndex >= currentMenuButtons.Count) return;
            
            Button button = currentMenuButtons[selectedButtonIndex];
            
            // Simulate click
            using (var clickEvent = ClickEvent.GetPooled())
            {
                clickEvent.target = button;
                button.SendEvent(clickEvent);
            }
            
            PlaySound(selectSound);
        }
        
        void SelectButton(int index)
        {
            if (index < 0 || index >= currentMenuButtons.Count) return;
            
            selectedButtonIndex = index;
            SelectCurrentButton();
        }
        
        void FocusCurrentButton()
        {
            if (currentMenuButtons.Count == 0) return;
            if (selectedButtonIndex < 0 || selectedButtonIndex >= currentMenuButtons.Count) return;
            
            currentMenuButtons[selectedButtonIndex].Focus();
        }
        
        void HandleBackAction()
        {
            PlaySound(backSound);
            
            // Navigate back based on current state
            switch (currentState)
            {
                case MenuState.SongSelection:
                case MenuState.Settings:
                    // Go back to main menu
                    // Find and click the back button if it exists
                    var settingsPanel = root?.Q<VisualElement>("SettingsPanel");
                    if (settingsPanel != null && settingsPanel.style.display == DisplayStyle.Flex)
                    {
                        var backButton = settingsPanel.Q<Button>();
                        if (backButton != null)
                        {
                            using (var clickEvent = ClickEvent.GetPooled())
                            {
                                clickEvent.target = backButton;
                                backButton.SendEvent(clickEvent);
                            }
                        }
                    }
                    currentState = MenuState.MainMenu;
                    UpdateCurrentMenuButtons();
                    break;
                    
                case MenuState.MainMenu:
                    // Maybe show a "Do you want to quit?" dialog
                    // For now, do nothing or trigger exit
                    break;
            }
        }
        
        void PlaySound(AudioClip clip)
        {
            if (sfxAudioSource != null && clip != null)
            {
                sfxAudioSource.PlayOneShot(clip);
            }
        }
        
        public void UpdateCurrentMenuButtons()
        {
            currentMenuButtons.Clear();
            
            if (root == null) return;
            
            // Find visible buttons based on current state
            var mainContent = root.Q<VisualElement>("MainContent");
            
            if (mainContent != null && mainContent.style.display != DisplayStyle.None)
            {
                // Get main menu buttons
                var startButton = root.Q<Button>("Start");
                var settingsButton = root.Q<Button>("Settings");
                var exitButton = root.Q<Button>("Exit");
                
                if (startButton != null) currentMenuButtons.Add(startButton);
                if (settingsButton != null) currentMenuButtons.Add(settingsButton);
                if (exitButton != null) currentMenuButtons.Add(exitButton);
                
                currentState = MenuState.MainMenu;
            }
            
            // Check if we're in song selection
            var startPanel = root.Q<VisualElement>("StartPanel");
            if (startPanel != null && startPanel.style.display == DisplayStyle.Flex)
            {
                currentState = MenuState.SongSelection;
                currentMenuButtons.Clear();
                
                // Get all song buttons from the scroll view
                var songsScrollView = root.Q<ScrollView>("Songs");
                if (songsScrollView != null)
                {
                    var buttons = songsScrollView.Query<Button>().ToList();
                    currentMenuButtons.AddRange(buttons);
                }
            }
            
            // Check if we're in settings
            var settingsPanel = root.Q<VisualElement>("SettingsPanel");
            if (settingsPanel != null && settingsPanel.style.display == DisplayStyle.Flex)
            {
                currentState = MenuState.Settings;
                // Settings might have sliders and buttons, handle accordingly
            }
            
            // Reset selection
            selectedButtonIndex = 0;
            if (currentMenuButtons.Count > 0)
            {
                FocusCurrentButton();
            }
        }
        
        public void SetMenuActive(bool active)
        {
            isMenuActive = active;
        }
        
        void OnDestroy()
        {
            // Unsubscribe from events
            if (playerInputManager != null)
            {
                playerInputManager.OnMoveInput -= HandleMenuNavigation;
            }
        }
    }
}