using System.Collections.Generic;
using UnityEngine;

namespace OneMoreTime
{
    /// <summary>One-way platform: solid from above, passable from below; S + Space drops through it.</summary>
    [RequireComponent(typeof(BoxCollider2D), typeof(PlatformEffector2D))]
    public sealed class OneWayPlatform : MonoBehaviour
    {
        public Collider2D Surface => GetComponent<BoxCollider2D>();
        public float TopY => Surface.bounds.max.y;

        public static void Setup(GameObject go, Vector2 size, Vector2 offset)
        {
            var box = go.GetComponent<BoxCollider2D>(); if (box == null) box = go.AddComponent<BoxCollider2D>();
            box.size = size; box.offset = offset; box.usedByEffector = true;
            var effector = go.GetComponent<PlatformEffector2D>(); if (effector == null) effector = go.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true; effector.surfaceArc = 160; effector.useSideFriction = false; effector.useSideBounce = false;
            if (go.GetComponent<OneWayPlatform>() == null) go.AddComponent<OneWayPlatform>();
        }
    }
}
