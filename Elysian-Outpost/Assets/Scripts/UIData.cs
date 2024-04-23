using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UIData : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI fpsNumber;
    [SerializeField] private TextMeshProUGUI voxelCount;

    private VoxelControl voxelControl;

    // Start is called before the first frame update
    void Start()
    {
        voxelControl = VoxelControl.Instance;
        
    }

    // Update is called once per frame
    void Update()
    {
        fpsNumber.text = (1 / Time.deltaTime).ToString("F0");
        voxelCount.text = voxelControl.GetIndex().ToString();
        
    }
}
