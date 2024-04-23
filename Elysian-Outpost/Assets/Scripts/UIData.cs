using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UIData : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI fpsNumber;
    [SerializeField] private TextMeshProUGUI voxelCount;

    private VoxelControl voxelControl;


    private float deltaTime = 0.0f;
    private float fpsAccumulator = 0.0f;
    private int frameCount = 0;
    private float updateInterval = 0.5f;
    private float nextUpdateTime = 0.0f;

    // Start is called before the first frame update
    void Start()
    {
        voxelControl = VoxelControl.Instance;
        
    }

    // Update is called once per frame
    void Update()
    {
        //fpsNumber.text = (1 / Time.deltaTime).ToString("F0");
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        fpsAccumulator += Time.timeScale / Time.deltaTime;
        frameCount++;
        if (Time.realtimeSinceStartup > nextUpdateTime) {
            float fps = fpsAccumulator / frameCount;
            fpsNumber.text = fps.ToString("F0");
            fpsAccumulator = 0.0f;
            frameCount = 0;
            nextUpdateTime += updateInterval;
        }

        voxelCount.text = voxelControl.GetIndex().ToString();
        
    }
}
