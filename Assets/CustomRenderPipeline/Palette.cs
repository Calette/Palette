using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace Palette
{
    public class CustomRenderPipeline : RenderPipeline
    {
        CommandBuffer _cb;

        public override void Dispose()
        {
            base.Dispose();
            if(_cb != null)
            {
                _cb.Dispose();
                _cb = null;
            }
        }

        public override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
        {
            base.Render(renderContext, cameras);

            if (_cb == null)
                _cb = new CommandBuffer();

            foreach(Camera camera in cameras)
            {
                //将上下文设置为当前相机的上下文
                renderContext.SetupCameraProperties(camera);
                //设置渲染目标的颜色为蓝色
                _cb.ClearRenderTarget(true, true, Color.blue);
                //提交指令队列至当前context处理
                renderContext.ExecuteCommandBuffer(_cb);
                //清空当前指令队列。
                _cb.Clear();
                //开始执行上下文
                renderContext.Submit();
            }
        }
    }
}