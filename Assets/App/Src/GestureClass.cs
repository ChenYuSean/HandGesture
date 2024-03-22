using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using Mediapipe.Tasks.Components.Containers;

namespace HandTask
{

    public static class Gestures
    {
        public enum GestureClass
        {
            NULL,
            palm,
            fist,
            one,
            ok,
            two_up,
            peace,
            three,
            three2,
            four,
            call,
            rock,
            like,
            dislike
        }

        public static GestureClass ToCategory(int val)
        {
            return (GestureClass)(val+1); // shift right for adding NULL in category
        }

        public static List<float> LandmarksPreprocessing(List<NormalizedLandmark> landmarks, int imageWidth, int imageHeight)
        {
            List<float> preprocessed = new List<float>();

            foreach (var lm in landmarks)
            {
                preprocessed.Add(lm.x * imageWidth);
                preprocessed.Add(lm.y * imageHeight);
            }

            // Normalized
            float max = preprocessed.Max();

            //Debug.Log($"Before: {string.Join(", ", preprocessed)}");
            for(int i = 0; i < preprocessed.Count; i++)
            {
                preprocessed[i] = preprocessed[i] / max;
            }
            //Debug.Log($"After: {string.Join(", ", preprocessed)}");


            return preprocessed;
        }

    }

}