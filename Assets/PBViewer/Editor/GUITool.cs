using UnityEditor;
using UnityEngine;

namespace PBViewer.Editor
{
    public static class GUITool
    {
  
        static GUITool()
        {
        }

        public static Rect Label(Rect pos, Rect itemRect, GUIContent content)
        {
            var rect = new Rect()
            {
                x = pos.x + itemRect.x,
                y = pos.y + itemRect.y,
                width = pos.width + itemRect.width,
                height = pos.height + itemRect.height
            };
            GUI.Label(rect, content);
            return rect;
        }
    }
}