using System;
using UnityEditor;
using UnityEngine;

namespace PBViewer.Editor
{
    public class CreateFileEditorWindow : EditorWindow
    {
        private Action<string, string, string> mFileCallback = null;
        private Action<string, string> mMsgCallback = null;
        private string mName;
        private string mContent;
        private string mPackage;
        private OpenType mOpenType;

        private enum OpenType
        {
            File,
            Message,
            Enum,
        }

        public void Init(Action<string, string> callback)
        {
            this.mMsgCallback = callback;
            this.mOpenType = OpenType.Message;
            Model.Instance.CreateFileEditorWindow = this;
        }

        public void Init(Action<string, string, string> callback)
        {
            this.mFileCallback = callback;
            this.mOpenType = OpenType.File;
            Model.Instance.CreateFileEditorWindow = this;
        }

        private void OnDestroy()
        {
            Model.Instance.CreateFileEditorWindow = null;
        }

        private void OnGUI()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("name：", EditorStyles.largeLabel, GUILayout.Width(80));
            this.mName = GUILayout.TextField(this.mName);
            GUILayout.EndHorizontal();

            if (this.mOpenType == OpenType.File)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("package：", EditorStyles.largeLabel, GUILayout.Width(80));
                this.mPackage = GUILayout.TextField(this.mPackage);
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("content：", EditorStyles.largeLabel, GUILayout.Width(80));
            this.mContent = GUILayout.TextArea(this.mContent, GUILayout.Width(200), GUILayout.Height(100));
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            if (GUILayout.Button("Create"))
            {
                if (!string.IsNullOrEmpty(this.mName))
                {
                    if (this.mOpenType == OpenType.File)
                    {
                        this.mFileCallback?.Invoke(this.mName, this.mContent, this.mPackage);
                    }
                    else
                    {
                        this.mMsgCallback?.Invoke(this.mName, this.mContent);
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("tip", "name is Empty", "ok");
                }
            }
        }
    }
}