using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine;
using System;
using System.IO;
using Random = UnityEngine.Random;

public class DungeonEscapeEnvController : MonoBehaviour
{
    [System.Serializable]
    public class PlayerInfo
    {
        public PushAgentEscape Agent;
        [HideInInspector]
        public Vector3 StartingPos;
        [HideInInspector]
        public Quaternion StartingRot;
        [HideInInspector]
        public Rigidbody Rb;
        [HideInInspector]
        public Collider Col;
    }

    [System.Serializable]
    public class DragonInfo
    {
        public SimpleNPC Agent;
        [HideInInspector]
        public Vector3 StartingPos;
        [HideInInspector]
        public Quaternion StartingRot;
        [HideInInspector]
        public Rigidbody Rb;
        [HideInInspector]
        public Collider Col;
        public Transform T;
        public bool IsDead;
    }

    /// <summary>
    /// Max Academy steps before this platform resets
    /// </summary>
    /// <returns></returns>
    [Header("Max Environment Steps")] public int MaxEnvironmentSteps = 25000;
    private int m_ResetTimer;

    /// <summary>
    /// The area bounds.
    /// </summary>
    [HideInInspector]
    public Bounds areaBounds;
    /// <summary>
    /// The ground. The bounds are used to spawn the elements.
    /// </summary>
    public GameObject ground;

    Material m_GroundMaterial; //cached on Awake()

    /// <summary>
    /// We will be changing the ground material based on success/failue
    /// </summary>
    Renderer m_GroundRenderer;

    public List<PlayerInfo> AgentsList = new List<PlayerInfo>();
    public List<DragonInfo> DragonsList = new List<DragonInfo>();
    private Dictionary<PushAgentEscape, PlayerInfo> m_PlayerDict = new Dictionary<PushAgentEscape, PlayerInfo>();
    public bool UseRandomAgentRotation = true;
    public bool UseRandomAgentPosition = true;
    PushBlockSettings m_PushBlockSettings;

    private int m_NumberOfRemainingPlayers;
    public GameObject Key;
    public GameObject Door;
    public GameObject Tombstone;
    private SimpleMultiAgentGroup m_AgentGroup;

    
    public bool EnableEpisodeLogging = true;
 
    private string runID;
    private int episodeCount = 0;
    private string episodeLogPath;
    private string agentLogPath;
    private string npcLogPath;


    void Start()
    {
        System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

        // Get the ground's bounds
        areaBounds = ground.GetComponent<Collider>().bounds;
        // Get the ground renderer so we can change the material when a goal is scored
        m_GroundRenderer = ground.GetComponent<Renderer>();
        // Starting material
        m_GroundMaterial = m_GroundRenderer.material;
        m_PushBlockSettings = FindObjectOfType<PushBlockSettings>();

        //Reset Players Remaining
        m_NumberOfRemainingPlayers = AgentsList.Count;

        //Hide The Key
        Key.SetActive(false);

        // Initialize TeamManager
        m_AgentGroup = new SimpleMultiAgentGroup();
        foreach (var item in AgentsList)
        {
            item.StartingPos = item.Agent.transform.position;
            item.StartingRot = item.Agent.transform.rotation;
            item.Rb = item.Agent.GetComponent<Rigidbody>();
            item.Col = item.Agent.GetComponent<Collider>();
            // Add to team manager
            m_AgentGroup.RegisterAgent(item.Agent);
        }
        foreach (var item in DragonsList)
        {
            item.StartingPos = item.Agent.transform.position;
            item.StartingRot = item.Agent.transform.rotation;
            item.T = item.Agent.transform;
            item.Col = item.Agent.GetComponent<Collider>();
        }

        Logging();

        ResetScene();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        m_ResetTimer += 1;
        ApproximateStats();
        if (m_ResetTimer >= MaxEnvironmentSteps && MaxEnvironmentSteps > 0)
        {
            LogEpisode("timeout");
            m_AgentGroup.GroupEpisodeInterrupted();
            ResetScene();
        }
    }

