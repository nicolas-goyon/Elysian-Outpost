using Base;
using Base.InGameConsole;
using TMPro;
using UnityEngine;

public class DebuggerConsoleUI : MonoBehaviour
{
    [SerializeField] private GameObject _consoleUI;
    [SerializeField] private CameraMovements _cameraMovements;
    [SerializeField] private GameInputs _gameInputs;
    
    [SerializeField] private TextMeshProUGUI _outputText;
    [SerializeField] private TMP_InputField _inputField;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _gameInputs._inputActions.PlayerMenuControls.ConsoleOpen.performed += OpenConsole;
        _inputField.onSubmit.AddListener(OnCommandSubmitted);
        DebuggerConsole.OnLogEmitted += DisplayLogEvent;

        DebuggerConsole.AddCommand(new DebuggerConsole.ConsoleCommand("log", "Logs a message to the console.", LogMessageCmd));
        DebuggerConsole.AddCommand(new DebuggerConsole.ConsoleCommand("clear", "Clears the console output.", args => Clear()));
        DebuggerConsole.AddCommand(new DebuggerConsole.ConsoleCommand("close", "Closes the console UI.", args => CloseConsole()));
        DebuggerConsole.AddCommand(new DebuggerConsole.ConsoleCommand("copy", "Copies the console output to clipboard.", args => CopyToClipboard()));
        DebuggerConsole.AddCommand(new DebuggerConsole.ConsoleCommand("help", "Displays a list of available commands.", args => DebuggerConsole.HelpCommand()));
        
        DebuggerConsole.Enable();
        
        
        CloseConsole();
    }
    
    public void CloseConsole()
    {
        _consoleUI.SetActive(false);
        _cameraMovements.Set(CameraMovements.CameraState.FreeFly);
        _inputField.DeactivateInputField();
        _gameInputs._inputActions.PlayerMenuControls.OpenCloseMenu.Enable();
    }
    
    private void OpenConsole(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        _consoleUI.SetActive(true);
        _cameraMovements.Set(CameraMovements.CameraState.OnMenu);
        _inputField.ActivateInputField();
        _gameInputs._inputActions.PlayerMenuControls.OpenCloseMenu.Disable();
    }
    
    private void OnCommandSubmitted(string command)
    {
        DebuggerConsole.ExecuteCommand(command);
        _inputField.text = string.Empty;
        _inputField.ActivateInputField();
    }

    private void DisplayLogEvent(string log)
    {
        _outputText.text += $"\n{log}";
    }
    
    public void CopyToClipboard()
    {
        string text = _outputText.text;
        Base.SystemPlugin.UniClipboard.SetText(text);
    }

    private void Clear()
    {
        _outputText.text = string.Empty;
    }
    
    private void LogMessageCmd(string[] args)
    {
        if (args.Length == 0)
        {
            DebuggerConsole.Log("Usage: log <message>");
            return;
        }
        string message = string.Join(" ", args);
        DebuggerConsole.Log(message);
    }
}
