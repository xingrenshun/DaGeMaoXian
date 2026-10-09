using DG.Tweening;
using UnityEngine;

public class Column : MonoBehaviour
{
    private Vector3 basePos;
    private int steps = 0;
    private float moveDownZVal = .36f;

    private void Start()
    {
        basePos = transform.position;
    }

    public void MoveDown()
    {


        steps++;
        float targetZPos = basePos.z - (moveDownZVal * steps);

        transform.DOMoveZ(targetZPos, 0.4f).SetEase(Ease.Linear).OnComplete(() =>
        {
            transform.position = new Vector3(transform.position.x, transform.position.y, targetZPos);
           
        });
    }
}