using System.Collections;
using UnityEngine;

public class AnimatorStartDelay : MonoBehaviour
{
    public float delaySeconds = 1f;

    Animator _animator;

    void OnEnable()
    {
        _animator = GetComponent<Animator>();
        _animator.enabled = false;
        StartCoroutine(DelayedStart());
    }

    IEnumerator DelayedStart()
    {
        yield return new WaitForSeconds(delaySeconds);
        _animator.enabled = true;
    }
}
