using UnityEngine;
using UnityEngine.AI;

public class Padestarian : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator anim;
    [SerializeField] GameObject[] characterPrefabs;
    [SerializeField] private Transform[] positions;

    private int prevState;
    bool isWalking;
    private float waitingTime;
    private float nextTime;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        characterPrefabs[Random.Range(0, characterPrefabs.Length)].SetActive(true);
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        prevState = Random.Range(0, positions.Length);
        agent.SetDestination(positions[prevState].transform.position);
    }

    // Update is called once per frame
    void Update()
    {
        if (agent.remainingDistance > agent.stoppingDistance)
        {
            anim.SetBool("isWalking", true);
            isWalking = true;
            return;
        }

        if (!isWalking)
            return;

        if (Time.time < nextTime)
        {
            return;
        }
        waitingTime = Random.Range(0, 1);
        nextTime = Time.time + waitingTime;
        
        isWalking = false;
        anim.SetBool("isWalking", false);
        
        int randomIndex = Random.Range(0, positions.Length);
        
        if (randomIndex == prevState)
        {
            if (randomIndex == 0)
            {
                randomIndex++;
            }else if (randomIndex == positions.Length - 1)
            {
                randomIndex--;
            }
            else
            {
                randomIndex++;
            }
        }
        prevState = randomIndex;
        agent.SetDestination(positions[randomIndex].transform.position);
    }
}
