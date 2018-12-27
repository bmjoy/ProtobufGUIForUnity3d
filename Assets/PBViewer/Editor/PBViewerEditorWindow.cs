using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

namespace PBViewer.Editor
{
    internal class PBViewerEditorWindow : EditorWindow
    {
        [MenuItem("Window/PFViewer", false, 2000)]
        private static void Open()
        {
            var pb = GetWindow<PBViewerEditorWindow>();
            pb.Show();

//            GUIStyle style = EditorStyles.label;
//            style.font =  Resources.GetBuiltinResource<Font>("Arial.ttf");
//             GameObject go=new GameObject();
//            Text text= go.AddComponent<Text>();
//            text.font = style.font;
//            text.text = "sdfsd";
//            style.fontSize = 14;
            //{style.CalcSize()}  
//            Debug.Log($"{style.fontSize}     {style.lineHeight}   {text.preferredWidth}");

//            Execute($"{Application.dataPath}/ProtobufScripts/", new string[] {"tt", "tt1"});
//            Execute($"H:/Workspaces/workspace_unity20170414f1_Demo/ToolProtobuff/", new string[] {"tt", "tt1"});
        }


        private int globalMessageId = 0;

        private int fileSelectIndex = 0;
        private int filePrevSelectIndex = -1;

        private GUIContent createContent = new GUIContent("Create");
        private Rect createRect;

        private void OnEnable()
        {
            globalMessageId = 0;
            Model.Instance.ProtoFiles.Clear();
            Model.Instance.CurrProtoFile = null;
        }

        private void OnDisable()
        {
            CloseWin<MessageItemEditorWindow>();
            CloseWin<CreateFileEditorWindow>();
        }

        private void CloseWin<T>() where T : EditorWindow
        {
            GetWindow<T>()?.Close();
        }

        private void CreateProtoFile()
        {
            var list = Model.Instance.ProtoFiles;
            var win = GetWindow<CreateFileEditorWindow>();
            win.titleContent = new GUIContent("Create PF");
            win.Show();
            win.maxSize = win.minSize = new Vector2(300, 200);
            win.Init((msgTitle, msgContent, package) =>
            {
                var pfs = list.FindAll((item) => item.FileName == msgTitle);
                if (pfs.Count > 0)
                {
                    EditorUtility.DisplayDialog("tip", $"the proto file name of {msgTitle} is using", "ok");
                    return;
                }

                win.Close();
                base.Repaint();
                var protoFile = new ProtoFile(msgTitle, new GUIContent(msgContent), package);
                list.Add(protoFile);
                this.fileSelectIndex = list.Count - 1;
            });
        }

        /// <summary>
        /// 保存文件
        /// </summary>
        private void SaveFile()
        {
            Model.Save();
        }

        private void DeleteProtoFile()
        {
            var curr = Model.Instance.CurrProtoFile;
            if (null != curr)
            {
                var hasItem = Model.Instance.ProtoFiles.Find((item) => { return item.FileName.Equals(curr.FileName); });
                Model.Instance.ProtoFiles.Remove(hasItem);
                var list = Model.Instance.ProtoFiles;
                this.fileSelectIndex = (list.Count > 0 ? list.Count - 1 : 0);
            }
        }

        private void CreateMessage(Model.MessageType messageType, Vector2 pos)
        {
            if (null == Model.Instance.CurrProtoFile)
            {
                return;
            }

            var win = CreateFileEditorWindow.GetWindow<CreateFileEditorWindow>();
            win.titleContent = new GUIContent($"Create {messageType}");
            var size = new Vector2(300, 200);
//            win.position = new Rect(pos, size);
            win.maxSize = win.minSize = size;
            win.Init((msgTitle, msgContent) =>
            {
                if (Model.Instance.CurrProtoFile.IsHas(msgTitle))
                {
                    EditorUtility.DisplayDialog("tip", $"the {messageType} name of {msgTitle} is using", "ok");
                    return;
                }

                win.Close();
                base.Repaint();
                Model.Instance.CurrProtoFile.CreateMessage(this.globalMessageId++, pos, new GUIContent(msgTitle), new GUIContent(msgContent), messageType);
            });
        }

        private void DeleteMessage()
        {
        }

        private void OnGUI()
        {
            var list = Model.Instance.ProtoFiles;
            var curr = Model.Instance.CurrProtoFile;


            if (null != curr)
            {
                lock (curr.Messages)
                {
                    var msgIndex = curr.Messages.Count;
                    while (msgIndex > 0)
                    {
                        msgIndex--;
                        var temp = curr.Messages[msgIndex];
                        if (!temp.IsRemvoe)
                        {
                            continue;
                        }

                        foreach (var item in temp.ObserverItems.Values)
                        {
                            item.IsRemvoe = true;
                        }

                        curr.Messages.Remove(temp);
                        temp = null;
                    }

                    BeginWindows();
                    foreach (var pf in curr.Messages)
                    {
                        pf.OnGUI();
                    }

                    EndWindows();
                }
            }


            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Save", EditorStyles.toolbarButton))
            {
                if (!Model.Instance.IsWindowOpening)
                {
                    if (EditorUtility.DisplayDialog("tip", "Are you sure to Save?", "ok"))
                    {
                        this.SaveFile();
                    }
                }
            }

            GUILayout.Space(6);
            if (GUILayout.Button("Create", EditorStyles.toolbarButton))
            {
                if (!Model.Instance.IsWindowOpening)
                {
                    this.CreateProtoFile();
                }
            }

            GUILayout.Space(6);
            if (GUILayout.Button("Delete", EditorStyles.toolbarButton))
            {
                if (!Model.Instance.IsWindowOpening)
                {
                    if (EditorUtility.DisplayDialog("tip", "Are you sure to delete?", "ok"))
                    {
                        this.DeleteProtoFile();
                    }
                }
            }

            var len = list.Count;
            var fileLabels = new string[len];
            if (len != 0)
            {
                for (var i = 0; i < len; i++)
                {
                    fileLabels[i] = list[i].FileName;
                }
            }

            GUILayout.Space(6);
            this.fileSelectIndex = EditorGUILayout.Popup(null != curr ? curr.FileName : "", this.fileSelectIndex, fileLabels, EditorStyles.toolbarDropDown);
            if (this.fileSelectIndex != this.filePrevSelectIndex)
            {
                if (list.Count > 0)
                {
                    Model.Instance.CurrProtoFile = list[this.fileSelectIndex];
                    this.fileSelectIndex = this.filePrevSelectIndex;
                }
                else
                {
                    Model.Instance.CurrProtoFile = null;
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (null != curr && Event.current.type == EventType.ContextClick)
            {
                if (Model.Instance.IsWindowOpening)
                {
                    return;
                }

                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("New Message"), false, MenuCallback, new object[] {Model.MessageType.Message, Event.current.mousePosition});
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("New Enum"), false, MenuCallback, new object[] {Model.MessageType.Enum, Event.current.mousePosition});
                menu.ShowAsContext();
                Event.current.Use();
            }
        }


        private void MenuCallback(object obj)
        {
            var objs = (object[]) obj;
            var type = (Model.MessageType) objs[0];
            CreateMessage(type, (Vector2) objs[1]);
            Debug.Log($"{type}");
        }
    }
}