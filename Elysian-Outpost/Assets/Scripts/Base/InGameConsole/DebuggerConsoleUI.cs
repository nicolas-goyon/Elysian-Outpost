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
        _gameInputs.OnConsoleEvent += OpenConsole;
        _inputField.onSubmit.AddListener(OnCommandSubmitted);
        // _debuggerConsole = new DebuggerConsole();
        DebuggerConsole.OnLogEmitted += DisplayLogEvent;
        // DebuggerConsole.Enable();
        
        DebuggerConsole.ConsoleCommand logCmd = new DebuggerConsole.ConsoleCommand("log", "Logs a message to the console.", args =>
        {
            if (args.Length == 0)
            {
                DebuggerConsole.Log("Usage: log <message>");
                return;
            }
            string message = string.Join(" ", args);
            DebuggerConsole.Log(message);
        });
        DebuggerConsole.AddCommand(logCmd);
    }

    public void CloseConsole()
    {
        _consoleUI.SetActive(false);
        _cameraMovements.Set(CameraMovements.CameraState.FreeFly);
        _inputField.DeactivateInputField();
    }
    
    private void OpenConsole()
    {
        _consoleUI.SetActive(true);
        _cameraMovements.Set(CameraMovements.CameraState.OnMenu);
        _inputField.ActivateInputField();
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
}
