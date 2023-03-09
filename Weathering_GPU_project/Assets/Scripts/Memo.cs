using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Memo : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        int[] testArr = { 0, 1, 2, 3, 4 };
        int count = 0;
        testArr[count++] = -1;
        
        for (int i = 0; i < 5; i++) { 
            Debug.Log(testArr[i]);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
