using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// "Manages" stuff related specifically to the Mission gameplay.
/// </summary>
public class MissionManager : MonoBehaviour
{
    /// <summary>
    /// Raised when mission end. Bool param false if player lost, true if player won.
    /// </summary>
    public static UnityEvent<bool> OnMissionEnd = new UnityEvent<bool>();
    /// <summary>
    /// Raised when attack happens in between turns
    /// </summary>
    public static UnityEvent OnAttackStart = new UnityEvent();
    public static UnityEvent OnLeaveBuildMode = new UnityEvent();
    /// <summary>
    /// Raised when attack ends
    /// </summary>
    public static UnityEvent OnAttackEnd = new UnityEvent();
    /// <summary>
    /// Called when a new turn begins
    /// </summary>
    public static UnityEvent OnNewTurn = new UnityEvent();

    public static MissionData CurrentMission => currentMission;
    private static MissionData currentMission;
    public static int EnemiesAlive => enemiesAlive;
    private static int enemiesAlive;
    public static int FriendliesAlive => friendliesAlive;
    private static int friendliesAlive;

    private static MissionManager _instance;
    public static MissionManager Instance { get { return _instance; } }
    public static bool isSlowMotion;
    // should be in SO probably
    public static float slowMotionSpeed = .5f;

    [SerializeField] private GameObject gateGO;
    public Player Player => player;
    [SerializeField] private Player player;

    // list of friendlies that are disabled until they are activated and initialized with CompanySoldier info. Basically for spawning allies in.
    [SerializeField] private List<FriendlyActorController> friendlyActorBodies = new List<FriendlyActorController>();
    // already initialized friendlies that are in the mission.
    [HideInInspector]
    public List<FriendlyActorController> friendlyActors = new List<FriendlyActorController>();

    private AudioSource genericSoundPlayer;
    [SerializeField] private AudioClip sirenSound;

    private bool hasBeenAnAttackThisDeployment;
    [HideInInspector]
    public int TurnNumber => turnNumber;
    private int turnNumber = 1;

    [Header("Testing Stuff")]
    [SerializeField] private bool testMode = false;
    [SerializeField] private MissionData testMission;
    [SerializeField] private ControllerData basicEnemy;
    [SerializeField] private ControllerData sapperEnemy;
    [SerializeField] private ControllerData warriorEnemy;


