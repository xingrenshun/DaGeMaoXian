using DG.Tweening;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using RDG;

public class Roll : MonoBehaviour
{
    [HideInInspector] public int colorId;
    private RollsManager rollsManager;
    public List<Transform> ropeRings;
    public float ropeRingsDelay, rollRotationDuration;
    [HideInInspector] public int consumedKnotHoldersCount, usingKnotHoldelsCount;
    public MeshRenderer rollModel, notMovableModel;
    public GameObject hideModelObj, frozenObj, keyObj, lockObj;
    [HideInInspector] public RollContainer rollContainer;
    [HideInInspector] public bool isMoved, isMovable; // Can this roll be moved
    [HideInInspector] public Vector2 targetRollDirection;
    [HideInInspector] public bool isConnectedRoll; // Is this roll connected to another roll
    [HideInInspector] public bool isFrozen;
    private string originalName;
    private int freezeCount = 3;
    private List<Transform> frozenObjects = new();
    public GameObject iceBreakParPrefab, unlockParticlePrefab;
    private ParticleSystem iceBreakParticle;
    private GameManager gameManager;
    public GameObject ropeCountUiPanel;
    [SerializeField] private TMP_Text ropeCountText;

    void Start()
    {
        rollsManager = FindFirstObjectByType<RollsManager>();
        gameManager = FindFirstObjectByType<GameManager>();

        // Destroy not usable objects
        if (!lockObj.activeSelf)
            Destroy(lockObj);
        if(!keyObj.activeSelf)
            Destroy(keyObj);

        if (isFrozen)
            frozenObjects.AddRange(frozenObj.transform.GetComponentsInChildren<Transform>());
    }

    private void OnMouseDown()
    {
        // Check if this roll can move
        if (!isMovable || isFrozen || lockObj || !gameManager.gameStarted) return;

        if (isConnectedRoll)
        {
            if (!HaveSpaceForRollsIfConnectRolls())
                return;
        }

        foreach (RollContainer _rollContainer in rollsManager.rollContainers)
        {
            if (_rollContainer.roll == null)
            {
                Vibration.Vibrate(50);

                AudioManager.Instance.Play("RollMove");
                rollContainer = _rollContainer;
                _rollContainer.roll = this;
                originalName = gameObject.name;
                gameObject.name = "MovedRoll";
                MoveRoll();
                Destroy(GetComponent<BoxCollider>());
                isMoved = true;
                break;
            }
        }
    }

    private bool HaveSpaceForRollsIfConnectRolls()
    {
        int count = 0;
        foreach (RollContainer _rollContainer in rollsManager.rollContainers)
        {
            if (_rollContainer.roll == null)
            {
                count++;

                if (count == 2) // We need 2 empty spaces for connected rolls
                    return true;
            }
        }

        return false;
    }

    private void MoveRoll()
    {
        isMoved = true;

        if (keyObj)
            keyObj.GetComponent<Key>().FindLockedRolls();

        CheckFrozenRolls();

        Transform rollMergeRing = transform.Find("RollMergeRing(Clone)");

        if (rollMergeRing)
            Destroy(rollMergeRing.gameObject);

        transform.DOPunchScale(new Vector3(.4f, .4f, .4f), 0.4f, 7, 1);
        transform.DOJump(rollContainer.transform.position, 4, 1, 0.4f).OnComplete(() =>
        {
            AudioManager.Instance.Play("RollMoved");
            ropeCountUiPanel.SetActive(true);
            ropeCountUiPanel.transform.parent = null;
            ropeCountUiPanel.transform.eulerAngles = Vector3.zero;

            foreach (Pipe rollHolder in FindObjectsByType<Pipe>(FindObjectsSortMode.None))
                rollHolder.CheckRollMoved();

            FindFirstObjectByType<RollMovableManager>().CheckAllRollsMovable();

            if (isConnectedRoll)
                MoveConnectedRoll();

            FindKnotsHolder();
            rollsManager.CheckRetry();
        });
    }

    private void CheckFrozenRolls()
    {
        foreach (Roll roll in FindObjectsByType<Roll>(FindObjectsSortMode.None))
        {
            if (roll.isFrozen && roll.isMovable)
                roll.Unfreeze();
        }
    }

    public void Unfreeze()
    {
        AudioManager.Instance.Play("IceBreak");

        if (iceBreakParticle == null)
            iceBreakParticle = Instantiate(iceBreakParPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity, transform).GetComponent<ParticleSystem>();

        iceBreakParticle.Play();

        freezeCount--;
        Destroy(frozenObjects[freezeCount].gameObject);
        if (freezeCount == 0)
            isFrozen = false;
    }

