using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Riftline
{
    [InitializeOnLoad]
    public static class RiftlineEditorAutomation
    {
        const string CommandPath = "/tmp/riftline-editor-action.json";
        const string QualityCommandPath = "/tmp/riftline-quality-action.json";
        const string ResultPath = "/tmp/riftline-editor-action-result.json";
        static double nextPoll;

        [Serializable]
        public sealed class Request
        {
            public string Action;
            public string Id;
            public string Path;
        }

        [Serializable]
        public sealed class Response
        {
            public string Id;
            public bool Success;
            public string Message;
            public bool Playing;
            public string Scene;
        }

        static RiftlineEditorAutomation()
        {
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling) return;
            nextPoll = EditorApplication.timeSinceStartup + .25;
            string requestPath = File.Exists(QualityCommandPath) ? QualityCommandPath : CommandPath;
            if (!File.Exists(requestPath)) return;
            Request request = null;
            try
            {
                request = JsonUtility.FromJson<Request>(File.ReadAllText(requestPath));
                File.Delete(requestPath);
                string message;
                switch (request.Action)
                {
                    case "Art":
                        RiftlineArtSetup.Configure();
                        message = "Generated art configured";
                        break;
                    case "Acceptance":
                        message = RiftlinePlayValidation.Action(string.IsNullOrEmpty(request.Path) ? "Start" : request.Path);
                        break;
                    case "Build":
                        RiftlineSceneBuilder.Build();
                        message = "Scene built";
                        break;
                    case "Validate":
                        message = RiftlineCoreValidation.RunAll();
                        break;
                    case "Play":
                        EditorApplication.isPlaying = true;
                        message = "Play requested";
                        break;
                    case "Stop":
                        EditorApplication.isPlaying = false;
                        message = "Stop requested";
                        break;
                    case "Reconnect":
                        Unity.AI.MCP.Editor.UnityMCPBridge.Stop();
                        Unity.AI.MCP.Editor.UnityMCPBridge.Start();
                        message = "Editor bridge restarted";
                        break;
                    case "Capture":
                        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Capture requires Play Mode.");
                        ScreenCapture.CaptureScreenshot(request.Path);
                        message = "Screenshot requested";
                        break;
                    case "Status":
                        var game = UnityEngine.Object.FindAnyObjectByType<RiftlineGame>();
                        message = game && game.Run != null ? "phase=" + game.Phase + "; distance=" + game.Run.Distance +
                            "; score=" + game.Run.Score : "No active game";
                        break;
                    default:
                        throw new ArgumentException("Unknown Editor action: " + request.Action);
                }
                Write(request, true, message);
            }
            catch (Exception exception)
            {
                Write(request, false, exception.ToString());
                Debug.LogException(exception);
            }
        }

        static void Write(Request request, bool success, string message)
        {
            File.WriteAllText(ResultPath, JsonUtility.ToJson(new Response
            {
                Id = request?.Id,
                Success = success,
                Message = message,
                Playing = EditorApplication.isPlaying,
                Scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path
            }, true));
        }
    }
}
