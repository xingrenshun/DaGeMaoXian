using DG.Tweening;
using UnityEngine;

public class Key : MonoBehaviour
{
    public void FindLockedRolls()
    {
        AudioManager.Instance.Play("Key");
        transform.parent = null;

        foreach (Roll roll in FindObjectsByType<Roll>(FindObjectsSortMode.None))
        {
            if (roll.lockObj)
            {             
                transform.DOJump(roll.transform.position + Vector3.up * .5f, 2, 1, .5f).OnComplete(() =>
                {
                    roll.Unlock();
                    Destroy(gameObject);
                });
                break;
            }
        }
    }
}