    public void Unlock()
    {
        AudioManager.Instance.Play("Unlock");
        Instantiate(unlockParticlePrefab, transform.position + Vector3.up * 1.5f, Quaternion.identity, transform);
        Destroy(lockObj);
    }

    private void MoveConnectedRoll()
    {
        // Ensure mergedRollDirection is one of the 4 cardinal directions

        Roll connectedRoll = FindConnectedRoll(originalName);

        Transform rollMergeRing = connectedRoll.transform.Find("RollMergeRing(Clone)");
        if (rollMergeRing)
            Destroy(rollMergeRing.gameObject);

        connectedRoll.isConnectedRoll = false; // Stop Connected Roll to Check For Move.
        connectedRoll.OnMouseDown();
    }

    private Roll FindConnectedRoll(string currentRollName)
    {
        string[] parts = currentRollName.Split('_');
        int x = int.Parse(parts[1]);
        int y = int.Parse(parts[2]);

        // Apply direction offset
        x += (int)targetRollDirection.x;
        y += (int)targetRollDirection.y;

        print(x + y);

        // Find the connected roll
        GameObject rollObj = GameObject.Find($"Roll_{x}_{y}");
        print(rollObj);
        Roll connectedRoll = rollObj.GetComponent<Roll>();
        return connectedRoll;
    }

    public void FindKnotsHolder()
    {
        if (consumedKnotHoldersCount == 3) return;

        Debug.Log("FindKnotsHolder " + gameObject.name);

        LevelSpawner levelSpawner = FindFirstObjectByType<LevelSpawner>();
        foreach (Transform columnTransform in levelSpawner.columnTransforms)
        {
            Debug.Log("knotsHolder " + columnTransform.gameObject.name);

            if (columnTransform.childCount == 0) continue;

            KnotsHolder knotsHolder = columnTransform.GetChild(0).GetComponent<KnotsHolder>();

            if (consumedKnotHoldersCount < 3 && !knotsHolder.isHolderBusy && knotsHolder.colorId == colorId)
            {
                knotsHolder.StartRopeAnimation(this);
                InvokeRepeating(nameof(EnableRingsInvoke), 0, ropeRingsDelay);
                RotateRoll();
            }
        }
    }

    private int ropeCount;

    public void UpdateRopeUI()
    {
        ropeCount++;
        float percent = (float)ropeCount / 72 * 100f;

        // If less than 5%, still show 0%
        if (percent < 5f && ropeCount < 72)
            percent = 0f;

        // Clamp to 100%
        percent = Mathf.Clamp(percent, 0f, 100f);

        ropeCountText.text = percent.ToString("F0") + "%";
    }


    public void StopRotating()
    {
        if (usingKnotHoldelsCount != 0) return;

        transform.DOKill();
        CancelInvoke(nameof(EnableRingsInvoke));
    }

    private void RotateRoll()
    {
        // Rotate 360 degrees on Y-axis and loop forever
        transform.DORotate(
            new Vector3(0, 360, 0),   // Target rotation
            rollRotationDuration,         // Duration
            RotateMode.FastBeyond360  // Keeps rotating beyond 360
        )
        .SetEase(Ease.Linear)          // Constant speed
        .SetLoops(-1, LoopType.Restart) // Infinite loop
        // ⚠ 无限循环补间，永远不会自己结束：如果关卡在它还在转的时候被销毁，
        //   它下一帧就会去写已销毁 Transform 的 rotation → WASM abort（整局卡死）。
        //   SetLink 让这个 Roll 被销毁时 DOTween 自动把它回收。
        .SetLink(gameObject);
    }

    void EnableRingsInvoke()
    {
        foreach (Transform ring in ropeRings)
        {
            // ✅ Check if this ring is already scaled up
            if (ring.localScale.x == 0)
            {
                ring.GetComponent<MeshRenderer>().material.color = rollModel.material.color;
                float val = 3.34f;
                ring.DOScale(new Vector3(val, 4.24f, val), ropeRingsDelay); // Enable object
                break;
            }
        }
    }
    
    public void CanMove()
    {
        isMovable = true;
        rollModel.gameObject.SetActive(true);
        notMovableModel.gameObject.SetActive(false);
        hideModelObj.SetActive(false);

        if(keyObj)
            keyObj.transform.position = new Vector3(keyObj.transform.position.x, .7f, keyObj.transform.position.z);
        else if (lockObj)
            lockObj.transform.localPosition = new Vector3(0, 1.83f, 0);
    }
}