using System.Collections.Generic;
using UnityEngine;

public class ChairLogicHandler : MonoBehaviour
{
    public List<OccupiedChairLogic> chairs = new List<OccupiedChairLogic>();
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        searchForChairs();
    }

    public void searchForChairs()
    {
        OccupiedChairLogic[] chairsToSearch = GetComponentsInChildren<OccupiedChairLogic>(true);
        chairs.AddRange(chairsToSearch);
    }
    
}
