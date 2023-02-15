using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleComputeShader_Array : MonoBehaviour
{
    public ComputeShader computeShader;
    int kernelIndex_KernelFunction_A;
    int kernelIndex_KernelFunction_B;
    ComputeBuffer intComputeBuffer;

    // Start is called before the first frame update
    void Start()
    {
        this.kernelIndex_KernelFunction_A
            = this.computeShader.FindKernel("KernelFunction_A");
        this.kernelIndex_KernelFunction_B
            = this.computeShader.FindKernel("KernelFunction_B");

        this.intComputeBuffer = new ComputeBuffer(4, sizeof(int));
        this.computeShader.SetBuffer(this.kernelIndex_KernelFunction_A, "intBuffer", this.intComputeBuffer);
        this.computeShader.SetInt("intValue", 1);

        this.computeShader.Dispatch(this.kernelIndex_KernelFunction_A, 1, 1, 1);
        int[] result = new int[4];
        this.intComputeBuffer.GetData(result);

        for (int i = 0; i < 4; i++)
        {
            Debug.Log(result[i]);
        }

        this.computeShader.SetBuffer(this.kernelIndex_KernelFunction_B, "intBuffer", this.intComputeBuffer);
        this.computeShader.Dispatch(this.kernelIndex_KernelFunction_B, 1, 1, 1);
        this.intComputeBuffer.GetData(result);

        for (int i = 0; i < 4; i++)
        {
            Debug.Log(result[i]);
        }

        this.intComputeBuffer.Release();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
