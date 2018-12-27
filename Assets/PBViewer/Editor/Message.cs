using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PBViewer.Editor
{
    internal class Message
    {
        private Rect mPostion;
        private int mWid;
        private GUIContent mTitle;
        private GUIContent mContent;
        private Model.MessageType mType;
        private ProtoFile mProtoFile;
        private Vector2 scrollPos;

        private Rect scrollPosRect;
        private Rect scrollViewRect;

        private readonly List<MessageItem> messageItems = new List<MessageItem>();
        internal readonly Dictionary<string, MessageItem> ObserverItems = new Dictionary<string, MessageItem>();

        public bool IsRemvoe = false;

        private Texture lineTexture;

        public List<MessageItem> MessageItems
        {
            get { return this.messageItems; }
        }

        ////Handles.DrawBezier(p2, p3, new Vector2(p3.x - p2.x, p3.y - p2.y) / 4, new Vector2(p3.x - p2.x, p3.y - p2.y) / 4 * 3, color, null, width);
        public Message(int wid, Rect postion, GUIContent title, GUIContent content, Model.MessageType type, ProtoFile protoFile)
        {
            this.mPostion = postion;
            this.mWid = wid;
            this.mTitle = title;
            this.mContent = content;
            this.mType = type;
            this.mProtoFile = protoFile;
        }

        public GUIContent Title => this.mTitle;

        public GUIContent Content => this.mContent;

        public Model.MessageType Type => this.mType;

        public Message OrignalMessage;

        public Rect Pos
        {
            get { return this.mPostion; }
        }

        public Message Clone()
        {
            return new Message(this.mWid, this.mPostion, this.mTitle, this.mContent, this.mType, this.mProtoFile)
            {
                OrignalMessage = this,
            };
        }

        public void OnGUI()
        {
            lock (messageItems)
            {
                var height = 0;
                var widht = 0;
                var itemIndex = messageItems.Count;
                while (itemIndex > 0)
                {
                    itemIndex--;
                    var item = messageItems[itemIndex];
                    if (!item.IsRemvoe)
                    {
//                        height += (int) rect.height * 2 + hSpace * 2 + hGroupSpace + 20;
                        continue;
                    }

                    messageItems.Remove(item);
                    item = null;
                }

                this.scrollPosRect = new Rect() {x = 6, y = 16, width = this.mPostion.width - 10, height = this.mPostion.height - 50};
                this.scrollViewRect = new Rect() {x = 6, y = 16, width = this.mPostion.width - 10, height = 200};
            }

            foreach (var messageItem in messageItems)
            {
                float lineWidth = 4;
                Vector2 lStart = messageItem.LeftGlobalPos;
                Vector2 lEnd = new Vector2(messageItem.LeftGlobalPos.x - 50, messageItem.LeftGlobalPos.y);
//                Handles.DrawBezier(lStart, lEnd, lStart, lEnd, Color.green, null, lineWidth);

                Vector2 rStart = messageItem.RightGlobalPos;
//                Vector2 rEnd = new Vector2(messageItem.RightGlobalPos.x + 50, messageItem.RightGlobalPos.y);
//                Handles.DrawBezier(rStart, rEnd, rStart, rEnd, Color.green, null, lineWidth);

                if (messageItem.PropertyType == Model.PropertyType.Enum || messageItem.PropertyType == Model.PropertyType.Message)
                {
                    Vector2 start = messageItem.LeftGlobalPos;
                    Vector2 end = new Vector2(messageItem.LinkCopyMessage.OrignalMessage.Pos.x,
                        messageItem.LinkCopyMessage.OrignalMessage.Pos.y + messageItem.LinkCopyMessage.OrignalMessage.Pos.height / 2);
                    Handles.DrawBezier(rStart, end, new Vector2(rStart.x + 100, rStart.y + 25), new Vector2(end.x - 100, end.y - 25), Color.red, null, lineWidth);
                }
            }

            //DrawLine();
            //绘画窗口 
            this.mPostion = GUI.Window(this.mWid, this.mPostion, DrawNodeWindow, $"{mType}  {this.mTitle.text}");
        }

        //绘画窗口函数
        void DrawNodeWindow(int id)
        {
            const int hGroupSpace = 6;
            const int itemSpace = 3;
            const int hSpace = 0;
            const int wSpace = 0;

            var rect = new Rect(10, 16, 200, 18);
            var rectPos = new Rect(rect);

            //移除MessageItem

            lock (messageItems)
            {
                messageItems.Sort((a, b) => (int.Parse(a.Flag) < int.Parse(b.Flag)) ? -1 : 1);

                this.scrollPos = GUI.BeginScrollView(scrollPosRect, this.scrollPos, scrollViewRect);

                foreach (var messageItem in messageItems)
                {
                    GUIContent guiContent;
                    Vector2 contentSize;
                    if (!string.IsNullOrEmpty(messageItem.Content))
                    {
                        guiContent = new GUIContent(string.IsNullOrEmpty(messageItem.Content) ? "" : $"//{messageItem.Content}");
                        contentSize = EditorStyles.label.CalcSize(guiContent);
                        guiContent.tooltip = "comment";
                        rectPos.width = contentSize.x;
                        rectPos.height = contentSize.y;
                        GUI.Label(rectPos, guiContent);
                    }

                    rectPos.width = 0;
                    rectPos.y += rectPos.height + hSpace;

                    //左连接点
                    var lRect = new Rect() {x = 5, y = rectPos.y + 18, width = 10, height = 10};
//                    messageItem.LeftSolt = new Rect(lRect) {x = 5, y = lRect.y + lRect.height / 2};
//                    GUI.Button(lRect, "");
                    //右连接点
                    var rRect = new Rect(lRect) {x = mPostion.width - lRect.x - lRect.width};
                    messageItem.RightSolt = new Rect(rRect) {y = rRect.y + rRect.height / 2};
                    GUI.Button(rRect, "");

                    if (messageItem.Keyword != Model.Keyword.None)
                    {
                        guiContent = new GUIContent(messageItem.Keyword.ToString().ToLower());
                        contentSize = EditorStyles.label.CalcSize(guiContent);
                        guiContent.tooltip = @"protobuf kewword";
                        rectPos = new Rect(rectPos) {width = contentSize.x, height = contentSize.y};
                        GUI.Label(rectPos, guiContent);
                    }

                    if (messageItem.PropertyType != Model.PropertyType.None)
                    {
                        if (messageItem.PropertyType == Model.PropertyType.Message || messageItem.PropertyType == Model.PropertyType.Enum)
                        {
                            guiContent = new GUIContent(messageItem.LinkCopyMessage.Title.text);
                        }
                        else
                        {
                            guiContent = new GUIContent(messageItem.PropertyType.ToString().ToLower());
                        }

                        contentSize = EditorStyles.label.CalcSize(guiContent);
                        guiContent.tooltip = @"type of message or base protobuf type";
                        rectPos.x += rectPos.width + wSpace;
                        rectPos = new Rect(rectPos) {width = contentSize.x, height = contentSize.y};
                        GUI.Label(rectPos, guiContent);
                    }

                    guiContent = new GUIContent(messageItem.PropertyName.ToLower());
                    contentSize = EditorStyles.label.CalcSize(guiContent);
                    guiContent.tooltip = "property name";
                    rectPos.x += rectPos.width + wSpace;
                    rectPos = new Rect(rectPos) {width = contentSize.x, height = contentSize.y};
                    GUI.Label(rectPos, guiContent);

                    guiContent = new GUIContent("=");
                    contentSize = EditorStyles.label.CalcSize(guiContent);
                    rectPos.x += rectPos.width + wSpace;
                    rectPos = new Rect(rectPos) {width = contentSize.x, height = contentSize.y};
                    GUI.Label(rectPos, guiContent);

                    guiContent = new GUIContent(messageItem.Flag);
                    contentSize = EditorStyles.label.CalcSize(guiContent);
                    guiContent.tooltip = @"flag";
                    rectPos.x += rectPos.width + wSpace;
                    rectPos = new Rect(rectPos) {width = contentSize.x, height = contentSize.y};
                    GUI.Label(rectPos, guiContent);

                    rectPos = new Rect() {y = rectPos.y, width = 20, height = 20};
                    rectPos.x = scrollPosRect.width - rectPos.width * 2 - 12;
                    rectPos.y = rectPos.y + rectPos.height + hSpace;
                    if (GUI.Button(rectPos, new GUIContent("E")))
                    {
                        var win = EditorWindow.GetWindow<MessageItemEditorWindow>();
                        win.Init(this, MessageItemEditorWindow.OpenType.Editor, messageItem);
                        win.Show();
                    }

                    rectPos = new Rect(rectPos);
                    rectPos.x = rectPos.x + rectPos.width + 3;
                    if (GUI.Button(rectPos, new GUIContent("D")))
                    {
                        if (!Model.Instance.IsWindowOpening)
                        {
                            messageItem.IsRemvoe = true;
                        }
                    }

                    rectPos.x = rect.x;
                    rectPos.y += rectPos.height + hGroupSpace;
                }

                GUI.EndScrollView();


                rectPos = new Rect {height = 15, width = this.mPostion.width / 2, x = 0, y = this.mPostion.height - 15};
                if (GUI.Button(rectPos, "Create"))
                {
                    if (!Model.Instance.IsWindowOpening)
                    {
                        if (IsRemvoe == true)
                        {
                            EditorUtility.DisplayDialog("tip", "The Message was Delete", "ok");
                        }
                        else
                        {
                            var win = EditorWindow.GetWindow<MessageItemEditorWindow>();
                            win.Init(this, MessageItemEditorWindow.OpenType.Create);
                            win.Show();
                        }
                    }
                }

                rectPos = new Rect() {height = 15, width = this.mPostion.width / 2};
                rectPos.x = rectPos.width;
                rectPos.y = this.mPostion.height - 15;
                if (GUI.Button(rectPos, "Delete"))
                {
                    if (!Model.Instance.IsWindowOpening)
                    {
                        if (EditorUtility.DisplayDialog("tip", "Are you sure to delete?", "ok"))
                        {
                            IsRemvoe = true;
                        }
                    }
                }
            }

            GUI.DragWindow();
        }

        public void CreateItem(MessageItem messageItem)
        {
            var has = messageItems.FindAll((item) => { return item.PropertyName.Equals(messageItem.PropertyName); });
            if (has.Count > 0)
            {
                EditorUtility.DisplayDialog("tip", $"the property name is using", "ok");
                return;
            }

            messageItem.ParentMessage = this;
            messageItems.Add(messageItem);
        }
    }
}