     void ApproximateStats()
   {
       if (!EnableEpisodeLogging) return;

       foreach (var item in AgentsList)
       {
           if (!item.Agent.gameObject.activeInHierarchy) continue; // skip dead agents
           Vector3 pos = item.Agent.transform.position;

           float dragonDistance = float.MaxValue; // stays MaxValue if no dragon is active
           foreach (var d in DragonsList)
           {
               if (!d.Agent || !d.Agent.gameObject.activeInHierarchy) continue;
               dragonDistance = Mathf.Min(dragonDistance, Vector3.Distance(pos, d.Agent.transform.position));
           }

           float keyDistance = (Key != null && Key.activeInHierarchy)
               ? Vector3.Distance(pos, Key.transform.position) : -1f;
           float doorDistance = (Door != null)
               ? Vector3.Distance(pos, Door.transform.position) : -1f;

           item.Agent.ApproximateStats(dragonDistance, keyDistance, doorDistance);
       }
   }

    public void TouchedHazard(PushAgentEscape agent)
    {
        m_NumberOfRemainingPlayers--;
        if (m_NumberOfRemainingPlayers == 0 || agent.IHaveAKey)
        {
            LogEpisode("hazard");
            m_AgentGroup.EndGroupEpisode();
            ResetScene();
        }
        else
        {
            agent.gameObject.SetActive(false);
        }
    }

    public void UnlockDoor()
    {
        m_AgentGroup.AddGroupReward(1f);
        StartCoroutine(GoalScoredSwapGroundMaterial(m_PushBlockSettings.goalScoredMaterial, 0.5f));

        print("Unlocked Door");
        LogEpisode("door_unlocked");
        m_AgentGroup.EndGroupEpisode();

        ResetScene();
    }

    public void KilledByBaddie(PushAgentEscape agent, Collision baddieCol)
    {
        baddieCol.gameObject.SetActive(false);
        m_NumberOfRemainingPlayers--;
        agent.gameObject.SetActive(false);
        print($"{baddieCol.gameObject.name} ate {agent.transform.name}");

        //Spawn Tombstone
        Tombstone.transform.SetPositionAndRotation(agent.transform.position, agent.transform.rotation);
        Tombstone.SetActive(true);

        //Spawn the Key Pickup
        Key.transform.SetPositionAndRotation(baddieCol.collider.transform.position, baddieCol.collider.transform.rotation);
        Key.SetActive(true);
    }

    /// <summary>
    /// Use the ground's bounds to pick a random spawn position.
    /// </summary>
    public Vector3 GetRandomSpawnPos()
    {
        var foundNewSpawnLocation = false;
        var randomSpawnPos = Vector3.zero;
        while (foundNewSpawnLocation == false)
        {
            var randomPosX = Random.Range(-areaBounds.extents.x * m_PushBlockSettings.spawnAreaMarginMultiplier,
                areaBounds.extents.x * m_PushBlockSettings.spawnAreaMarginMultiplier);

            var randomPosZ = Random.Range(-areaBounds.extents.z * m_PushBlockSettings.spawnAreaMarginMultiplier,
                areaBounds.extents.z * m_PushBlockSettings.spawnAreaMarginMultiplier);
            randomSpawnPos = ground.transform.position + new Vector3(randomPosX, 1f, randomPosZ);
            if (Physics.CheckBox(randomSpawnPos, new Vector3(2.5f, 0.01f, 2.5f)) == false)
            {
                foundNewSpawnLocation = true;
            }
        }
        return randomSpawnPos;
    }

    /// <summary>
    /// Swap ground material, wait time seconds, then swap back to the regular material.
    /// </summary>
    IEnumerator GoalScoredSwapGroundMaterial(Material mat, float time)
    {
        m_GroundRenderer.material = mat;
        yield return new WaitForSeconds(time); // Wait for 2 sec
        m_GroundRenderer.material = m_GroundMaterial;
    }

    public void BaddieTouchedBlock()
    {
        LogEpisode("baddie_touched_block");
        m_AgentGroup.EndGroupEpisode();

        // Swap ground material for a bit to indicate we scored.
        StartCoroutine(GoalScoredSwapGroundMaterial(m_PushBlockSettings.failMaterial, 0.5f));
        ResetScene();
    }

    Quaternion GetRandomRot()
    {
        return Quaternion.Euler(0, Random.Range(0.0f, 360.0f), 0);
    }

