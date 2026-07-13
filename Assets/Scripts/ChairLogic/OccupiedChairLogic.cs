using UnityEngine;

public class OccupiedChairLogic : MonoBehaviour
{
    public GameObject[] characterPrefabs;
    private int randomIndex;
    
    public void OccupyChair()
    {
        randomIndex = Random.Range(0, characterPrefabs.Length);
        characterPrefabs[randomIndex].SetActive(true);
        Debug.Log(randomIndex);
    }

    public void UnOccupyChair()
    {
        characterPrefabs[randomIndex].SetActive(false);
    }
}
