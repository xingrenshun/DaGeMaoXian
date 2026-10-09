using RDG;
using System;
using System.Collections.Generic;
using UnityEngine;

public class RollsManager : MonoBehaviour
{
    [HideInInspector] public List<RollContainer> rollContainers;
    private GameManager gameManager;
    private LevelSpawner levelSpawner;
    public GameObject rollDestroyParticle;

    private void Start()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        levelSpawner = FindFirstObjectByType<LevelSpawner>();
    }
    public void CheckRolls()
    {
        Invoke(nameof(CheckRollsInvoke), .1f);
    }

    public void CheckRollsInvoke()
    {
        foreach (RollContainer rollContainer in rollContainers)
        {
            if (rollContainer.roll)
            {
                rollContainer.roll.FindKnotsHolder();
            }
        }

        CheckRetry();
        Invoke(nameof(CheckWinInvoke), .2f);
    }

    private void CheckWinInvoke()
    {
        if (!FindFirstObjectByType<Roll>())
            gameManager.Win();
    }

    public void CheckRetry()
    {
        Invoke(nameof(CheckRetryInvoke), .2f);
    }

    private void CheckRetryInvoke()
    {
        Debug.Log("CheckRetry");
        foreach (RollContainer rollContainer in rollContainers)
        {
            if (rollContainer.roll == null)
            {
                Debug.Log("CheckRetry RollContainer " + rollContainer.name);

                return;
            }
        }

        foreach (Transform columnTransform in levelSpawner.columnTransforms)
        {
            if (columnTransform.childCount > 0 && columnTransform.GetChild(0).GetComponent<KnotsHolder>().isHolderBusy)
                return;
        }

        foreach (Transform columnTransform in levelSpawner.columnTransforms)
        {
            if (columnTransform.childCount > 0)
            { 
                foreach (RollContainer rollContainer in rollContainers)
                {
                    if (columnTransform.GetChild(0).GetComponent<KnotsHolder>().colorId == rollContainer.roll.colorId)
                        return;
                }
            }
        }

        gameManager.Retry();
    }

    public void RemoveRoll(Roll usedRoll)
    {
        foreach (RollContainer rollContainer in rollContainers)
        {
            if (rollContainer.roll && rollContainer.roll == usedRoll)
            {
                AudioManager.Instance.Play("RollDestroy");

                Debug.Log("rollContainer " + rollContainer.name);
                rollContainer.roll = null;

                Destroy(usedRoll.GetComponent<Roll>().ropeCountUiPanel);
                Destroy(usedRoll.gameObject);
                Vibration.Vibrate(70);

                Instantiate(rollDestroyParticle, rollContainer.transform.position, Quaternion.identity);
                break;
            }
        }
    }
}