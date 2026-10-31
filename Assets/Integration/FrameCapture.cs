using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Integration
{
    // Renders the real scene and its UI into a GPU texture, independent of the
    // native window's swap chain. Used only by the opt-in executable smoke run.
    public static class FrameCapture
    {
        struct CanvasState
        {
            public Canvas canvas;
            public RenderMode mode;
            public Camera camera;
            public float planeDistance;
        }

        struct OverlayDepth
        {
            public RectTransform transform;
            public float localZ;
        }

        public static bool TryCapture(string path, out string error, int width = 1600, int height = 900)
        {
            error = null;
            Camera camera = Camera.main;
            if (camera == null) { error = "No active main camera"; return false; }
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            { error = "GPU frame capture requires a graphics device"; return false; }

            var canvases = new List<CanvasState>();
            var depths = new List<OverlayDepth>();
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            float previousAspect = camera.aspect;
            RenderTexture target = null;
            Texture2D pixels = null;
            try
            {
                target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB);
                camera.targetTexture = target;
                camera.aspect = (float)width / height;
                foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                    canvases.Add(new CanvasState { canvas = canvas, mode = canvas.renderMode,
                        camera = canvas.worldCamera, planeDistance = canvas.planeDistance });
                    // Overlay UI uses hierarchy draw order. Legacy SF-button instances
                    // contain local Z=-709, which is harmless on the overlay but would
                    // place them behind this temporary capture camera's near plane.
                    foreach (var transform in canvas.GetComponentsInChildren<RectTransform>(true))
                    {
                        if (transform == canvas.transform) continue;
                        var position = transform.localPosition;
                        if (Mathf.Abs(position.z) < .00001f) continue;
                        depths.Add(new OverlayDepth { transform = transform, localZ = position.z });
                        position.z = 0;
                        transform.localPosition = position;
                    }
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = Mathf.Max(camera.nearClipPlane + .5f, 1f);
                }
                Canvas.ForceUpdateCanvases();
                if (GraphicsSettings.currentRenderPipeline != null)
                {
                    // Verified against the installed URP package's SingleCameraRequest API.
                    var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                    if (!RenderPipeline.SupportsRenderRequest(camera, request))
                        throw new InvalidOperationException("Active render pipeline does not support camera render requests");
                    RenderPipeline.SubmitRenderRequest(camera, request);
                }
                else camera.Render();

                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply(false, false);
                string parent = Path.GetDirectoryName(Path.GetFullPath(path));
                Directory.CreateDirectory(parent);
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                return true;
            }
            catch (Exception exception) { error = exception.ToString(); return false; }
            finally
            {
                foreach (var state in canvases)
                {
                    if (state.canvas == null) continue;
                    state.canvas.renderMode = state.mode;
                    state.canvas.worldCamera = state.camera;
                    state.canvas.planeDistance = state.planeDistance;
                }
                foreach (var depth in depths)
                {
                    if (depth.transform == null) continue;
                    var position = depth.transform.localPosition;
                    position.z = depth.localZ;
                    depth.transform.localPosition = position;
                }
                camera.targetTexture = previousTarget;
                camera.aspect = previousAspect;
                Canvas.ForceUpdateCanvases();
                RenderTexture.active = previousActive;
                if (target != null) RenderTexture.ReleaseTemporary(target);
                if (pixels != null) UnityEngine.Object.Destroy(pixels);
            }
        }
    }
}
