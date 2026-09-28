using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;


public struct AgentEpisodeStats{
    public string AgentName;
    public int StepsToKeyPickup;
    public bool GotKey;
    public bool UnlockedDoor;
    public string DeathCause;
    public float MinDistanceToDragon;
    public int StepsNearDragon;
    public float MinDistanceToKey;
    public float MinDistanceToDoor;
    public string PolicyLabel;
    public float StartDistanceToDoor;
    public float StartDistanceToDragon;
}

public class PushAgentEscape : Agent
{

    public GameObject MyKey; //my key gameobject. will be enabled when key picked up.
    public bool IHaveAKey; //have i picked up a key
    private PushBlockSettings m_PushBlockSettings;
    private Rigidbody m_AgentRb;
    private DungeonEscapeEnvController m_GameController;

    private const float DragonProximityThreshold = 5f; 
    private int stepsToKeyPickup;
    private bool unlockedDoor;
    private string deathCause;
    private float minDistanceToDragon = float.MaxValue;
    private int stepsNearDragon;
    private float minDistanceToKey = float.MaxValue;
    private float minDistanceToDoor = float.MaxValue;
    public string PolicyLabel = "unset";  
    private float startDistanceToDoor = -1f;
    private float startDistanceToDragon = float.MaxValue;

    public override void Initialize(){
        m_GameController = GetComponentInParent<DungeonEscapeEnvController>();
        m_AgentRb = GetComponent<Rigidbody>();
        m_PushBlockSettings = FindObjectOfType<PushBlockSettings>();
        MyKey.SetActive(false);
        IHaveAKey = false;
    }

    public override void OnEpisodeBegin()
    {
        MyKey.SetActive(false);
        IHaveAKey = false;

        stepsToKeyPickup = -1;
        unlockedDoor = false;
        deathCause = "none";
        minDistanceToDragon = float.MaxValue;
        stepsNearDragon = 0;
        minDistanceToKey = float.MaxValue;
        minDistanceToDoor = float.MaxValue;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(IHaveAKey);
    }

    /// <summary>
    /// Moves the agent according to the selected action.
    /// </summary>
    public void MoveAgent(ActionSegment<int> act)
    {
        var dirToGo = Vector3.zero;
        var rotateDir = Vector3.zero;

        var action = act[0];

        switch (action)
        {
            case 1:
                dirToGo = transform.forward * 1f;
                break;
            case 2:
                dirToGo = transform.forward * -1f;
                break;
            case 3:
                rotateDir = transform.up * 1f;
                break;
            case 4:
                rotateDir = transform.up * -1f;
                break;
            case 5:
                dirToGo = transform.right * -0.75f;
                break;
            case 6:
                dirToGo = transform.right * 0.75f;
                break;
        }
        transform.Rotate(rotateDir, Time.fixedDeltaTime * 200f);
        m_AgentRb.AddForce(dirToGo * m_PushBlockSettings.agentRunSpeed,
            ForceMode.VelocityChange);
    }

    /// <summary>
    /// Called every step of the engine. Here the agent takes an action.
    /// </summary>
    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        // Move the agent using the action.
        MoveAgent(actionBuffers.DiscreteActions);
    }

    void OnCollisionEnter(Collision col)
    {
        if (col.transform.CompareTag("lock"))
        {
            if (IHaveAKey)
            {
                MyKey.SetActive(false);
                IHaveAKey = false;
                unlockedDoor = true;
                m_GameController.UnlockDoor();
            }
        }
        if (col.transform.CompareTag("dragon"))
        {
            deathCause = "dragon";
            m_GameController.KilledByBaddie(this, col);
            MyKey.SetActive(false);
            IHaveAKey = false;
        }
        if (col.transform.CompareTag("portal"))
        {
            deathCause = "hazard";
            m_GameController.TouchedHazard(this);
        }
    }

    void OnTriggerEnter(Collider col)
    {
        //if we find a key and it's parent is the main platform we can pick it up
        if (col.transform.CompareTag("key") && col.transform.parent == transform.parent && gameObject.activeInHierarchy)
        {
            print("Picked up key");
            MyKey.SetActive(true);
            IHaveAKey = true;
            stepsToKeyPickup = StepCount;
            col.gameObject.SetActive(false);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;
        if (Input.GetKey(KeyCode.D))
        {
            discreteActionsOut[0] = 3;
        }
        else if (Input.GetKey(KeyCode.W))
        {
            discreteActionsOut[0] = 1;
        }
        else if (Input.GetKey(KeyCode.A))
        {
            discreteActionsOut[0] = 4;
        }
        else if (Input.GetKey(KeyCode.S))
        {
            discreteActionsOut[0] = 2;
        }
    }


    public void ApproximateStats(float distanceToDragon, float distanceToKey, float distanceToDoor){
        if (distanceToDragon < minDistanceToDragon){
            minDistanceToDragon = distanceToDragon;
        }
        if (distanceToDragon <= DragonProximityThreshold){
            stepsNearDragon++;
        }
        if (distanceToKey >= 0f && distanceToKey < minDistanceToKey){
            minDistanceToKey = distanceToKey;
        }
        if (distanceToDoor >= 0f && distanceToDoor < minDistanceToDoor){
            minDistanceToDoor = distanceToDoor;
        }
    }

    public void SetSpawnDistances(float toDoor, float toDragon){
        startDistanceToDoor = toDoor;
        startDistanceToDragon = toDragon;
    }

    public AgentEpisodeStats GetEpisodeStats(){
        return new AgentEpisodeStats{
            AgentName = gameObject.name,
            StepsToKeyPickup = stepsToKeyPickup,
            GotKey = IHaveAKey || stepsToKeyPickup >= 0,
            UnlockedDoor = unlockedDoor,
            DeathCause = deathCause,
            MinDistanceToDragon = minDistanceToDragon,
            StepsNearDragon = stepsNearDragon,
            MinDistanceToKey = minDistanceToKey,
            MinDistanceToDoor = minDistanceToDoor,
            PolicyLabel = PolicyLabel,
            StartDistanceToDoor = startDistanceToDoor,
            StartDistanceToDragon = startDistanceToDragon
        };
    }

}
