using System;
using System.Collections.Generic;
using UnityEngine;

namespace PBViewer.Editor
{
    internal class MessageItem:IDisposable
    {
        public string Content { get; set; }
        public Model.Keyword Keyword { get; set; }

        public Model.PropertyType PropertyType { get; set; }
        public string PropertyName { get; set; }

        public Message ParentMessage { get; set; }
        public Message LinkCopyMessage { get; set; }

        public string Flag { get; set; }

        public bool IsRemvoe = false;

        public Rect LeftSolt;
        public Rect RightSolt;

        public Vector2 LeftGlobalPos
        {
            get
            {
                if (this.ParentMessage == null)
                {
                    return Vector2.zero;
                }

//                return new Vector2() {x = ParentMessage.Pos.x, y = ParentMessage.Pos.y};
                return new Vector2() {x = ParentMessage.Pos.x + LeftSolt.x, y = ParentMessage.Pos.y + LeftSolt.y};
            }
        }

        public Vector2 RightGlobalPos
        {
            get
            {
                if (this.ParentMessage == null)
                {
                    return Vector2.zero;
                }

//                return new Vector2() {x = ParentMessage.Pos.x, y = ParentMessage.Pos.y};
                return new Vector2() {x = ParentMessage.Pos.x + RightSolt.x, y = ParentMessage.Pos.y + RightSolt.y};
            }
        }

        public MessageItem Clone()
        {
            return new MessageItem()
            {
                Content = this.Content,
                Keyword = this.Keyword,
                PropertyType = this.PropertyType,
                PropertyName = this.PropertyName,
                LinkCopyMessage = this.LinkCopyMessage,
                Flag = this.Flag,
                IsRemvoe = this.IsRemvoe
            };
        }

        public void CopyTo(MessageItem messageItem)
        {
            this.Content = messageItem.Content;
            this.Keyword = messageItem.Keyword;
            this.PropertyName = messageItem.PropertyName;
            this.PropertyType = messageItem.PropertyType;
            this.LinkCopyMessage = messageItem.LinkCopyMessage;
            this.Flag = messageItem.Flag;
            this.IsRemvoe = messageItem.IsRemvoe;
        }

        public void Dispose()
        {
        }
    }
}