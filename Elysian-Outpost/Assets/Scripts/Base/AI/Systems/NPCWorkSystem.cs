using System.Collections.Generic;
using Base.AI;
using Base.AI.Jobs;
using Base.Terrain;
using UnityEngine;

public class NPCWorkSystem : MonoBehaviour
{
    private List<CitizenEntity> _npcList = new();
    private List<MineAndDropJob> _jobList = new();
    [SerializeField] private TerrainHolder TerrainHolder;

    public void RegisterNPC(CitizenEntity npc)
    {
        if (_npcList.Contains(npc)) return;
        _npcList.Add(npc);
        MineAndDropJob job = new(npc, TerrainHolder);
        _jobList.Add(job);
        job.Start();
    }

    // Update is called once per frame
    void Update()
    {
        foreach (MineAndDropJob job in _jobList) job.Update();
    }
}