    public Dictionary<Vector3, bool> followOffsets = new Dictionary<Vector3, bool>();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.Log("More than one Game Manager, deleting one.");
            Destroy(gameObject);
            return;
        }
        else
        {
            _instance = this;
        }

        PlayerInput.InitializeControls();

        EnemyActorController.OnEnemySpawned.AddListener(HandleEnemySpawned);
        AIActorController.OnActorKilled.AddListener(HandleActorKilled);

        // initially, all bodies are not active. in case I leave some enabled in the mission scene.
        foreach (FriendlyActorController body in friendlyActorBodies)
        {
            body.transform.parent.gameObject.SetActive(false);
        }

        // while developing new game loop, just use the test mission. Will rework later.
        currentMission = testMission;

        if (!testMode)
        {
            DeployFriendlies();
            StartCoroutine(InitializeMissionWhenGameManagerReady());
        }

        GenerateFollowOffsets();
    }

    private void Start()
    {
        // set up victory condition
        switch (currentMission.victoryCondition)
        {
            case MissionData.VictoryCondition.EliminateAllEnemies:
                StartCoroutine(CheckForAllEnemiesEliminated());
                break;
        }

        // if in test mode, will begin attack from button
        if (!testMode)
        {
            BeginAttack();
        }
    }


    public void BeginAttack()
    {
        // Incoming!
        OnAttackStart.Invoke();
        //OnLeaveBuildMode.Invoke();

        PlaySound(sirenSound, .5f);

    }

    private void GenerateFollowOffsets()
    {
        followOffsets[new Vector3(0, 0, 0)] = false;
        followOffsets[new Vector3(.5f, -.5f, 0)] = false;
        followOffsets[new Vector3(-.5f, -.5f, 0)] = false;
        followOffsets[new Vector3(0f, -1f, 0)] = false;

        // haven't set these ones up
        //followOffsets[new Vector3(2, 0, 0)] = false;
        //followOffsets[new Vector3(2, 1, 0)] = false;
        //followOffsets[new Vector3(0, 2, 0)] = false;
        //followOffsets[new Vector3(1, 2, 0)] = false;
        //followOffsets[new Vector3(2, 2, 0)] = false;
    }

    // this case is just for if we're testing the Mission scene, shouldn't be in this case if playing the game normally (through Bootstrap scene)
    //else
    //{
    //    GameObject[] friendlyBodies = GameObject.FindGameObjectsWithTag("FriendlyActorBody");
    //    for (int i = 0; i <  friendlyBodies.Length; i++)
    //    {
    //        friendlyActors.Add(friendlyBodies[i].GetComponent<FriendlyActorController>());
    //    }
    //}


    //}

    private void OnDestroy()
    {
        //player.RemoveDeathListener(HandlePlayerDeath);
        EnemyActorController.OnEnemySpawned.RemoveListener(HandleEnemySpawned);
        AIActorController.OnActorKilled.RemoveListener(HandleActorKilled);
    }

    private void DeployFriendlies()
    {
        if (GameManager.Instance.Company != null)
        {
            List<CompanySoldier> soldiers = GameManager.Instance.Company.GetDeployedSoldiersAsList();
            for (int i = 0; i < soldiers.Count; i++)
            {
                CompanySoldier soldier = soldiers[i];
                FriendlyActorController actorController = friendlyActorBodies[i];
                actorController.SetSoldier(soldier);
                friendlyActors.Add(actorController);
                
            }

            FriendlyActorController controlledActor = friendlyActors[0];
            player.ControlledActor = controlledActor;
            controlledActor.GetActor().IsPlayer = true;
            controlledActor.SetActorControlled(true);
            //controlledActor.OnActorDeath.AddListener(HandleControlledActorDeath);
            //HandleCommandModeExit(cntxt);
        }

    }

    /// <summary>
    /// Set on button in the TestMission in inspector!
    /// </summary>
    public void SpawnFriendlyPistolSoldier()
    {
        CompanySoldier soldier = GameManager.Instance.Company.GetNewRecruits(1)[0];
        soldier.CurrentWeapon = GameManager.Instance.GetDataStore().pistol;
        GameManager.Instance.Company.AddRecruit(soldier);

        for (int i = 0; i < friendlyActorBodies.Count; i++)
        {
            if (!friendlyActorBodies[i].isActiveAndEnabled)
            {
                FriendlyActorController actorController = friendlyActorBodies[i];
                actorController.SetSoldier(soldier);
                friendlyActors.Add(actorController);
                

                if (friendlyActors.Count == 1)
                {

                    FriendlyActorController controlledActor = friendlyActors[0];
                    player.ControlledActor = controlledActor;
                    controlledActor.GetActor().IsPlayer = true;
                    controlledActor.SetActorControlled(true);
                }


                return;
            }
        }

    }

    /// <summary>
    /// Set on button in the TestMission in inspector!
    /// </summary>
    public void SpawnFriendlyShotgunSoldier()
    {
        CompanySoldier soldier = GameManager.Instance.Company.GetNewRecruits(1)[0];
        soldier.CurrentWeapon = GameManager.Instance.GetDataStore().shotgun;
        GameManager.Instance.Company.AddRecruit(soldier);

        for (int i = 0; i < friendlyActorBodies.Count; i++)
        {
            if (!friendlyActorBodies[i].isActiveAndEnabled)
            {
                FriendlyActorController actorController = friendlyActorBodies[i];
                actorController.SetSoldier(soldier);
                friendlyActors.Add(actorController);

                

                if (friendlyActors.Count == 1)
                {        
                    FriendlyActorController controlledActor = friendlyActors[0];
                    player.ControlledActor = controlledActor;
                    controlledActor.GetActor().IsPlayer = true;
                    controlledActor.SetActorControlled(true);
                }

                return;
            }
        }
    }

    /// <summary>
    /// Set on button in the TestMission in inspector!
    /// </summary>
    public void SpawnFriendlyAssaultRifleSoldier()
    {
        CompanySoldier soldier = GameManager.Instance.Company.GetNewRecruits(1)[0];
        soldier.CurrentWeapon = GameManager.Instance.GetDataStore().assaultRifle;
        GameManager.Instance.Company.AddRecruit(soldier);

        for (int i = 0; i < friendlyActorBodies.Count; i++)
        {
            if (!friendlyActorBodies[i].isActiveAndEnabled)
            {
                FriendlyActorController actorController = friendlyActorBodies[i];
                actorController.SetSoldier(soldier);
                friendlyActors.Add(actorController);

                if (friendlyActors.Count == 1)
                {             
                    FriendlyActorController controlledActor = friendlyActors[0];
                    player.ControlledActor = controlledActor;
                    controlledActor.GetActor().IsPlayer = true;
                    controlledActor.SetActorControlled(true);
                }

                return;
            }
        }
    }

    /// <summary>
    /// Set on button in the TestMission in inspector!
    /// </summary>
    public void ToggleBuildMode()
    {
        if (BuildingManager.Instance.InBuildMode)
        {
            OnLeaveBuildMode.Invoke();
        }
        else
        {
            OnAttackEnd.Invoke();
            OnNewTurn.Invoke();
        }
    }

    private IEnumerator InitializeMissionWhenGameManagerReady()
    {
        yield return new WaitUntil(GameManager.Instance.IsInitialized);
        //currentMission = GameManager.Instance.CurrentMission;
        OnNewTurn.Invoke();
    }

    /// <summary>
    /// Set on the EndTurn Button in the inspector. Trigger an attack or start the next turn.
    /// </summary>
    //public void EndTurn()
    //{
        //float attackChanceRoll = Random.Range(0f, 1f);
        //// if rolled high enough to get an attack, or if there hasn't been an attack and it's the end, trigger attack
        //if (currentMission.perTurnAttackChance > attackChanceRoll || (turnNumber == currentMission.numberOfTurns && !hasBeenAnAttackThisDeployment))
        //{
        //    // Incoming!
        //    OnAttackStart.Invoke();
        //    OnLeaveBuildMode.Invoke();

        //    PlaySound(sirenSound, .5f);

        //    // set up victory condition
        //    switch (currentMission.victoryCondition)
        //    {
        //        case MissionData.VictoryCondition.EliminateAllEnemies:
        //            StartCoroutine(CheckForAllEnemiesEliminated());
        //            break;
        //    }

        //    hasBeenAnAttackThisDeployment = true;
        //}
        //else
        //{
        //    NextTurn();
        //}
    //}

    private IEnumerator CheckForAllEnemiesEliminated()
    {
        while (EnemySpawner.totalEnemiesSpawned < currentMission.enemyCount || enemiesAlive > 0)
        {
            yield return new WaitForSeconds(1f);

        }

        OnAttackEnd.Invoke();
        NextTurn();
    }

    private void NextTurn()
    {
        turnNumber++;

        if (turnNumber > currentMission.numberOfTurns)
        {
            // passing true for now - not necessarily that the player won though.
            OnMissionEnd.Invoke(true);
        }

        OnNewTurn.Invoke();
    }

    /// <summary>
    /// Creates a new object with an audio source component to play the sound. One use of this is classes that aren't monobehaviours needing to play audio. 
    /// </summary>
    /// <param name="clip"></param>
    public void PlaySound(AudioClip clip, float volume = 1f)
    {
        if (genericSoundPlayer == null)
        {
            GameObject soundPlayerGO = Instantiate(new GameObject());
            soundPlayerGO.name = "GM Sound Player";
            genericSoundPlayer = soundPlayerGO.AddComponent<AudioSource>();
            genericSoundPlayer.volume = volume;
        }
        
        genericSoundPlayer.clip = clip;
        genericSoundPlayer.Play();
        
    }



    public void StartSlowMotion(float secondsToWait)
    {
        StartCoroutine(StartSlowMotionRoutine(secondsToWait));
    }

    private IEnumerator StartSlowMotionRoutine(float secondsToWait)
    {
        yield return new WaitForSeconds(secondsToWait);

        isSlowMotion = true;
        Time.timeScale = slowMotionSpeed;

        yield return new WaitForSeconds(2.75f);

        isSlowMotion = false;
        Time.timeScale = 1f;
    }

    private void HandleEnemySpawned()
    {
        enemiesAlive++;
    }

    public void HandleFriendlySpawned()
    {
        friendliesAlive++;
    }

    private void HandleActorKilled(Actor.ActorTeam team)
    {
        if (team == Actor.ActorTeam.Enemy)
        {
            enemiesAlive--;
        }
        else if (team == Actor.ActorTeam.Friendly)
        {
            friendliesAlive--;

            if (friendliesAlive <= 0)
            {
                EndMission(false);
            }
        }
        else
        {
            Debug.Log("Friendly Actor killed");
        }
    }

    private void HandlePlayerDeath()
    {
        EndMission(false);
    }

    private void EndMission(bool playerWon)
    {
        OnMissionEnd.Invoke(playerWon);
    }

    public GameObject GetGateGO()
    {
        return gateGO;
    }
}
