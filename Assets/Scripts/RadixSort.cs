using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RadixSort : MonoBehaviour
{
    public List<int> nums;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        int[] arr= nums.ToArray();
        Radix(ref arr);
        foreach(int i in arr)
            Debug.Log(i);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
    public int[] CountSortByDigit(int[] arr, int exp)
    {
        int len = arr.Length;
        if(len ==0)
            return null;
        int[] counts = {0,0};
        int[] output = new int[len]; 
        for(int i =0; i < len; i++)
        {
            int curDig = (arr[i] / exp) %2;
            counts[curDig] += 1;
        }
        int prefix_sum = 0;
        for(int i =0; i<2; i++)
        {
            prefix_sum+=counts[i];
            counts[i] = prefix_sum;
        }
        for(int i = len - 1; i > -1; i--)
        {
            int d = (arr[i] / exp) %2;

            int a = arr[i];
            int b = counts[d]-1;
            output[b] = a;
            counts[d]-=1;



        }
        return output;

    }


    public void Radix(ref int[] arr)
    {
        int max = nums.Max();    
        int exp =1;
        while(max/exp > 0)
        {
            arr = CountSortByDigit(arr,exp);
            exp*=2;
        }
    }
}
