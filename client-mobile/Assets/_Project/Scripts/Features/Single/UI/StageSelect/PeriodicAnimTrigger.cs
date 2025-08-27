using UnityEngine;
using System.Collections;

public class PeriodicAnimTrigger : MonoBehaviour
{
    public Animator animator;             // 붙어있는 Animator
    public string triggerName = "Spin";   // 위에서 만든 Trigger 이름
    public float period = 5f;             // 시작-시작 간격(초)
    public bool unscaledTime = false;     // 일시정지 중에도 돌리고 싶다면 true

    Coroutine loop;

    void Reset()  { animator = GetComponent<Animator>(); }
    void OnEnable(){ loop = StartCoroutine(Loop()); }
    void OnDisable(){ if (loop != null) StopCoroutine(loop); }

    IEnumerator Loop()
    {
        while (true)
        {
            animator.SetTrigger(triggerName);

            if (unscaledTime)
                yield return new WaitForSecondsRealtime(period);
            else
                yield return new WaitForSeconds(period);
        }
    }
}
