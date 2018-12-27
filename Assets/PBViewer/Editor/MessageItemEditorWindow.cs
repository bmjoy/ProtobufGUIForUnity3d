using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PBViewer.Editor
{
    internal class MessageItemEditorWindow : EditorWindow
    {
        private MessageItem mMessageItem;
        private Message mMessage;

        private List<string> mLinkMesageLabels = new List<string>();
        private List<Message> mLinkMessages = new List<Message>();

        private List<string> mPropertyTypeLabels = new List<string>();
        private int mPropertyTypeLabelSelectIndex = 0;

        private List<string> mFlagLables = new List<string>(1024);
        private int mFlagLabelsSelectIndex = 0;

        private int mMsgSelectIndex = 0;

        private OpenType mOpenType;

        private MessageItem mOrignalMessageItem;

        public enum OpenType
        {
            Create,
            Editor
        }


        private void OnDestroy()
        {
            Model.Instance.MessageItemEditorWindow = null;
        }

        public void Init(Message msg, OpenType openType, MessageItem msgItem = null)
        {
            Model.Instance.MessageItemEditorWindow = this;
            this.mMessage = msg;
            this.mOpenType = openType;
            this.mMessageItem = null != msgItem ? msgItem.Clone() : new MessageItem();
            this.mOrignalMessageItem = msgItem;

            GetAllPropertyTypes();

            this.mFlagLables.Clear();
            GetAllFlags(ref this.mFlagLables);

            if (this.mOpenType == OpenType.Editor)
            {
                this.mPropertyTypeLabelSelectIndex = InitMPropertyTypeLabelSelectIndex(msgItem.PropertyType.ToString());

                if (this.mMessage.Type == Model.MessageType.Message)
                {
                    if ((this.mMessageItem.PropertyType == Model.PropertyType.Enum) || (this.mMessageItem.PropertyType == Model.PropertyType.Message))
                    {
                        this.mMsgSelectIndex = InitMsgSelectIndex(msgItem.LinkCopyMessage.Title.text);
                    }
                }

                foreach (var messageMessageItem in this.mMessage.MessageItems)
                {
                    if (!this.mFlagLables.Contains(messageMessageItem.Flag))
                    {
                        continue;
                    }
                    else
                    {
                        if (messageMessageItem.Flag.Equals(msgItem.Flag))
                        {
                            continue;
                        }
                    }

                    this.mFlagLables.Remove(messageMessageItem.Flag);
                }

                var len = this.mFlagLables.Count;
                for (var i = 0; i < len; i++)
                {
                    if (!this.mFlagLables[i].Equals(msgItem.Flag))
                    {
                        continue;
                    }

                    this.mFlagLabelsSelectIndex = i;
                    return;
                }

                this.mFlagLabelsSelectIndex = EditorGUILayout.Popup(this.mFlagLabelsSelectIndex, this.mFlagLables.ToArray());
                this.mMessageItem.Flag = this.mFlagLables[this.mFlagLabelsSelectIndex];
            }
            else
            {
                this.mPropertyTypeLabelSelectIndex = 0;
                foreach (var messageMessageItem in this.mMessage.MessageItems)
                {
                    if (!this.mFlagLables.Contains(messageMessageItem.Flag))
                    {
                        continue;
                    }

                    this.mFlagLables.Remove(messageMessageItem.Flag);
                }
            }
        }

        private void OnGUI()
        {
            if (null == this.mMessageItem)
            {
                return;
            }

            var curr = Model.Instance.CurrProtoFile;

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Content："), GUILayout.Width(100));
            this.mMessageItem.Content = GUILayout.TextArea(this.mMessageItem.Content, GUILayout.Width(200), GUILayout.Height(100));
            GUILayout.EndHorizontal();

            if (this.mMessage.Type == Model.MessageType.Message)
            {
                GUILayout.Space(6);
                GUILayout.BeginHorizontal();
                GUILayout.Label(new GUIContent("Keyword："), GUILayout.Width(100));
                this.mMessageItem.Keyword = (Model.Keyword) EditorGUILayout.EnumPopup(this.mMessageItem.Keyword);
                GUILayout.EndHorizontal();


                GUILayout.Space(6);
                GUILayout.BeginHorizontal();
                GUILayout.Label(new GUIContent("PropertyType："), GUILayout.Width(100));
                this.mPropertyTypeLabelSelectIndex = EditorGUILayout.Popup(this.mPropertyTypeLabelSelectIndex, mPropertyTypeLabels.ToArray());

                this.mMessageItem.PropertyType = (Model.PropertyType) Enum.Parse(typeof(Model.PropertyType), mPropertyTypeLabels[this.mPropertyTypeLabelSelectIndex]);
                GUILayout.EndHorizontal();


                if ((this.mMessageItem.PropertyType == Model.PropertyType.Enum) || (this.mMessageItem.PropertyType == Model.PropertyType.Message))
                {
                    GUILayout.Space(6);
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(110);

                    if (null != curr)
                    {
                        GetList(curr, this.mMessageItem.PropertyType, ref mLinkMesageLabels, ref mLinkMessages);

                        if (mLinkMesageLabels.Count > 0)
                        {
                            this.mMsgSelectIndex = EditorGUILayout.Popup(this.mMsgSelectIndex, this.mLinkMesageLabels.ToArray());
                            this.mMessageItem.LinkCopyMessage = mLinkMessages[this.mMsgSelectIndex].Clone();
                        }
                    }

                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                this.mMessageItem.PropertyType = Model.PropertyType.None;
            }


            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("PropertyName："), GUILayout.Width(100));
            this.mMessageItem.PropertyName = GUILayout.TextField(this.mMessageItem.PropertyName);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Flag："), GUILayout.Width(100));
            this.mFlagLabelsSelectIndex = EditorGUILayout.Popup(this.mFlagLabelsSelectIndex, this.mFlagLables.ToArray());
            this.mMessageItem.Flag = this.mFlagLables[this.mFlagLabelsSelectIndex];

            GUILayout.EndHorizontal();


            GUILayout.Space(12);
            if (this.mOpenType == OpenType.Create)
            {
                if (GUILayout.Button("Create"))
                {
                    if (string.IsNullOrEmpty(this.mMessageItem.PropertyName))
                    {
                        EditorUtility.DisplayDialog("tip", "PropertyName  is Empty", "ok");
                        return;
                    }

                    if (null != this.mMessageItem.LinkCopyMessage && (!this.mMessageItem.LinkCopyMessage.OrignalMessage.ObserverItems.ContainsKey(this.mMessageItem.PropertyName)))
                    {
                        this.mMessageItem.LinkCopyMessage.OrignalMessage.ObserverItems.Add(this.mMessageItem.PropertyName, this.mMessageItem);
                    }

                    this.mMessage.CreateItem(this.mMessageItem);
                    GetWindow<PBViewerEditorWindow>().Repaint();
                    Close();
                }
            }
            else
            {
                if (GUILayout.Button("Update"))
                {
                    if (!this.mOrignalMessageItem.PropertyName.Equals(this.mMessageItem.PropertyName))
                    {
                        if (null != this.mMessageItem.LinkCopyMessage && (this.mOrignalMessageItem.LinkCopyMessage.OrignalMessage.ObserverItems.ContainsKey(this.mOrignalMessageItem.PropertyName))) ;
                        {
                            this.mOrignalMessageItem.LinkCopyMessage.OrignalMessage.ObserverItems.Remove(this.mOrignalMessageItem.PropertyName);
                        }
                    }


                    this.mOrignalMessageItem.CopyTo(this.mMessageItem);
                    GetWindow<PBViewerEditorWindow>().Repaint();
                    Close();
                }
            }
        }

        private void GetList(ProtoFile curr, Model.PropertyType propertyType, ref List<string> propertyTypeLabels, ref List<Message> messages)
        {
            propertyTypeLabels.Clear();
            messages.Clear();
            var msges = curr.Messages.FindAll((item) => { return item.Type == (Model.MessageType) propertyType; });
            if (msges.Count > 0)
            {
                foreach (var item in msges)
                {
                    if (this.mMessage.Title.text.Equals(item.Title.text))
                    {
                        continue;
                    }

                    propertyTypeLabels.Add(item.Title.text);
                    messages.Add(item);
                }
            }
        }

        private void GetAllFlags(ref List<string> list)
        {
            for (var i = 1; i < 1025; i++)
            {
                list.Add(i.ToString());
            }
        }

        private int InitMsgSelectIndex(string linkMessageName)
        {
            GetList(Model.Instance.CurrProtoFile, this.mMessageItem.PropertyType, ref mLinkMesageLabels, ref mLinkMessages);

            var len = mLinkMesageLabels.Count;
            for (var i = 0; i < len; i++)
            {
                if (mLinkMesageLabels[i].Equals(linkMessageName))
                {
                    return i;
                }
            }

            return 0;
        }

        private int InitMPropertyTypeLabelSelectIndex(string propertyType)
        {
            var len = mPropertyTypeLabels.Count;
            for (var i = 0; i < len; i++)
            {
                if (mPropertyTypeLabels[i].Equals(propertyType))
                {
                    return i;
                }
            }

            return 0;
        }

        private void GetAllPropertyTypes()
        {
            mPropertyTypeLabels.Clear();
            for (int i = 1; i < 17; i++)
            {
                mPropertyTypeLabels.Add(((Model.PropertyType) i).ToString());
            }

            Model.Instance.CurrProtoFile?.GetOtherMsg(this.mMessage, Model.MessageType.Enum, ref mLinkMessages, ref mLinkMesageLabels);
            if (mLinkMesageLabels.Count == 0)
            {
                RemovePropertyType(Model.MessageType.Enum.ToString());
            }

            Model.Instance.CurrProtoFile?.GetOtherMsg(this.mMessage, Model.MessageType.Message, ref mLinkMessages, ref mLinkMesageLabels);
            if (mLinkMesageLabels.Count == 0)
            {
                RemovePropertyType(Model.MessageType.Message.ToString());
            }
        }

        private void RemovePropertyType(string type)
        {
            var has = mPropertyTypeLabels.Find((item) => { return item.Equals(type); });
            if (null != has)
            {
                mPropertyTypeLabels.Remove(has);
            }
        }
    }
}