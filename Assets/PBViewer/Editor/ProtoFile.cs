using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PBViewer.Editor
{
    internal class ProtoFile
    {
        private readonly List<Message> mMessages = new List<Message>();

        private string mFileName;
        private GUIContent mContent;
        private string packageName;

        public string FileName => this.mFileName;
        public GUIContent Content => this.mContent;

        public string PackageName => packageName;

        public ProtoFile(string fileName,GUIContent content,string package)
        {
            this.mFileName = fileName;
            this.mContent = content;
            this.packageName = package;
        }

        public Message CreateMessage(int wid, Vector2 position, GUIContent title, GUIContent content, Model.MessageType type)
        {
            var msg = new Message(wid, new Rect(position, new Vector2(300, 400)), title, content, type, this);
            mMessages.Add(msg);

            return msg;
        }

        public void DeleteMessage(Message message)
        {
            lock (mMessages)
            {
                if (mMessages.RemoveAll((item) => { return item.Title.text.Equals(message.Title.text); }) > 0)
                {
                    EditorWindow.GetWindow<PBViewerEditorWindow>()?.Repaint();
                }
            }
        }

        public void DeleteMessageItem(Message message, MessageItem messageItem)
        {
            lock (mMessages)
            {
                var msg = mMessages.Find((item) => { return item.Title.text.Equals(message.Title.text); });
                lock (msg.MessageItems)
                {
                    if (msg.MessageItems.RemoveAll((item) => { return item.PropertyName.Equals(messageItem.PropertyName); }) > 0)
                    {
                        EditorWindow.GetWindow<PBViewerEditorWindow>()?.Repaint();
                    }
                }
            }
        }

        public List<Message> Messages
        {
            get { return this.mMessages; }
        }

        public void GetOtherMsg(Message msg, Model.MessageType messageType, ref List<Message> otherMsg, ref List<string> otherMsgLabels)
        {
            otherMsg.Clear();
            otherMsgLabels.Clear();
            foreach (var item in mMessages)
            {
                if (item.Type != messageType)
                {
                    continue;
                }

                if (item.Title.text.Equals(msg.Title.text))
                {
                    continue;
                }

                otherMsg.Add(item);
                otherMsgLabels.Add(item.Title.text);
            }
        }

        public bool IsHas(string name)
        {
            var msg = mMessages.FindAll((item) => item.Title.text.Equals(name));
            return msg.Count > 0;
        }

        public void OnGUI()
        {
            foreach (var messageWindow in mMessages)
            {
                messageWindow.OnGUI();
            }
        }
    }
}