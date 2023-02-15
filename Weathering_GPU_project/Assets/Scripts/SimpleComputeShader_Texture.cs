using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleComputeShader_Texture : MonoBehaviour
{
    public GameObject plane_A;
    public GameObject plane_B;

    public ComputeShader computeShader;
    int kernelIndex_KernelFunction_A;
    int kernelIndex_KernelFunction_B;
    RenderTexture renderTexture_A;
    RenderTexture renderTexture_B;

    struct ThreadSize {
        public int x;
        public int y;
        public int z;
        public ThreadSize(uint x, uint y, uint z) {
            this.x = (int)x;
            this.y = (int)y;
            this.z = (int)z;
        }
    }

    ThreadSize kernelThreadSize_KernelFunction_A;
    ThreadSize kernelThreadSize_KernelFunction_B;

    // Start is called before the first frame update
    void Start()
    {
        this.renderTexture_A = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32);
        this.renderTexture_B = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32);
        this.renderTexture_A.enableRandomWrite = true;
        this.renderTexture_B.enableRandomWrite = true;
        this.renderTexture_A.Create();
        this.renderTexture_B.Create();

        this.kernelIndex_KernelFunction_A = this.computeShader.FindKernel("KernelFunction_A");
        this.kernelIndex_KernelFunction_B = this.computeShader.FindKernel("KernelFunction_B");

        uint threadSizeX, threadSizeY, threadSizeZ;
        
        this.computeShader.GetKernelThreadGroupSizes(
            this.kernelIndex_KernelFunction_A,
            out threadSizeX, out threadSizeY, out threadSizeZ);
        this.kernelThreadSize_KernelFunction_A = new ThreadSize(threadSizeX, threadSizeY, threadSizeZ);
        
        this.computeShader.GetKernelThreadGroupSizes(
            this.kernelIndex_KernelFunction_B,
            out threadSizeX, out threadSizeY, out threadSizeZ);
        this.kernelThreadSize_KernelFunction_B = new ThreadSize(threadSizeX, threadSizeY, threadSizeZ);

        this.computeShader.SetTexture(this.kernelIndex_KernelFunction_A, "textureBuffer", this.renderTexture_A);
        this.computeShader.SetTexture(this.kernelIndex_KernelFunction_B, "textureBuffer", this.renderTexture_B);
    }

    // Update is called once per frame
    void Update()
    {
        this.computeShader.Dispatch(
            this.kernelIndex_KernelFunction_A,
            this.renderTexture_A.width / this.kernelThreadSize_KernelFunction_A.x,
            this.renderTexture_A.height / this.kernelThreadSize_KernelFunction_A.y,
            this.kernelThreadSize_KernelFunction_A.z
            );
        this.computeShader.Dispatch(
            this.kernelIndex_KernelFunction_B,
            this.renderTexture_B.width / this.kernelThreadSize_KernelFunction_B.x,
            this.renderTexture_B.height / this.kernelThreadSize_KernelFunction_B.y,
            this.kernelThreadSize_KernelFunction_B.z
            );
        plane_A.GetComponent<Renderer>().material.mainTexture = this.renderTexture_A;
        plane_B.GetComponent<Renderer>().material.mainTexture = this.renderTexture_B;
    }
}