    void ResetScene()
    {

        //Reset counter
        m_ResetTimer = 0;

        //Reset Players Remaining
        m_NumberOfRemainingPlayers = AgentsList.Count;

        //Random platform rot
        var rotation = Random.Range(0, 4);
        var rotationAngle = rotation * 90f;
        transform.Rotate(new Vector3(0f, rotationAngle, 0f));

        //Reset Agents
        foreach (var item in AgentsList)
        {
            var pos = UseRandomAgentPosition ? GetRandomSpawnPos() : item.StartingPos;
            var rot = UseRandomAgentRotation ? GetRandomRot() : item.StartingRot;

            item.Agent.transform.SetPositionAndRotation(pos, rot);
            item.Rb.linearVelocity = Vector3.zero;
            item.Rb.angularVelocity = Vector3.zero;
            item.Agent.MyKey.SetActive(false);
            item.Agent.IHaveAKey = false;
            item.Agent.gameObject.SetActive(true);
            m_AgentGroup.RegisterAgent(item.Agent);
        }

        //Reset Key
        Key.SetActive(false);

        //Reset Tombstone
        Tombstone.SetActive(false);

        //End Episode
        foreach (var item in DragonsList)
        {
            if (!item.Agent)
            {
                return;
            }
            item.Agent.transform.SetPositionAndRotation(item.StartingPos, item.StartingRot);
            item.Agent.SetRandomWalkSpeed();
            item.Agent.gameObject.SetActive(true);
        }

        foreach (var item in AgentsList){
            Vector3 p = item.Agent.transform.position;
            float toDoor = Door != null ? Vector3.Distance(p, Door.transform.position) : -1f;
            float toDragon = float.MaxValue;
            foreach (var d in DragonsList){
                if (!d.Agent) continue;
                toDragon = Mathf.Min(toDragon, Vector3.Distance(p, d.Agent.transform.position));
            }
            item.Agent.SetSpawnDistances(toDoor, toDragon);
        }
    }

     void Logging()
    {
        if (!EnableEpisodeLogging) return;
 
        runID = Guid.NewGuid().ToString("N").Substring(0, 6);
 
        string logDir = Path.Combine(Application.dataPath, "../EpisodeLogs");
        Directory.CreateDirectory(logDir);
 
        episodeLogPath = Path.Combine(logDir, "episode_log.csv");
        agentLogPath = Path.Combine(logDir, "agent_log.csv");
        npcLogPath = Path.Combine(logDir, "npc_log.csv");
 
        WriteHeaderIfMissing(episodeLogPath, "run_id,episode,end_reason,steps_taken,num_players_start,num_players_remaining,num_dragons");
        WriteHeaderIfMissing(agentLogPath, "run_id,episode,agent_name,policy_label,start_distance_to_door,start_distance_to_dragon,steps_to_key_pickup,got_key,unlocked_door,death_cause,min_distance_to_dragon,steps_near_dragon,min_distance_to_key,min_distance_to_door");        
        WriteHeaderIfMissing(npcLogPath, "run_id,episode,npc_name,walk_speed");
    }
 

    void WriteHeaderIfMissing(string path, string header)
    {
        if (!File.Exists(path))
        {
            File.AppendAllText(path, header + "\n");
        }
    }
 
    string FormatMaybeUnset(float value)
    {
        return value >= float.MaxValue ? "-1" : value.ToString("F2");
    }
 
    void LogEpisode(string reason)
    {
        if (!EnableEpisodeLogging) return;
 
        episodeCount++;
  
        File.AppendAllText(episodeLogPath, string.Join(",",
            runID, episodeCount, reason, m_ResetTimer,
            AgentsList.Count, m_NumberOfRemainingPlayers, DragonsList.Count) + "\n");

        foreach (var item in AgentsList)
        {
            var s = item.Agent.GetEpisodeStats();
            File.AppendAllText(agentLogPath, string.Join(",",
                runID, episodeCount, s.AgentName, s.PolicyLabel,
                FormatMaybeUnset(s.StartDistanceToDoor), FormatMaybeUnset(s.StartDistanceToDragon),
                s.StepsToKeyPickup, s.GotKey, s.UnlockedDoor, s.DeathCause,
                FormatMaybeUnset(s.MinDistanceToDragon), s.StepsNearDragon,
                FormatMaybeUnset(s.MinDistanceToKey), FormatMaybeUnset(s.MinDistanceToDoor)) + "\n");
        }

        foreach (var item in DragonsList)
        {
            if (!item.Agent) continue;
            var s = item.Agent.GetEpisodeStats();
            File.AppendAllText(npcLogPath, string.Join(",",
                runID, episodeCount, s.NpcName, s.WalkSpeed.ToString("F3")) + "\n");
        }
    }
}
 
