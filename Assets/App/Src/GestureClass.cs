using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using Mediapipe.Tasks.Components.Containers;

namespace App
{

    public static class Gestures
    {
        public enum GestureClass
        {
            NULL,
            three,
            peace,
            fist,
            palm,
            four,
            ok,
            one
        }

        public static GestureClass ToCategory(int val)
        {
            return (GestureClass)(val+1); // shift right for adding NULL in category
        }

        public static List<float> LandmarksPreprocessing(List<NormalizedLandmark> landmarks)
        {
            List<float> preprocessed = new List<float>();

            float base_x = 0f, base_y = 0f;
            foreach (var lm in landmarks)
            {
                if (preprocessed.Count == 0)
                {
                    base_x = lm.x;
                    base_y = lm.y;
                }
                preprocessed.Add(lm.x - base_x);
                preprocessed.Add(lm.y - base_y);
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