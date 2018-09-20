using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScreenSpaceReflection : MonoBehaviour
{
    public float maxRayDistance;
    public float sampleCount;
    public float thickness;

    public Shader shader;
    private Camera cam;
    private Material mat;
    //private Matrix4x4 inverseViewProjectionMatrix;
    private Matrix4x4 projectionMatrix;
    private Matrix4x4 inverseProjectionMatrix;
    private Matrix4x4 viewMatrix;
    //private Matrix4x4 inverseViewMatrix;
    private Vector3 prePos;
    private Quaternion preRot;

    void Start ()
    {
        cam = GetComponent<Camera>();
        //cam.depthTextureMode = DepthTextureMode.DepthNormals;

        mat = new Material(shader);

        //inverseViewProjectionMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true);
        projectionMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true);
        inverseProjectionMatrix = projectionMatrix.inverse;
        
        //inverseViewProjectionMatrix *= cam.worldToCameraMatrix;
        //inverseViewProjectionMatrix = inverseViewProjectionMatrix.inverse;
        viewMatrix = cam.worldToCameraMatrix;
        //inverseViewMatrix = viewMatrix.inverse;

        //mat.SetMatrix("_InverseViewProjectionMatrix", inverseViewProjectionMatrix);
        // mat.SetMatrix("_InverseViewMatrix", inverseViewMatrix);
        mat.SetMatrix("_ProjectionMatrix", projectionMatrix);
        mat.SetMatrix("_InverseProjectionMatrix", inverseProjectionMatrix);
        mat.SetMatrix("_ViewMatrix", viewMatrix);

        prePos = transform.localPosition;
        preRot = transform.localRotation;
    }

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (mat != null)
        {
            if (prePos != transform.localPosition || preRot != transform.localRotation)
            {
                projectionMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true);
                inverseProjectionMatrix = projectionMatrix.inverse;
                viewMatrix = cam.worldToCameraMatrix;

                mat.SetMatrix("_ProjectionMatrix", projectionMatrix);
                mat.SetMatrix("_InverseProjectionMatrix", inverseProjectionMatrix);
                mat.SetMatrix("_ViewMatrix", viewMatrix);

                prePos = transform.localPosition;
                preRot = transform.localRotation;
            }

            mat.SetFloat("_MaxRayDistance", maxRayDistance);
            mat.SetFloat("_SampleCount", sampleCount);
            mat.SetFloat("_Thickness", thickness);
            
            Graphics.Blit(src, dest, mat);
        }
        else
        {
            Graphics.Blit(src, dest);
        }
    }
}
