using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestList : MonoBehaviour
{
    void Test0()
    {
        var list = new List<int>();
        list.Add(Random.Range(0, 10));
        list.Add(Random.Range(0, 10));
        list.Add(Random.Range(0, 10));
        for (int i = 0; i < list.Count; i++)
        {
            Debug.Log(list[i]);
        }
    }
    private void Test1()
    {
        var array = new int[3];
        array[0] = Random.Range(0, 10);
        array[1] = Random.Range(0, 10);
        array[2] = Random.Range(0, 10);
        for (int i = 0; i < 3; i++)
        {
            Debug.Log(array[i]);
        }
    }
    // Update is called once per frame
    void Update()
    {
        Test0();
        Test1();

    }

    
}
