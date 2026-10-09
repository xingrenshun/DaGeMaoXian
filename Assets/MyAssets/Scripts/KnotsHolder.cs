using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;

public class KnotsHolder : MonoBehaviour
{
    public Transform ropeStart, ropeEnd;
    public Transform knotsParent;
    private List<Transform> knots = new();
    private int knotsIndex = 1;
    public float ropeMovingDur, ropeToRollDur;
    [HideInInspector] public int colorId;
    Vector3 rollPos;
    private Roll usingRoll;
    [HideInInspector] public bool isHolderBusy;
    [HideInInspector] public GameObject hidePanel;
    private int knotRowCount;

    public void StartRopeAnimation(Roll roll)
    {
        print("StartRopeAnimation");
        isHolderBusy = true;
        usingRoll = roll;
        usingRoll.usingKnotHoldelsCount++;
        usingRoll.consumedKnotHoldersCount++;
        usingRoll.UpdateRopeUI();

        GetComponent<Rope>().drawRope = true;
        Material mat = GetComponent<LineRenderer>().material;
        mat.color = usingRoll.rollModel.material.color;
        mat.SetColor("_EmissionColor", mat.color);

        ropeEnd.parent = usingRoll.transform;

        for (int i = 0; i < knotsParent.childCount; i++)
            knots.Add(knotsParent.GetChild(i));

        rollPos = usingRoll.rollContainer.transform.position;
        rollPos.y += .5f;

        ropeEnd.DOMove(rollPos, ropeToRollDur).OnComplete(() =>
        {
            MoveStartRope();
        });
    }
    private void MoveStartRope()
    {
        // Check index is in range
        if (knotsIndex >= knots.Count)
        {
            knots[^1].gameObject.SetActive(false); // Diseble Last Knot Also

            Debug.Log("Reached end of knots list " + gameObject.name);

            // Try to spawn next knot holder in this column before completing

            FindFirstObjectByType<LevelSpawner>().SpawnNextKnotsHolder(transform.parent);

            ropeStart.DOMove(rollPos, ropeToRollDur).OnComplete(() =>
            {
                if (transform.parent.childCount > 1)
                {
                    KnotsHolder knotsHolder = transform.parent.GetChild(1).GetComponent<KnotsHolder>();
                    if (knotsHolder.hidePanel)
                        Destroy(knotsHolder.hidePanel);
                }

                Debug.Log("Rope Start Moved " + gameObject.name);
                usingRoll.usingKnotHoldelsCount--;

                if (usingRoll.usingKnotHoldelsCount == 0)
                {
                    usingRoll.StopRotating();

                    if (usingRoll.consumedKnotHoldersCount == 3)
                        FindFirstObjectByType<RollsManager>().RemoveRoll(usingRoll);
                }

                FindFirstObjectByType<RollsManager>().CheckRolls();
                Destroy(gameObject);
            });
            return;
        }

        ropeStart.DOMove(knots[knotsIndex].position, ropeMovingDur).OnComplete(() =>
        {
            usingRoll.UpdateRopeUI();
            knots[knotsIndex-1].gameObject.SetActive(false);
            knotsIndex++;

            knotRowCount++;

            if (knotRowCount == 4)
            {
                knotRowCount = 0;
                transform.parent.GetComponent<Column>().MoveDown();
            }
            MoveStartRope();
        });
    }
}