using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Memo : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        uint num = 3;
        for (int i = 0; i < 5; i++) { 
            Debug.Log(--num);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
