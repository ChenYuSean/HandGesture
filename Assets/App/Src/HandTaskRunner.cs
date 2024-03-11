// Copyright (c) 2023 homuler
//
// Use of this source code is governed by an MIT-style
// license that can be found in the LICENSE file or at
// https://opensource.org/licenses/MIT.

using System.Linq;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample;
using Mediapipe.Unity.Sample.HandLandmarkDetection;
using Experimental = Mediapipe.Unity.Experimental;
using Tasks = Mediapipe.Tasks;
using Unity.Barracuda;
using PimDeWitte.UnityMainThreadDispatcher;
using TMPro;

namespace HandTask
{
    public class HandTaskRunner : VisionTaskApiRunner<HandLandmarker>
    {
        [SerializeField] private HandLandmarkerResultAnnotationController _handLandmarkerResultAnnotationController;

        private Experimental.TextureFramePool _textureFramePool;

        public readonly HandLandmarkDetectionConfig config = new HandLandmarkDetectionConfig();

        public TMP_Text display_text;

        // Our prediction model
        public NNModel modelAsset;
        Model model;
        IWorker modelWorker;

        const int B = 1;
        const int H = 1;
        const int W = 1;
        const int C = 43;

        public override void Stop()
        {
            base.Stop();
            _textureFramePool?.Dispose();
            _textureFramePool = null;
        }

        private void Awake()
        {
            // Load DL Model
            model = ModelLoader.Load(modelAsset);
            modelWorker = WorkerFactory.CreateWorker(model, WorkerFactory.Device.CPU);
        }


        protected override IEnumerator Run()
        {
            // Set Config
            config.NumHands = 2;

            Debug.Log($"Delegate = {config.Delegate}");
            Debug.Log($"Running Mode = {config.RunningMode}");
            Debug.Log($"NumHands = {config.NumHands}");
            Debug.Log($"MinHandDetectionConfidence = {config.MinHandDetectionConfidence}");
            Debug.Log($"MinHandPresenceConfidence = {config.MinHandPresenceConfidence}");
            Debug.Log($"MinTrackingConfidence = {config.MinTrackingConfidence}");

            yield return AssetLoader.PrepareAssetAsync(config.ModelPath);

            // Create Task
            var options = config.GetHandLandmarkerOptions(config.RunningMode == Tasks.Vision.Core.RunningMode.LIVE_STREAM ? OnHandLandmarkDetectionOutput : null);
            taskApi = HandLandmarker.CreateFromOptions(options);
            var imageSource = ImageSourceProvider.ImageSource;

            yield return imageSource.Play();

            if (!imageSource.isPrepared)
            {
                Debug.LogError("Failed to start ImageSource, exiting...");
                yield break;
            }

            // Use RGBA32 as the input format.
            // TODO: When using GpuBuffer, MediaPipe assumes that the input format is BGRA, so maybe the following code needs to be fixed.
            _textureFramePool = new Experimental.TextureFramePool(imageSource.textureWidth, imageSource.textureHeight, TextureFormat.RGBA32, 10);

            // NOTE: The screen will be resized later, keeping the aspect ratio.
            screen.Initialize(imageSource);

            SetupAnnotationController(_handLandmarkerResultAnnotationController, imageSource);

            var transformationOptions = imageSource.GetTransformationOptions();
            var flipHorizontally = transformationOptions.flipHorizontally;
            var flipVertically = transformationOptions.flipVertically;
            var imageProcessingOptions = new Tasks.Vision.Core.ImageProcessingOptions(rotationDegrees: (int)transformationOptions.rotationAngle);

            AsyncGPUReadbackRequest req = default;
            var waitUntilReqDone = new WaitUntil(() => req.done);
            var result = HandLandmarkerResult.Alloc(options.numHands);

            while (true)
            {
                if (isPaused)
                {
                    yield return new WaitWhile(() => isPaused);
                }

                if (!_textureFramePool.TryGetTextureFrame(out var textureFrame))
                {
                    yield return new WaitForEndOfFrame();
                    continue;
                }

                // Copy current image to TextureFrame
                req = textureFrame.ReadTextureAsync(imageSource.GetCurrentTexture(), flipHorizontally, flipVertically);
                yield return waitUntilReqDone;

                if (req.hasError)
                {
                    Debug.LogError($"Failed to read texture from the image source, exiting...");
                    break;
                }

                var image = textureFrame.BuildCPUImage();
                switch (taskApi.runningMode)
                {
                    case Tasks.Vision.Core.RunningMode.IMAGE:
                        if (taskApi.TryDetect(image, imageProcessingOptions, ref result))
                        {
                            _handLandmarkerResultAnnotationController.DrawNow(result);
                        }
                        else
                        {
                            _handLandmarkerResultAnnotationController.DrawNow(default);
                        }
                        break;
                    case Tasks.Vision.Core.RunningMode.VIDEO:
                        if (taskApi.TryDetectForVideo(image, GetCurrentTimestampMillisec(), imageProcessingOptions, ref result))
                        {
                            _handLandmarkerResultAnnotationController.DrawNow(result);
                        }
                        else
                        {
                            _handLandmarkerResultAnnotationController.DrawNow(default);
                        }
                        break;
                    case Tasks.Vision.Core.RunningMode.LIVE_STREAM:
                        taskApi.DetectAsync(image, GetCurrentTimestampMillisec(), imageProcessingOptions);
                        break;
                }

                textureFrame.Release();
            }
        }

        private void OnHandLandmarkDetectionOutput(HandLandmarkerResult result, Image image, long timestamp)
        {
            _handLandmarkerResultAnnotationController.DrawLater(result);

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    //Debug.Log("Execute On Main Thread");
                    PredictGesture(result);
                }
            );
        }


        private void PredictGesture(HandLandmarkerResult result)
        {
            if (result.handedness == null) return;

            Gestures.GestureClass[] gestures = new Gestures.GestureClass[result.handedness.Count];

            for (var hand_idx = 0; hand_idx < result.handedness.Count; hand_idx++)
            {
                // TODO: Fix the issus when there are multiple hands but the landmarks of the hands after the first one are null 
                if (result.handLandmarks[hand_idx].landmarks == null)
                {
                    Debug.LogWarning($"landmarks[{hand_idx}] is null");
                    continue;
                }

                var idx = 0;
                float[] input_features = new float[C];
                input_features[idx++] = result.handedness[hand_idx].categories[0].displayName.ToLower() == "right" ? 1.0f : 0.0f;

                // Preprocessing
                var preprocessed = Gestures.LandmarksPreprocessing(result.handLandmarks[hand_idx].landmarks);
                foreach (var item in preprocessed)
                {
                    input_features[idx++] = item;
                }
                //Debug.Log($"[{string.Join(", ", input_features)}]");

                // Inference Model
                Tensor input_tensor = new Tensor(B, H, W, C, input_features);
                modelWorker.Execute(input_tensor);

                Tensor output = modelWorker.PeekOutput();

                gestures[hand_idx] = Gestures.ToCategory(output.ArgMax()[0]); 

                input_tensor.Dispose();
            }

            //Debug.Log($"Gesture:{string.Join(", " , gestures)}");
            display_text.text = $"Gesture:{string.Join(", ", gestures)}";
        }

        public void OnDestroy()
        {
            modelWorker.Dispose();
        }

    }
}
