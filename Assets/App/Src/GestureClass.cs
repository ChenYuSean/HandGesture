using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace App
{

    public class Gestures
    {
        public enum GestureClass
        {
            NULL,
            A,
            B,
            C,
            D,
            E,
            F,
        }

        public static GestureClass ToCategory(int val)
        {
            return (GestureClass)(val+1); // shift right for adding NULL in category
        }


    }

}