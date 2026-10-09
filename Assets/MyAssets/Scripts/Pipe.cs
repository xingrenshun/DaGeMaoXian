using System;
using System.Collections.Generic;
using UnityEngine;

public class Pipe : MonoBehaviour
{
    public List<int> rollsToSpawnColorIds;
    public int x, y;
    private LevelSpawner levelSpawner;
    public Transform spawnPos;

    private void Start()
    {
        levelSpawner = FindFirstObjectByType<LevelSpawner>();
    }

    public void CheckRollMoved()
    {
        string targetRoll = null;
        
        if(transform.eulerAngles.y == 0)
            targetRoll = "Roll_" + x + "_" + (y + 1); // Check Front Roll
        else if (transform.eulerAngles.y == 180)
            targetRoll = "Roll_" + x + "_" + (y - 1); // Check Back Roll
        else if(transform.eulerAngles.y == 90)
            targetRoll = "Roll_" + (x + 1) + "_" + y; // Check Right Roll
        else if(transform.eulerAngles.y == 270)
            targetRoll = "Roll_" + (x - 1) + "_" + y; // Check Left Roll 

        //Debug.Log("Rot " + transform.eulerAngles + " " + targetRoll);

        if (GameObject.Find(targetRoll) == null)
        {
            SpawnRoll(targetRoll);
        }
    }

    private void SpawnRoll(string holeName)
    {
        AudioManager.Instance.Play("RollSpawn");

        GameObject rollObj = Instantiate(levelSpawner.rollPrefab, spawnPos.position, Quaternion.identity);
        rollObj.name = holeName;

        Roll roll = rollObj.GetComponent<Roll>();
        roll.CanMove();
        roll.colorId = rollsToSpawnColorIds[0];

        Color color = levelSpawner.GetColor(rollsToSpawnColorIds[0]);
        roll.rollModel.material.color = color;
        roll.notMovableModel.material.color = color;

        rollsToSpawnColorIds.RemoveAt(0);

        if(rollsToSpawnColorIds.Count == 0)
            Destroy(this);
    }
}