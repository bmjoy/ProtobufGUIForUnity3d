using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PBViewer.Editor
{
    internal class Model
    {
        private const string LEFT_BRACE = "{";
        private const string RIGHT_BRACE = "}";

        private readonly List<ProtoFile> mProtoFiles = new List<ProtoFile>();

        private Dictionary<string, string> baseTypes = new Dictionary<string, string>
        {
            {"double", "double"}, {"float", "float"}, {"int32", "int"}, {"int64", "long"}, {"uint32", "uint"}, {"uint64", "uint64"}, {"sint32", "sint32"}, {"sint64", "sint64"},
            {"fixed32", "uint"}, {"fixed64", "int"}, {"sfixed32", "double"}, {"sfixed64", "long"}, {"string", "string"}, {"bytes", "ByteString"}
        };

        public enum MessageType
        {
            Message = 1,
            Enum = 2,
        }

        public enum PropertyType
        {
            None = 0,
            Message = 1,
            Enum = 2,
            Double = 3,
            Float = 4,
            Int32 = 5,
            Int64 = 6,
            UInt32 = 7,
            UInt64 = 8,
            SInt32 = 9,
            SInt64 = 10,
            FIxed32 = 11,
            FIxed64 = 12,
            SFixed32 = 13,
            SFixed64 = 14,
            String = 15,
            Bytes = 16,
        }

        public enum Keyword
        {
            None = 0,
            Repeated = 1,
        }

        private static Model _mInstance = null;

        public static Model Instance => _mInstance ?? (_mInstance = new Model());

        public MessageItemEditorWindow MessageItemEditorWindow = null;
        public CreateFileEditorWindow CreateFileEditorWindow = null;

        public List<ProtoFile> ProtoFiles => mProtoFiles;

        public ProtoFile CurrProtoFile = null;

        public bool IsWindowOpening
        {
            get
            {
                if (null != CreateFileEditorWindow)
                {
                    CreateFileEditorWindow.Focus();
                    return true;
                }

                if (null != MessageItemEditorWindow)
                {
                    MessageItemEditorWindow.Focus();
                    return true;
                }

                return false;
            }
        }


        internal static void Save()
        {
            var curr = Model.Instance.CurrProtoFile;
            if (null == curr)
            {
                return;
            }

            if (curr.Messages.Count > 0)
            {
                var str = CreateFileData(curr);

                var rootPath = $"{Application.dataPath}/PBViewer/Editor/Protos/{curr.FileName}.proto";
                var bytes = System.Text.Encoding.UTF8.GetBytes(str);
                var file = File.Create(rootPath);
                file.Write(bytes, 0, bytes.Length);
                file.Flush();
                file.Close();
                Debug.Log($"{str}");
            }

            Execute($"{Application.dataPath}/ProtobufScripts/", new string[] {$"{curr.FileName}"});
        }


        private static string CreateFileData(ProtoFile file)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"//{file.Content.text}");
            sb.AppendLine("syntax = \"proto3\";");
            if (string.IsNullOrEmpty(file.PackageName))
            { 
                sb.AppendLine($"package \"{file.PackageName}\";");
                sb.AppendLine($"option csharp_namespace = \"{file.PackageName}\";");
            } 
            sb.AppendLine();
            var list = file.Messages;
            foreach (var message in list)
            {
                sb.AppendLine($"//{message.Content.text}");
                sb.AppendLine($"message {message.Title.text} {LEFT_BRACE}");
                foreach (var messageItem in message.MessageItems)
                {
                    sb.AppendLine($"   //{messageItem.Content}");

                    var str = string.Empty;

                    if (messageItem.PropertyType == PropertyType.Message)
                    {
                        str = (messageItem.Keyword == Model.Keyword.None) ? "" : ("   " + messageItem.Keyword.ToString().ToLower());
                    }

                    if (messageItem.PropertyType == Model.PropertyType.Message || messageItem.PropertyType == Model.PropertyType.Enum)
                    {
                        str += "   " + messageItem.LinkCopyMessage.Title.text;
                    }
                    else
                    {
                        str += "   " + messageItem.PropertyType.ToString().ToLower();
                    }

                    str += "   " + messageItem.PropertyName + "   =   " + messageItem.Flag + " ;";
                    sb.AppendLine(str);
                }

                sb.AppendLine($"{RIGHT_BRACE}");
                sb.AppendLine();
            }

            return sb.ToString();
        }


        private static bool Execute(string outPath, IEnumerable<string> fileNames)
        {
            try
            {
                if (string.IsNullOrEmpty(outPath))
                {
                    Debug.LogError("输出目录不存在！！");
                    return false;
                }

                if (null == fileNames || fileNames.Count() == 0)
                {
                    Debug.LogError(".proto文件 为空");
                    return false;
                }

                var rootPath = $"{Application.dataPath}/PBViewer/Editor";
                var filesStr = string.Empty;
                foreach (var fileName in fileNames)
                {
                    var tempStr = $"{rootPath}/Protos/{fileName}.proto";
                    if (string.IsNullOrEmpty(filesStr))
                    {
                        filesStr = tempStr;
                    }
                    else
                    {
                        filesStr += tempStr;
                    }
                }

                var exe = Execute($"{rootPath}/protoc.exe --csharp_out=\"{outPath}\" --csharp_opt=file_extension=.g.cs --proto_path=\"{rootPath}/\" {filesStr}");

                EditorUtility.DisplayDialog("Tip", exe == true ? "Commplete" : "Commplete Failure", "Ok");
                if (exe)
                {
                    AssetDatabase.Refresh();
                }

                return exe;
            }
            catch (Exception e)
            {
                Debug.LogError($"{e.Message}\n{e.StackTrace}");
            }

            return false;
        }

        /// <summary>  
        /// 执行DOS命令，返回DOS命令的输出  
        /// </summary>   
        /// <param name="command">dos命令</param>
        /// <param name="seconds">等待命令执行的时间（单位：毫秒），如果设定为0，则无限等待</param>
        /// <returns>返回DOS命令的输出</returns>  
        private static bool Execute(string command, int seconds = 10)
        {
            if (string.IsNullOrEmpty(command) || command.Equals(""))
            {
                return false;
            }

            var process = new Process();
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/C " + command,
                UseShellExecute = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardErrorEncoding = System.Text.Encoding.Default,
                StandardOutputEncoding = System.Text.Encoding.Default,
            };
            process.StartInfo = startInfo;
            try
            {
                if (process.Start())
                {
                    if (seconds == 0)
                    {
                        process.WaitForExit();
                    }
                    else
                    {
                        process.WaitForExit(seconds);
                    }

//                    output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    Debug.LogWarning($"command:::{command}");

                    if (!string.IsNullOrEmpty(error))
                    {
                        Debug.LogError(error);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message + "  \n" + e.StackTrace);
                return false;
            }

            finally
            {
                process.Close();
            }

            return true;
        }
    }
}